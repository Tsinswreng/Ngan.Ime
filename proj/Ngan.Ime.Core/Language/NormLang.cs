namespace Ngan.Ime.Core.Language;

/// 語言代碼採用的標識標準。
/// 語音識別首階段只使用 BCP 47；保留枚舉是爲了讓模型可在未來兼容其他標識體系。
public enum ELangIdentType{
	Unknown = 0,
	Bcp47,
}

/// 可跨平台傳遞的標準化語言身份。
/// `Code` 在 BCP 47 模式下保存 `zh-CN`、`en-US`、`ja-JP` 等完整語言標籤。
public interface INormLang{
	/// 語言代碼採用的標識標準。
	public ELangIdentType Type{get;set;}

	/// 對應標準下的語言代碼。
	public str Code{get;set;}
}

/// 帶顯示資料的標準化語言。
/// 前端可同時展示母語名稱與英文名稱，不必在 View 中自行維護語言名稱映射。
public interface INormLangDetail : INormLang{
	/// 語言使用者通常看到的本地名稱，例如 `中文（中國）`。
	public str NativeName{get;set;}

	/// 語言的英文名稱，例如 `Chinese (China)`。
	public str EnglishName{get;set;}

	/// 用於排列常用語言；數值越大時可越靠前展示。
	public f64 Weight{get;set;}
}

/// 標準化語言身份的默認資料載體。
public class NormLang : INormLang{
	/// 默認採用語音服務普遍支持的 BCP 47 標籤。
	public ELangIdentType Type{get;set;} = ELangIdentType.Bcp47;

	/// 語言代碼；空字串表示尚未指定。
	public str Code{get;set;} = "";
}

/// 供語言列表與語音輸入頁展示的完整語言資料。
public class NormLangDetail : NormLang, INormLangDetail{
	/// 語言的本地名稱。
	public str NativeName{get;set;} = "";

	/// 語言的英文名稱。
	public str EnglishName{get;set;} = "";

	/// 常用語言排序權重。
	public f64 Weight{get;set;} = 0;
}
