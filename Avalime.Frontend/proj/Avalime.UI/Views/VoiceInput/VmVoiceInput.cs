using Avalime.ViewModels;
using Avalime.Core.Language;
using Avalime.Core.Infra;

namespace Avalime.UI.Views.VoiceInput;

/// 語音輸入頁的 ViewModel 聲明。
/// 目前先只承擔前端狀態，不直接綁平台錄音實現。
public partial class VmVoiceInput : ViewModelBase
	, IMk<VmVoiceInput>
{
	/// 當前語音識別語言。
	/// 本輪只保留前端資料入口，不實現選擇、持久化或向語音服務同步的流程。
	public NormLangDetail CurrentLang{
		get;
		set{SetProperty(ref field, value);}
	} = new(){
		Code = "zh-CN",
		NativeName = "中文（中國）",
		EnglishName = "Chinese (China)",
	};

	/// 當前語言按鈕的單行顯示文字。
	/// 後續實現可組合 BCP 47 代碼與本地名稱，例如 `zh-CN 中文（中國）`。
	public str CurrentLangDisplay{
		get;
		set{SetProperty(ref field, value);}
	} = "zh-CN  中文（中國）";

	/// 錄音狀態。
	/// `false` 時按鈕顯示白色；`true` 時按鈕顯示 `UiCfg.MainColor`。
	public bool IsRecording{
		get;
		set{SetProperty(ref field, value);}
	}

	/// 語音頁中央提示文案。
	public str StatusText{
		get;
		set{SetProperty(ref field, value);}
	} = "點擊開始語音輸入";

	/// 語音頁 VM 的兜底創建路徑。
	public static partial VmVoiceInput Mk();

	/// 接收語言選擇頁返回的標準化語言。
	/// 本輪只聲明前端接縫，不更新任何語音服務狀態。
	public partial void ApplyLang(NormLangDetail Lang);

	/// 切換錄音狀態。
	public partial void ToggleRecording();

	/// 主動停止錄音，用於退出語音頁時收斂狀態。
	public partial void StopRecording();
}
