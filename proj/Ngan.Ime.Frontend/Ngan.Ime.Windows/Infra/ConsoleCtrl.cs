namespace Ngan.Ime.Windows.Infra;

using System;
using System.Runtime.InteropServices;
using Tsinswreng.CsCore;

[Doc($$"""
行程級的主控台控制事件處理。

*為何需要*：常駐服務端由排程工作以主控台程式啟動。那個視窗一旦被關掉（或承載它的
終端機關閉），行程會收到 `CTRL_CLOSE_EVENT` 並以 `0xC000013A` 結束——
沒有崩潰事件、沒有 dump、日誌停在最後一行，使用者看到的現象是「輸入法突然完全沒了」。

`SetConsoleCtrlHandler(NULL, TRUE)` 讓行程忽略這幾種事件並繼續活着。
只在常駐模式呼叫：互動模式（`RunWin`）仍要能用 Ctrl+C 結束。
""")]
public static partial class ConsoleCtrl{

	[Doc("忽略 Ctrl+C／Ctrl+Break／關閉視窗等控制事件；拿不到主控台時只印一行，不影響啟動。")]
	public static partial void IgnoreControlEvents();

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool SetConsoleCtrlHandler(IntPtr Handler, bool Add);
}
