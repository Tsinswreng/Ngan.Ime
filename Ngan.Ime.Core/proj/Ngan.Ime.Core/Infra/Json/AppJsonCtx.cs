namespace Ngan.Ime.Infra.Json;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsinswreng.CsCore;

[Doc($$"""
本應用的 JSON 源生成上下文（`System.Text.Json` 源生成）。

*為何一定要有它*：專案是 NativeAOT，運行時反射式序列化會被裁剪掉、直接在執行期拋例外。
凡是「上網線」的型別、以及「要按名讀寫成員」的型別，都必須在此登記，
否則症狀是「本機 debug 正常、AOT 發佈後才炸」。

*登記清單不在這裡*：`[JsonSerializable]` 由 `Ngan.Ime.Frontend/proj/Ngan.Ime.Scripts` 的
`GenAppJsonCtx` 掃描 {{nameof(IAppSerializable)}} 的實現者後生成到
`Tsinswreng.SrcGen/Ngan.Ime.Gen.g.cs`。本檔只留「執行期選項」與「轉換器清單」的接口。

**STJ 源生成器不支援把 `[JsonSerializable]` 分散到多個 partial 宣告**，
所以那批註解必須集中在生成檔的同一個宣告上——這也是一定要靠生成器、不能手寫散開的原因。
""")]
public partial class AppJsonCtx : JsonSerializerContext {
	[Doc($$"""
	線上一律使用的選項（手動序列化、DI 注入的 `Tsinswreng.CsJson.IJsonSerializer` 都用這一份）。

	*為何 `PropertyNamingPolicy = null`*：成員本來就是 PascalCase，不轉命名最省心；
	改策略等於改變對外協議，要兩端同時發版。

	*為何 `DefaultIgnoreCondition = WhenWritingNull`*：可空欄位沒值時不出現在 JSON 裡，
	payload 小一點、前端也不必區分「null」與「沒這個欄位」。

	*為何 `Encoder = UnsafeRelaxedJsonEscaping`*：預設編碼器會把中文寫成 `\uXXXX`，
	合法但難讀、也讓 payload 變大；這裡輸出原樣 UTF-8。

	*為何 `TypeInfoResolver = Default` 這一行不能省*：NativeAOT 下沒有反射式 resolver 兜底，
	漏了它等於整個序列化都失效。
	""")]
	public static JsonSerializerOptions WireOptions => field ??= MkWireOptions();

	[Doc($$"""
	建線上一律使用的那份選項。
	#Rtn[已掛好源生成 resolver 與自訂轉換器的選項。]
	#Throw[{{nameof(InvalidOperationException)}}][resolver 沒掛上時；那代表 AOT 下會靜默退化成反射。]
	""")]
	static JsonSerializerOptions MkWireOptions() {
		var Opt = new JsonSerializerOptions {
			PropertyNamingPolicy = null,
			WriteIndented = false,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			// 生成檔（Ngan.Ime.Gen.g.cs）的 JsonSourceGenerationOptions 也必須與這裡一致：
		// 兩邊不一致時同一個型別走不同路徑會寫出不同結果。目前兩邊都不開字符串枚舉，
		// 故枚舉一律以數字上線；要改成字符串時必須同時改生成器與本方法。
		// 這一行是本類存在的理由：讓「只帶 options」的序列化重載在 AOT 下仍走源生成。
			TypeInfoResolver = Default,
		};
		// 強類型值（Id 之類）的轉換器由生成器列出，這裡只負責掛上。
		Opt.Converters.AddRange(MkJsonConverters());
		// 守衛：resolver 為 null 時，非 AOT 環境會「看起來正常」（退回反射），
		// AOT 環境則在第一次序列化才炸。寧可在建構期就炸，也不要留到發佈後。
		if (Opt.TypeInfoResolver is null) {
			throw new InvalidOperationException(
				$"{nameof(AppJsonCtx)}.{nameof(WireOptions)} 沒有掛上 TypeInfoResolver："
				+ "源生成 context 未就緒。這通常意味著靜態初始化順序有問題。"
			);
		}
		return Opt;
	}

	[Doc($$"""
	列出要掛進 {{nameof(WireOptions)}} 的自訂轉換器。

	*為何是 partial 方法而不是 static 字段*：清單由生成檔寫入，而生成檔與本檔是不同檔案；
	同一型別的靜態字段初始化順序跨檔案沒有保證，用方法就能保證「被呼叫時清單已經組好」。

	#Rtn[每個強類型 Id 各一個轉換器；現階段沒有，故通常為空清單。]
	""")]
	public static partial IList<JsonConverter> MkJsonConverters();
}
