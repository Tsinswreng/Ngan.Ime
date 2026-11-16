namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

[Doc($$"""
看門狗：應用卡死時，把「它的 UI 執行緒停在誰身上」寫進日誌。
*為什麼非要有這個*：2026-11-15 兩次 VSCode 掛起（AppHang），我們的外殼在事發時都是閒着的，
日誌只能證明「不是我們正在執行」，證明不了「UI 執行緒卡在哪一行」。沒有那一行，
就只能靠猜，而猜已經浪費了十幾輪。

*做法*：每個載入本外殼的行程起一條背景執行緒，每秒做一次與 Windows 判定「無回應」
相同的事——對自己行程的前景視窗送 `WM_NULL` 並等 500ms；逾時就是掛起。
此時把該視窗的執行緒暫停、取一次 `CONTEXT`，把 `Rip` 反查成「模組名+位移」寫進日誌。
一行就足以分辨：卡在 `win32u`／`ntdll` 的等待、卡在 `msctf`、卡在我們 DLL、還是卡在 Chromium。

*為何只取一幀而不用 StackWalk64*：一幀是「現在停在哪」，多數情況已經足夠指認；
完整的堆疊回溯要在別人的行程裏拆 unwind 資訊，失敗模式多、風險大。
先把最便宜的那一幀拿到，不夠再加。

*安全性*：整個流程包在 try/catch 裏，只暫停／喚醒執行緒一次，不寫別人的記憶體；
取不到就記一筆失敗原因，絕不影響輸入。
""")]
internal static unsafe partial class NativeTsf {

	[Doc($$"""
	看門狗是否已啟動（0／1）；每個行程只啟動一次。
	""")]
	private static i32 WatchdogStarted;

	[Doc($$"""
	目前正在處理的虛擬鍵碼 + 1；0 表示閒置。只用於卡死時的自述。
	""")]
	private static i32 CurrentOpVk;

	[Doc($$"""
	目前這顆鍵的方向與來源：bit0 擡起、bit1 徵詢。
	""")]
	private static i32 CurrentOpFlags;

	[Doc($$"""
	目前這顆鍵開始處理的時間（{{nameof(System.Diagnostics.Stopwatch)}} 時間戳）。
	""")]
	private static i64 CurrentOpStart;

	[Doc($$"""
	最後一次按鍵回調結束的時間；用來回答「我們閒置多久了」。
	""")]
	private static i64 LastCallbackTs;

	[Doc($$"""
	OpenThread：可暫停／喚醒執行緒。
	""")]
	private const u32 ThreadSuspendResume = 0x0002;
	[Doc($$"""
	OpenThread：可讀取執行緒 context。
	""")]
	private const u32 ThreadGetContext = 0x0008;
	[Doc($$"""
	OpenThread：可查詢執行緒資訊。
	""")]
	private const u32 ThreadQueryInformation = 0x0040;
	[Doc($$"""
	SendMessageTimeout：同步等待。
	""")]
	private const u32 SmtoBlock = 0x0001;
	[Doc($$"""
	SendMessageTimeout：對方無回應就立刻返回。
	""")]
	private const u32 SmtoAbortIfHung = 0x0002;
	[Doc($$"""
	WM_NULL：不改變任何狀態的探測訊息。
	""")]
	private const u32 WmNull = 0x0000;
	[Doc($$"""
	`CONTEXT_FULL`（x64，`winnt.h`）：控制、整數、浮點暫存器。
	值 0x0010000B = `CONTEXT_AMD64`(0x00100000) | `CONTEXT_CONTROL`(0x1) |
	`CONTEXT_INTEGER`(0x2) | `CONTEXT_FLOATING_POINT`(0x8)。
	先前註釋寫「除錯暫存器全要」是錯的：那要再加上 `CONTEXT_DEBUG_REGISTERS`(0x10)，
	也就是 `CONTEXT_ALL` = 0x0010001F。我們只讀 Rip／Rsp／Rbp，屬整數暫存器，
	`CONTEXT_FULL` 就夠；名稱與註釋一起改正，免得下一個人以為拿得到除錯暫存器。
	""")]
	private const u32 ContextFullX64 = 0x0010000B;
	[Doc($$"""
	x64 CONTEXT 的實際大小（含對齊）；配置時多留一點。
	""")]
	private const i32 ContextBufferSize = 0x0500;
	[Doc($$"""
	x64 CONTEXT 內 ContextFlags 的位移。
	""")]
	private const i32 ContextFlagsOffset = 0x30;
	[Doc($$"""
	x64 CONTEXT 內 Rsp 的位移。
	""")]
	private const i32 ContextRspOffset = 0x98;
	[Doc($$"""
	x64 CONTEXT 內 Rbp 的位移。
	""")]
	private const i32 ContextRbpOffset = 0xA0;
	[Doc($$"""
	x64 CONTEXT 內 Rip 的位移。
	""")]
	private const i32 ContextRipOffset = 0xF8;
	[Doc($$"""
	偵測間隔（毫秒）。
	""")]
	private const i32 WatchdogIntervalMs = 1000;
	[Doc($$"""
	探測訊息的等待上限（毫秒）；與 Windows 判定無回應同一個量級。
	""")]
	private const u32 WatchdogProbeTimeoutMs = 500;

