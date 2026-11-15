namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

/// <summary>
/// 看門狗：應用卡死時，把「它的 UI 執行緒停在誰身上」寫進日誌。
/// </summary>
/// <remarks>
/// *為什麼非要有這個*：2026-11-15 兩次 VSCode 掛起（AppHang），我們的外殼在事發時都是閒着的，
/// 日誌只能證明「不是我們正在執行」，證明不了「UI 執行緒卡在哪一行」。沒有那一行，
/// 就只能靠猜，而猜已經浪費了十幾輪。
///
/// *做法*：每個載入本外殼的行程起一條背景執行緒，每秒做一次與 Windows 判定「無回應」
/// 相同的事——對自己行程的前景視窗送 `WM_NULL` 並等 500ms；逾時就是掛起。
/// 此時把該視窗的執行緒暫停、取一次 `CONTEXT`，把 `Rip` 反查成「模組名+位移」寫進日誌。
/// 一行就足以分辨：卡在 `win32u`／`ntdll` 的等待、卡在 `msctf`、卡在我們 DLL、還是卡在 Chromium。
///
/// *為何只取一幀而不用 StackWalk64*：一幀是「現在停在哪」，多數情況已經足夠指認；
/// 完整的堆疊回溯要在別人的行程裏拆 unwind 資訊，失敗模式多、風險大。
/// 先把最便宜的那一幀拿到，不夠再加。
///
/// *安全性*：整個流程包在 try/catch 裏，只暫停／喚醒執行緒一次，不寫別人的記憶體；
/// 取不到就記一筆失敗原因，絕不影響輸入。
/// </remarks>
internal static unsafe partial class NativeTsf {

	/// <summary>看門狗是否已啟動（0／1）；每個行程只啟動一次。</summary>
	private static int WatchdogStarted;

	/// <summary>目前正在處理的虛擬鍵碼 + 1；0 表示閒置。只用於卡死時的自述。</summary>
	private static int CurrentOpVk;

	/// <summary>目前這顆鍵的方向與來源：bit0 擡起、bit1 徵詢。</summary>
	private static int CurrentOpFlags;

	/// <summary>目前這顆鍵開始處理的時間（<see cref="System.Diagnostics.Stopwatch"/> 時間戳）。</summary>
	private static long CurrentOpStart;

	/// <summary>最後一次按鍵回調結束的時間；用來回答「我們閒置多久了」。</summary>
	private static long LastCallbackTs;

	/// <summary>OpenThread：可暫停／喚醒執行緒。</summary>
	private const uint ThreadSuspendResume = 0x0002;
	/// <summary>OpenThread：可讀取執行緒 context。</summary>
	private const uint ThreadGetContext = 0x0008;
	/// <summary>OpenThread：可查詢執行緒資訊。</summary>
	private const uint ThreadQueryInformation = 0x0040;
	/// <summary>SendMessageTimeout：同步等待。</summary>
	private const uint SmtoBlock = 0x0001;
	/// <summary>SendMessageTimeout：對方無回應就立刻返回。</summary>
	private const uint SmtoAbortIfHung = 0x0002;
	/// <summary>WM_NULL：不改變任何狀態的探測訊息。</summary>
	private const uint WmNull = 0x0000;
	/// <summary>CONTEXT_ALL（x64）：控制、整數、浮點、除錯暫存器全要。</summary>
	private const uint ContextAllX64 = 0x0010000B;
	/// <summary>x64 CONTEXT 的實際大小（含對齊）；配置時多留一點。</summary>
	private const int ContextBufferSize = 0x0500;
	/// <summary>x64 CONTEXT 內 ContextFlags 的位移。</summary>
	private const int ContextFlagsOffset = 0x30;
	/// <summary>x64 CONTEXT 內 Rsp 的位移。</summary>
	private const int ContextRspOffset = 0x98;
	/// <summary>x64 CONTEXT 內 Rbp 的位移。</summary>
	private const int ContextRbpOffset = 0xA0;
	/// <summary>x64 CONTEXT 內 Rip 的位移。</summary>
	private const int ContextRipOffset = 0xF8;
	/// <summary>偵測間隔（毫秒）。</summary>
	private const int WatchdogIntervalMs = 1000;
	/// <summary>探測訊息的等待上限（毫秒）；與 Windows 判定無回應同一個量級。</summary>
	private const uint WatchdogProbeTimeoutMs = 500;

