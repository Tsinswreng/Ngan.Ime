namespace Avalime.UI.Views.ToolBar;

/// 工具欄新增語音頁入口的實現。
public partial class VmToolBar{
	/// 在普通鍵盤與語音輸入前端之間切換。
	public partial void ToggleVoiceInput(){
		UiState.ToggleVoiceInput();
	}
}
