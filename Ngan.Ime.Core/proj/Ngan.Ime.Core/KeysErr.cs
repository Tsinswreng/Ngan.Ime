namespace Ngan.Ime;

using Tsinswreng.CsCore;
using Tsinswreng.CsErr;
using K = Tsinswreng.CsErr.IErrNode;
using static Tsinswreng.CsErr.ErrNode;

[Doc($$"""
異常鍵族（CsErr 的 {{nameof(IErrNode)}}）。全項目拋錯一律拋這裡的鍵，不拋裸字符串。

*鍵不是文案*：鍵的用途是「可識別的失敗原因」，不是給用戶看的句子。
本專案現階段沒有 i18n，故拋出時帶的鍵主要供日誌與後續翻譯使用。

*命名約定*：成員名結尾的 `__` 是參數佔位符，`ToErr(參數...)` 會按順序填入。
`Mk` 造出的鍵**不帶任何標籤**——這正是默認語義：公開可見的業務異常。
只有「不可示人」的少數情況才自己加 {{nameof(AppErrTags.Private)}}（或 {{nameof(AppErrTags.SysErr)}}），
本檔用 {{nameof(MkSys)}} 統一標系統級錯誤。

#Eg[
	```cs
	throw KeysErr.Rime.DllPathEmpty.ToErr();
	throw KeysErr.Common.ArgErr__.ToErr(nameof(Options));
	```
]
""")]
public static class KeysErr {
	[Doc("根節點。值來自 ErrRoot，此處只是給調用方一個好記的名字。")]
	public static K _Root = ErrRoot.Node;

	[Doc("不分模組的通用錯誤。")]
	public static class Common {
		[Doc("此組的根節點；子鍵都以它為父。")]
		public static K _R = Mk(ErrRoot.Node, [nameof(Common)]);

		[Doc($$"""
		參數不合法。佔位符填字段名。
		#Eg[`throw {nameof(ArgErr__)}.ToErr(nameof(Options))`]
		""")]
		public static K ArgErr__ = Mk(_R, [nameof(ArgErr__)]);

		[Doc("未預期的失敗。回應給用戶時只說「請稍後重試」，詳細原因看日誌。")]
		public static K UnknownErr = Mk(_R, [nameof(UnknownErr)]);

		[Doc("網絡不通或外部服務不可達。")]
		public static K NetWorkErr = Mk(_R, [nameof(NetWorkErr)]);
	}

	[Doc("程式自身的啟動期不變式被破壞（不是使用者操作造成的）。")]
	public static class Sys {
		[Doc("此組的根節點。")]
		public static K _R = Mk(ErrRoot.Node, [nameof(Sys)]);

		[Doc("還沒有人給 Di.SvcProvider 賦值就取服務：入口漏了建立容器。")]
		public static K DiNotSetup = MkSys(_R, [nameof(DiNotSetup)]);
	}

	[Doc("配置層的錯誤。")]
	public static class Cfg {
		[Doc("此組的根節點。")]
		public static K _R = Mk(ErrRoot.Node, [nameof(Cfg)]);

		[Doc("出廠配置把「日常配置檔路徑」寫成了空串：那是手改或部署錯誤，不是正常缺省。")]
		public static K RwCfgPathEmpty = MkSys(_R, [nameof(RwCfgPathEmpty)]);

		[Doc("配置檔存在但讀不了（權限、佔用、格式壞掉）。佔位符填檔案路徑。")]
		public static K FileBroken__ = MkSys(_R, [nameof(FileBroken__)]);
	}

	[Doc("應用儲存區（檔案系統）的錯誤。")]
	public static class Storage {
		[Doc("此組的根節點。")]
		public static K _R = Mk(ErrRoot.Node, [nameof(Storage)]);

		[Doc("這個宿主沒有檔案系統（Browser／WASM），不該向它要目錄。")]
		public static K NoBaseDir = MkSys(_R, [nameof(NoBaseDir)]);

		[Doc("宿主還沒把 Context／根目錄注入進來就有人要路徑：啟動順序寫錯。")]
		public static K CtxNotInjected = MkSys(_R, [nameof(CtxNotInjected)]);

		[Doc("系統拒絕提供應用儲存區目錄（例如外部儲存被拔掉）。")]
		public static K DirUnavailable = MkSys(_R, [nameof(DirUnavailable)]);
	}

	[Doc("Rime 引擎適配層的錯誤。")]
	public static class Rime {
		[Doc("此組的根節點。")]
		public static K _R = Mk(ErrRoot.Node, [nameof(Rime)]);

		[Doc("配置沒給 librime 的動態庫路徑，無法載入引擎。")]
		public static K DllPathEmpty = MkSys(_R, [nameof(DllPathEmpty)]);

		[Doc("配置沒給 Rime 的用戶資料目錄，引擎無法初始化。")]
		public static K UserDataDirEmpty = MkSys(_R, [nameof(UserDataDirEmpty)]);
	}

	[Doc($$"""
	造一個帶 {{nameof(AppErrTags.SysErr)}} 標籤的系統級錯誤鍵。
	#Prm[{{nameof(Parent)}}][父節點，用 {nameof(ErrRoot)}.Node 或本組 `_R`。]
	#Prm[{{nameof(Path)}}][自己的路徑段。]
	#Rtn[已標 SysErr 的鍵。]
	""")]
	static K MkSys(K? Parent, IReadOnlyList<str> Path){
		var R = Mk(Parent, Path);
		R.Tags ??= new HashSet<str>();
		R.Tags.Add(AppErrTags.SysErr);
		return R;
	}
}

[Doc($$"""
錯誤鍵樹的根。**刻意獨立成一個類型**，而不是 {{nameof(KeysErr)}} 的成員。

*為何*：分組類（{{nameof(KeysErr)}}.Common 等）的鍵要以根為父。若根長在外層 {{nameof(KeysErr)}} 上，
分組類初始化時讀根就會觸發 {{nameof(KeysErr)}} 初始化，而外層類初始化又要碰分組成員——
此時分組類**正在**初始化中，CLR 不允許重入，成員仍是 null。
根搬到獨立類型後，兩邊互不牽扯。
""")]
internal static class ErrRoot {
	[Doc("根鍵，路徑 `Error`。")]
	public static K Node = Mk(null, ["Error"]);
}
