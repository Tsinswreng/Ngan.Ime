using Avalime.ViewModels;

namespace Avalime.UI.Views.VoiceInput;

/// 語音輸入頁的 ViewModel 聲明。
/// 目前先只承擔前端狀態，不直接綁平台錄音實現。
public partial class VmVoiceInput : ViewModelBase
	, IMk<VmVoiceInput>
{
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
	} = "";

	/// 語音頁 VM 的兜底創建路徑。
	public static partial VmVoiceInput Mk();

	/// 切換錄音狀態。
	public partial void ToggleRecording();

	/// 主動停止錄音，用於退出語音頁時收斂狀態。
	public partial void StopRecording();
}