	/// <summary>啟動看門狗；重複呼叫無效。</summary>
	private static void EnsureWatchdog() {
		if (Interlocked.CompareExchange(ref WatchdogStarted, 1, 0) != 0) {
			return;
		}
		try {
			var Watcher = new Thread(WatchdogLoop) {
				IsBackground = true,
				Name = "NganIme.StallWatch",
			};
			Watcher.Start();
			LogWarn("看門狗 已啟動 pid=" + Environment.ProcessId);
		}
		catch (Exception Exception) {
			LogError("看門狗 啟動失敗 " + Exception);
		}
	}

	/// <summary>每秒探測一次前景視窗；掛起就記一行 UI 執行緒的位置。</summary>
	private static void WatchdogLoop() {
		var Reported = false;
		var Timeouts = 0;
		while (true) {
			try {
				Thread.Sleep(WatchdogIntervalMs);
				var Hwnd = GetForegroundWindow();
				if (Hwnd == IntPtr.Zero) {
					Reported = false;
					Timeouts = 0;
					continue;
				}
				var ThreadId = GetWindowThreadProcessId(Hwnd, out var OwnerPid);
				if (OwnerPid != (uint)Environment.ProcessId) {
					// 前景不是本行程：這一刻的卡頓不關這個行程的事。
					Reported = false;
					Timeouts = 0;
					continue;
				}
				var Answered = SendMessageTimeout(Hwnd, WmNull, IntPtr.Zero, IntPtr.Zero, SmtoBlock | SmtoAbortIfHung, WatchdogProbeTimeoutMs, out _);
				if (Answered != IntPtr.Zero) {
					Reported = false;
					Timeouts = 0;
					continue;
				}
				Timeouts++;
				// 連續兩次才判定：一次逾時可能只是應用正在忙著算東西（開大檔、重繪），
				// 連續兩秒都不回 WM_NULL，才叫做掛起。
				if (Timeouts < 2) {
					continue;
				}
				if (Reported) {
					continue;
				}
				Reported = true;
				LogError("STALL 前景視窗連續 " + Timeouts + " 次沒有回應（每次 " + WatchdogProbeTimeoutMs + "ms） hwnd=" + Hwnd.ToString("X") + " tid=" + ThreadId + " pid=" + Environment.ProcessId);
				LogOwnState();
				LogThreadLocation(ThreadId, "STALL");
			}
			catch (Exception Exception) {
				LogError("看門狗 EX " + Exception);
			}
		}
	}

	/// <summary>
	/// 把「卡死那一刻我們在做什麼」寫一行。
	/// </summary>
	/// <remarks>
	/// 兩種結果的意義完全不同：
	/// + 有心跳（`正在處理`）＝還卡在我們自己的回調裏，責任在這裏；
	/// + 沒有心跳（`閒置`）＝我們最後一次回調早就交還了，卡的是應用自己，要往它身上找。
	/// 這一行就是先前十幾輪一直缺的那句實話。
	/// </remarks>
	private static void LogOwnState() {
		try {
			var Vk = Volatile.Read(ref CurrentOpVk);
			var Last = Volatile.Read(ref LastCallbackTs);
			var SinceLastMs = Last == 0 ? -1 : (long)System.Diagnostics.Stopwatch.GetElapsedTime(Last).TotalMilliseconds;
			if (Vk == 0) {
				LogError("STALL 我們閒置：最後一次按鍵回調在 " + SinceLastMs + "ms 前就已結束（不是卡在我們身上）");
				return;
			}
			var Flags = Volatile.Read(ref CurrentOpFlags);
			var Start = Volatile.Read(ref CurrentOpStart);
			var InsideMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(Start).TotalMilliseconds;
			LogError("STALL 我們正在處理 vk=0x" + (Vk - 1).ToString("X2")
				+ ((Flags & 1) != 0 ? " up" : " dn")
				+ ((Flags & 2) != 0 ? "/test" : "/key")
				+ " 已 " + InsideMs + "ms 還沒返回");
		}
		catch (Exception Exception) {
			LogError("LogOwnState EX " + Exception);
		}
	}

