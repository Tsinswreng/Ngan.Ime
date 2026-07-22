namespace Avalime.UI.Views.VoiceInput;

/// 一次流式語音識別文本更新的 UI 邊界模型。
/// 將“已穩定、不應再被供應商改寫的文本”與“下一包可能替換的臨時文本”分開，
/// 避免簡單追加每個響應而產生重複句子，也為未來替換不同識別 Provider 保持統一語義。
public class VoiceRecognitionTextUpdate{
	/// 本次會話至今已確認穩定的完整文本快照。
	/// Provider 應傳完整快照而非僅傳增量，讓 UI 可在斷線重連或漏包後直接恢復一致狀態。
	public str FinalizedText{get;set;} = "";

	/// 當前尚未確定的臨時尾部文本。
	/// 後續更新可以整段替換此值；當一句話確定後，Provider 將其移入 `FinalizedText` 並清空此值。
	public str InterimText{get;set;} = "";

	/// 此更新是否代表服務端已完成整個識別會話。
	/// 收到完成更新後，VM 可從 `Finalizing` 進入 `Completed`，並重新允許人工編輯與上屏。
	public bool IsCompleted{get;set;} = false;
}
