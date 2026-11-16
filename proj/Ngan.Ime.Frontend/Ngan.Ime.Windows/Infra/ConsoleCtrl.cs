namespace Ngan.Ime.Windows.Infra;

using System;
using System.Runtime.InteropServices;

[Doc($$"""
行程級的主控台控制事件處理。

*為何需要*：常駐服務端由排程工作以主控台程式啟動。那個主控台一旦被關掉（或承載它的
終端機關閉），行程會收到 `CTRL_CLOSE_EVENT` 並以 `0xC000013A` 結束——
沒有崩潰事件、沒有 dump、日誌停在最後一行，使用者看到的現象是「輸入法突然完全沒了」。

*為何只靠 `SetConsoleCtrlHandler` 不夠*：`Handler` 傳 `NULL`、`Add` 傳 `TRUE`
只忽略 `CTRL_C_EVENT` 與 `CTRL_BREAK_EVENT`；`CTRL_CLOSE_EVENT`／`LOGOFF`／`SHUTDOWN`
不受影響，而且處理器回 `TRUE` 也只代表「已處理」，系統仍會在處理器返回
（或約五秒逾時）之後結束行程。所以真正擋得住「關視窗」的是 {{nameof(Detach)}}：
把行程從主控台脫離，之後根本收不到這些事件。
Weasel 的服務端本來就是沒有主控台的 GUI 程式，這裡等價。

*互動模式仍要能 Ctrl+C*：`RunWin` 直接跑 exe 時不呼叫 {{nameof(Detach)}}，
`Ctrl+C` 照常結束。
""")]
public static partial class ConsoleCtrl{

	[Doc("把行程從主控台脫離；之後不再收到 CTRL_CLOSE／LOGOFF／SHUTDOWN／Ctrl+C。常駐模式呼叫。")]
	public static partial void Detach();

	[Doc("忽略 Ctrl+C 與 Ctrl+Break；拿不到主控台時不影響啟動。擋不住關閉視窗，見類註釋。")]
	public static partial void IgnoreControlEvents();

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool SetConsoleCtrlHandler(IntPtr Handler, bool Add);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool FreeConsole();
}
