namespace Ngan.Ime.UI.Infra;

using Tsinswreng.CsCore;

[Doc($$"""
沒有鍵盤宿主能力的實現（Desktop 預覽、Browser、iOS 骨架）。

*為何要它*：骨架宿主以前不註冊 {{nameof(IKeyboardHost)}}，解析 `VmIme` 時直接拋例外——
「構建綠、運行必炸」。給一個明確的 no-op 實現，界面可以起來、上屏按鈕依 {{nameof(CanCommitText)}}
自動禁用，降級是可見的而不是崩潰。
""")]
public class NoopKeyboardHost : IKeyboardHost{
	[Doc("不支援把文字提交到外部輸入目標，故上屏按鈕禁用。")]
	public bool CanCommitText => false;

	[Doc("沒有真正的鍵盤可隱藏，故什麼都不做。")]
	public void HideKeyboard(){ }

	[Doc("沒有輸入連接，提交文字無處可去，故丟棄；呼叫方已由 CanCommitText 禁用入口。")]
	public void CommitText(str Text){ }
}

[Doc("沒有剪貼板能力的實現：回空清單，界面顯示空白頁而不是崩潰。")]
public class NoopClipboardService : IClipboardService{
	[Doc("回空清單。")]
	public Task<IReadOnlyList<str>> GetItemsAsy(CT Ct = default){
		return Task.FromResult<IReadOnlyList<str>>([]);
	}
}