	[Doc($$"""
	啟動看門狗；重複呼叫無效。
	""")]
	private static void EnsureWatchdog() {
		if (Interlocked.CompareExchange(ref WatchdogStarted, 1, 0) != 0) {
			return;
		}
		try {
			var watcher = new Thread(WatchdogLoop) {
				IsBackground = true,
				Name = "NganIme.StallWatch",
			};
			watcher.Start();
			LogWarn("看門狗 已啟動 pid=" + Environment.ProcessId);
		} catch (Exception Exception) {
			LogError("看門狗 啟動失敗 " + Exception);
		}
	}

	[Doc($$"""
	每秒探測一次前景視窗；掛起就記一行 UI 執行緒的位置。
	""")]
	private static void WatchdogLoop() {
		var reported = false;
		var timeouts = 0;
		while (true) {
			try {
				Thread.Sleep(WatchdogIntervalMs);
				var hwnd = GetForegroundWindow();
				if (hwnd == IntPtr.Zero) {
					reported = false;
					timeouts = 0;
					continue;
				}
				var threadId = GetWindowThreadProcessId(hwnd, out var ownerPid);
				if (ownerPid != (u32)Environment.ProcessId) {
					// 前景不是本行程：這一刻的卡頓不關這個行程的事。
					reported = false;
					timeouts = 0;
					continue;
				}
				var answered = SendMessageTimeout(hwnd, WmNull, IntPtr.Zero, IntPtr.Zero, SmtoBlock | SmtoAbortIfHung, WatchdogProbeTimeoutMs, out _);
				if (answered != IntPtr.Zero) {
					reported = false;
					timeouts = 0;
					continue;
				}
				timeouts++;
				// 連續兩次才判定：一次逾時可能只是應用正在忙著算東西（開大檔、重繪），
				// 連續兩秒都不回 WM_NULL，才叫做掛起。
				if (timeouts < 2) {
					continue;
				}
				if (reported) {
					continue;
				}
				reported = true;
				LogError("STALL 前景視窗連續 " + timeouts + " 次沒有回應（每次 " + WatchdogProbeTimeoutMs + "ms） hwnd=" + hwnd.ToString("X") + " tid=" + threadId + " pid=" + Environment.ProcessId);
				LogOwnState();
				LogThreadLocation(threadId, "STALL");
			} catch (Exception Exception) {
				LogError("看門狗 EX " + Exception);
			}
		}
	}

	[Doc($$"""
	把「卡死那一刻我們在做什麼」寫一行。
	兩種結果的意義完全不同：
	+ 有心跳（`正在處理`）＝還卡在我們自己的回調裏，責任在這裏；
	+ 沒有心跳（`閒置`）＝我們最後一次回調早就交還了，卡的是應用自己，要往它身上找。
	這一行就是先前十幾輪一直缺的那句實話。
	""")]
	private static void LogOwnState() {
		try {
			var vk = Volatile.Read(ref CurrentOpVk);
			var last = Volatile.Read(ref LastCallbackTs);
			var sinceLastMs = last == 0 ? -1 : (i64)System.Diagnostics.Stopwatch.GetElapsedTime(last).TotalMilliseconds;
			if (vk == 0) {
				LogError("STALL 我們閒置：最後一次按鍵回調在 " + sinceLastMs + "ms 前就已結束（不是卡在我們身上）");
				return;
			}
			var flags = Volatile.Read(ref CurrentOpFlags);
			var start = Volatile.Read(ref CurrentOpStart);
			var insideMs = (i64)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
			LogError("STALL 我們正在處理 vk=0x" + (vk - 1).ToString("X2")
				+ ((flags & 1) != 0 ? " up" : " dn")
				+ ((flags & 2) != 0 ? "/test" : "/key")
				+ " 已 " + insideMs + "ms 還沒返回");
		} catch (Exception Exception) {
			LogError("LogOwnState EX " + Exception);
		}
	}

