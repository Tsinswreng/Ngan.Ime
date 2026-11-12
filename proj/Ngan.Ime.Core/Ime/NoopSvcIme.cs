namespace Ngan.Ime.Core.Ime;

using Tsinswreng.CsCore;

[Doc($$"""
沒有引擎的輸入法實現：狀態永遠是「已連接、無組字、無候選」。

*用途*：骨架宿主（Desktop 預覽、Browser、iOS、測試）需要一棵能起來、能互動、
但不接任何引擎的界面。舊版這些宿主不註冊引擎服務，解析 `VmIme` 時直接拋——
「構建綠、運行必炸」。給一個明確的空引擎，降級是可見的。

*行為*：按鍵照收（不產生任何結果），選項切換只翻狀態位，連接一次即成功。
故工具欄、候選欄、preedit 都能正常顯示，只是永遠沒有內容。

#Eg[
	```cs
	Svc.AddSingleton<SvcImeBase, NoopSvcIme>();
	```
]
""")]
public sealed class NoopSvcIme : SvcImeBase{

	[Doc("建立空引擎；不啟動任何背景工作。")]
	public NoopSvcIme(){}

	[Doc("連接永遠成功：本宿主沒有引擎可連。")]
	public override async Task ConnectAsy(CT Ct = default){
		IsConnected = true;
		await Task.CompletedTask;
		RaiseConnectCompleted(true);
	}
}
