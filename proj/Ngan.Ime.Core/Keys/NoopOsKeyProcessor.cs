namespace Ngan.Ime.Core.Keys;

using Tsinswreng.CsCore;

[Doc($$"""
什麼都不做的 {{nameof(IOsKeyProcessor)}}。

*用在哪*：TSF 外殼就是「宿主應用」本身——引擎未處理的按鍵只要回報「沒吃掉」，
系統自然會把按鍵交給前景應用。此時若再用 `keybd_event` 補發一次，按鍵會重新進入
TSF，形成「同一顆鍵無限迴圈」（實測：按一次 Enter，之後每秒自動重複 Enter）。

故 TSF 模式（`--tsf`）一律裝這個空實現，把「還給系統」的責任交回 TSF 外殼。

#Eg[
	```cs
	Svc.AddSingleton<IOsKeyProcessor, NoopOsKeyProcessor>();
	```
]
""")]
public sealed class NoopOsKeyProcessor : IOsKeyProcessor{

	[Doc("錯誤事件；本實現永不觸發。")]
	public event ErrHandler? OnErr;

	[Doc("直接返回：未處理按鍵交給呼叫方（TSF 外殼）決定。")]
	public Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> KeyEvents, CT Ct){
		return Task.FromResult<IRespOnKeyEvent>(new RespOnKeyEvent());
	}
}