	[Doc($$"""
	暫停指定執行緒、取一次 context，把 Rip 寫成「模組+位移」。
	#Prm[{{nameof(ThreadId)}}][要觀察的執行緒。]
	#Prm[{{nameof(Why)}}][這次觀察的原因（只寫進日誌）。]
	""")]
	private static void LogThreadLocation(u32 ThreadId, str Why) {
		var handle = OpenThread(ThreadSuspendResume | ThreadGetContext | ThreadQueryInformation, false, ThreadId);
		if (handle == IntPtr.Zero) {
			LogError(Why + " OpenThread 失敗 err=" + Marshal.GetLastWin32Error());
			return;
		}
		var buffer = (u8*)NativeMemory.Alloc(ContextBufferSize);
		// 只有這幾個純量在「執行緒被暫停」的期間取得；任何配置記憶體、寫日誌的動作都留到恢復之後。
		// 理由是 GC：被我們暫停的執行緒如果不回應 GC 的 suspend 請求，GC 会在這裏等死。
		u64 Rip = 0;
		u64 Rsp = 0;
		u64 Rbp = 0;
		var haveContext = false;
		var failError = 0;
		var failStep = "";
		try {
			var previous = SuspendThread(handle);
			if (previous == u32.MaxValue) {
				LogError(Why + " SuspendThread 失敗 err=" + Marshal.GetLastWin32Error());
				return;
			}
			try {
				*(u32*)(buffer + ContextFlagsOffset) = ContextFullX64;
				if (GetThreadContext(handle, (IntPtr)buffer) == 0) {
					failStep = "GetThreadContext";
					failError = Marshal.GetLastWin32Error();
				} else {
					Rip = *(u64*)(buffer + ContextRipOffset);
					Rsp = *(u64*)(buffer + ContextRspOffset);
					Rbp = *(u64*)(buffer + ContextRbpOffset);
					haveContext = true;
				}
			} finally {
				ResumeThread(handle);
			}
		} finally {
			NativeMemory.Free(buffer);
			CloseHandle(handle);
		}
		if (!haveContext) {
			LogError(Why + " " + failStep + " 失敗 err=" + failError);
			return;
		}
		LogError(Why + " UI執行緒 rip=" + Rip.ToString("X")
			+ " rsp=" + Rsp.ToString("X")
			+ " rbp=" + Rbp.ToString("X")
			+ " 位置=" + DescribeAddress(Rip));
	}

	[Doc($$"""
	把位址寫成「模組檔名+位移」；反查不到就寫 unknown。
	#Prm[{{nameof(Address)}}][要反查的位址。]
	#Rtn[可讀的位置描述。]
	""")]
	private static str DescribeAddress(u64 Address) {
		try {
			if (Address == 0) {
				return "0";
			}
			if (!GetModuleHandleEx(ModuleHandleFromAddress | ModuleHandleUnchangedRefCount, (IntPtr)(nint)Address, out var module) || module == IntPtr.Zero) {
				return "unknown+0x" + Address.ToString("X");
			}
			var name = new StringBuilder(1024);
			var length = GetModuleFileName(module, name, (u32)name.Capacity);
			var file = length > 0 ? name.ToString(0, (i32)length) : "?";
			var offset = Address - (u64)module.ToInt64();
			return file + "+0x" + offset.ToString("X");
		} catch (Exception Exception) {
			return "describe-failed " + Exception.GetType().Name;
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenThread(u32 DesiredAccess, bool InheritHandle, u32 ThreadId);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern u32 SuspendThread(IntPtr Thread);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern u32 ResumeThread(IntPtr Thread);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(IntPtr Handle);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern i32 GetThreadContext(IntPtr Thread, IntPtr Context);

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern IntPtr SendMessageTimeout(IntPtr Hwnd, u32 Msg, IntPtr WParam, IntPtr LParam, u32 Flags, u32 Timeout, out IntPtr Result);
}
