namespace Avalime.UI;

public interface IKeyboardHost
{
	/// 當前宿主是否能把文字提交到外部輸入目標。
	/// Android IME 有有效 `InputConnection` 時可支持；Windows 原型尚未接系統輸入法 API，應返回 `false`。
	/// UI 使用此能力禁用“上屏”按鈕，避免向用戶提供點擊後無任何效果的操作。
	public bool CanCommitText{get;}

	public void HideKeyboard();
	public void CommitText(str text);
}