	/// <summary>
	/// 暫停指定執行緒、取一次 context，把 Rip 寫成「模組+位移」。
	/// </summary>
	/// <param name="ThreadId">要觀察的執行緒。</param>
	/// <param name="Why">這次觀察的原因（只寫進日誌）。</param>
	private static void LogThreadLocation(uint ThreadId, string Why) {
		var Handle = OpenThread(ThreadSuspendResume | ThreadGetContext | ThreadQueryInformation, false, ThreadId);
		if (Handle == IntPtr.Zero) {
			LogError(Why + " OpenThread 失敗 err=" + Marshal.GetLastWin32Error());
			return;
		}
		var Buffer = (byte*)NativeMemory.Alloc(ContextBufferSize);
		// 只有這幾個純量在「執行緒被暫停」的期間取得；任何配置記憶體、寫日誌的動作都留到恢復之後。
		// 理由是 GC：被我們暫停的執行緒如果不回應 GC 的 suspend 請求，GC 会在這裏等死。
		ulong Rip = 0;
		ulong Rsp = 0;
		ulong Rbp = 0;
		var HaveContext = false;
		var FailError = 0;
		var FailStep = "";
		try {
			var Previous = SuspendThread(Handle);
			if (Previous == unchecked((uint)-1)) {
				LogError(Why + " SuspendThread 失敗 err=" + Marshal.GetLastWin32Error());
				return;
			}
			try {
				*(uint*)(Buffer + ContextFlagsOffset) = ContextAllX64;
				if (GetThreadContext(Handle, (IntPtr)Buffer) == 0) {
					FailStep = "GetThreadContext";
					FailError = Marshal.GetLastWin32Error();
				}
				else {
					Rip = *(ulong*)(Buffer + ContextRipOffset);
					Rsp = *(ulong*)(Buffer + ContextRspOffset);
					Rbp = *(ulong*)(Buffer + ContextRbpOffset);
					HaveContext = true;
				}
			}
			finally {
				ResumeThread(Handle);
			}
		}
		finally {
			NativeMemory.Free(Buffer);
			CloseHandle(Handle);
		}
		if (!HaveContext) {
			LogError(Why + " " + FailStep + " 失敗 err=" + FailError);
			return;
		}
		LogError(Why + " UI執行緒 rip=" + Rip.ToString("X")
			+ " rsp=" + Rsp.ToString("X")
			+ " rbp=" + Rbp.ToString("X")
			+ " 位置=" + DescribeAddress(Rip));
	}

	/// <summary>把位址寫成「模組檔名+位移」；反查不到就寫 unknown。</summary>
	/// <param name="Address">要反查的位址。</param>
	/// <returns>可讀的位置描述。</returns>
	private static string DescribeAddress(ulong Address) {
		try {
			if (Address == 0) {
				return "0";
			}
			if (!GetModuleHandleEx(ModuleHandleFromAddress | ModuleHandleUnchangedRefCount, (IntPtr)(nint)Address, out var Module) || Module == IntPtr.Zero) {
				return "unknown+0x" + Address.ToString("X");
			}
			var Name = new StringBuilder(1024);
			var Length = GetModuleFileName(Module, Name, (uint)Name.Capacity);
			var File = Length > 0 ? Name.ToString(0, (int)Length) : "?";
			var Offset = Address - (ulong)Module.ToInt64();
			return File + "+0x" + Offset.ToString("X");
		}
		catch (Exception Exception) {
			return "describe-failed " + Exception.GetType().Name;
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenThread(uint DesiredAccess, bool InheritHandle, uint ThreadId);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern uint SuspendThread(IntPtr Thread);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern uint ResumeThread(IntPtr Thread);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(IntPtr Handle);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern int GetThreadContext(IntPtr Thread, IntPtr Context);

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern IntPtr SendMessageTimeout(IntPtr Hwnd, uint Msg, IntPtr WParam, IntPtr LParam, uint Flags, uint Timeout, out IntPtr Result);
}
