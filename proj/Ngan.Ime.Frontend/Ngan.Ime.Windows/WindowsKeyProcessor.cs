namespace Ngan.Ime.Windows;

using Ngan.Ime.Core.Keys;
using Tsinswreng.CsCore;

[Doc($$"""
Windows 的「未處理按鍵 → 作業系統」轉發器。

*為何是系統級注入*：Rime 未處理的鍵（例如 ASCII 模式下的 `a`）必須由宿主補發，
否則使用者打什麼都不會上屏。本類用 `keybd_event` 把鍵送給當前前臺視窗。

*已知限制*（保留並記錄）：`keybd_event` 對以管理員權限執行的視窗無效（UIPI），
且無法區分「哪個視窗該收鍵」。正式版應改走 TSF 的合成路徑；
現階段桌面預覽與非提權場景可用。

#Eg[
	```cs
	await Processor.OnKeyEvents(Unhandled, Ct);
	```
]
""")]
public class WindowsKeyProcessor : IOsKeyProcessor{

	[Doc("錯誤事件；本實現目前不主動觸發，保留供宿主統一掛接。")]
	public event ErrHandler? OnErr;

	[Doc("把每個未處理按鍵原樣發給系統；已按鍵事件的按下／擡起狀態決定 `dwFlags`。")]
	public Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> KeyEvents, CT Ct){
		foreach(var One in KeyEvents){
			Ct.ThrowIfCancellationRequested();
			var Key = KeyEventConverter.Inst.Convert(One);
			KeySender.keybd_event(Key.Vk, Key.Scan, Key.Flags, Key.ExtraInfo);
		}
		return Task.FromResult<IRespOnKeyEvent>(new RespOnKeyEvent());
	}
}
