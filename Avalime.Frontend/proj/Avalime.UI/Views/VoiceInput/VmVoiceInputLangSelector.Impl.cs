using Avalime.Core.Language;

namespace Avalime.UI.Views.VoiceInput;

/// 語音語言選擇頁的靜態前端資料實現。
/// 列表只用於畫出第一版界面，不代表最終語音引擎支持範圍。
public partial class VmVoiceInputLangSelector{
	/// 建立帶常用語言示例資料的選擇頁 ViewModel。
	public static partial VmVoiceInputLangSelector Mk(){
		return new VmVoiceInputLangSelector{
			Rows = [
				new NormLangDetail{Code = "zh-CN", NativeName = "中文（中國）", EnglishName = "Chinese (China)"},
				new NormLangDetail{Code = "zh-TW", NativeName = "中文（臺灣）", EnglishName = "Chinese (Taiwan)"},
				new NormLangDetail{Code = "en-US", NativeName = "English (United States)", EnglishName = "English (United States)"},
				new NormLangDetail{Code = "ja-JP", NativeName = "日本語（日本）", EnglishName = "Japanese (Japan)"},
				new NormLangDetail{Code = "yue-Hant-HK", NativeName = "粵語（香港）", EnglishName = "Cantonese (Hong Kong)"},
			],
		};
	}

	/// 搜索狀態尚未納入本輪，只保留按鈕的可調用入口。
	public partial void ClearSearch(){
	}

	/// 搜索狀態尚未納入本輪，只保留按鈕的可調用入口。
	public partial void Search(){
	}
}
