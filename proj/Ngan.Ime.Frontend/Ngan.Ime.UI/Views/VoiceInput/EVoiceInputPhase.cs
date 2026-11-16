namespace Ngan.Ime.UI.Views.VoiceInput;


[Doc($$"""
語音輸入的前端階段。

*為何要有列舉*：按鈕可用性、草稿只讀、錄音高亮都由階段推出；
若用多個布爾表示（IsRecording、IsBusy、IsStarting…）就會出現互相矛盾的組合。
""")]
public enum EVoiceInputPhase{
	[Doc("空閒，可開始新一輪。")]
	Idle,
	[Doc("正在建立遠端會話與麥克風。")]
	Starting,
	[Doc("正在錄音並接收流式結果。")]
	Recording,
	[Doc("已停止採集，等待供應商最終結果。")]
	Finalizing,
	[Doc("識別完成，可修改或上屏。")]
	Completed,
	[Doc("識別失敗，錯誤已寫入 ErrorText。")]
	Failed,
}
