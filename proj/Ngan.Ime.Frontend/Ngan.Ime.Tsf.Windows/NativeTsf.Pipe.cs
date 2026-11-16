namespace Ngan.Ime.Tsf.Windows;

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Ngan.Ime.Tsf.Ipc;

/// <summary>
/// 外殼與服務端之間的管道客戶端：查一次按鍵、送一條不等待回應的命令。
/// </summary>
/// <remarks>
/// *為何長連線*：一顆鍵要問一次、擡起再問一次，每顆鍵都重新 Connect 會撞上服務端換連線的空窗
/// （實測每兩顆鍵掉一次）。故連一次、之後每顆鍵復用，斷了才重連。
///
/// *為何有逾時*：這段程式跑在應用行程的 UI 執行緒上（TSF 的 OnTestKeyDown 是同步契約）。
/// 服務端若卡住，沒有逾時就等於把使用者的應用一起卡死。
/// 連線與讀取都給上限，逾時就放棄這顆鍵、由系統交給應用。
///
/// *為何只回一個可空結果*：呼叫端只需要「吃掉與否、組字、候選、上屏」；
/// 服務端不在、回應壞掉、逾時，一律回 null，由呼叫端放行按鍵。
///
/// *為何管道名要按安裝目錄算*：每次安裝都是一個獨立輸入法（各自的 CLSID、profile、服務端）。
/// 若共用一條固定名稱的管道，兩個版本的服務端會同時在同一條名稱上監聽
/// （具名管道允許多個同名 server 實例），客戶端連到哪一個由系統決定——舊版外殼就可能
/// 吃到新版的引擎狀態。名稱由 <see cref="TsfIpc.PipeNameFor"/> 依本 DLL 所在目錄算出，
/// 服務端從自己的目錄算出同一個名稱。
/// </remarks>
internal static unsafe partial class NativeTsf {

	/// <summary>連線建立的逾時（毫秒）；正常情況下服務端已在監聽，這個值只是保底。</summary>
	private const int ConnectTimeoutMs = 150;

	/// <summary>
	/// 讀取回應的逾時（毫秒）。
	/// </summary>
	/// <remarks>
	/// *為何是 25 而不是 300*：2026-11-15 深夜的實測——服務端處理一顆鍵 0.05～0.37ms，
	/// 但外殼這一側有幾顆鍵卡了 310ms。310 不是服務端慢，是這個預算被用完：
	/// 等滿 300ms、判定逾時、丟連線、重連、把同一顆鍵**再送一次**。
	/// 等於每一顆異常的鍵都讓應用的 UI 執行緒白等 0.3 秒，還讓引擎把同一顆鍵處理兩次
	/// （服務端日誌看得到多出來的那一顆）。
	/// 預算收到 25ms：正常往返 0.1～1ms，留二十倍餘裕；異常時最壞只卡 25ms。
	/// </remarks>
	private const int ReadTimeoutMs = 25;

	/// <summary>逾時之後的短冷卻（毫秒）：服務端還活著，只是這一顆沒等到，先讓它喘一口氣。</summary>
	private const int TimeoutBackoffMs = 200;

	/// <summary>剛連上的第一顆鍵用這個預算（毫秒）：服務端可能正在暖機。</summary>
	private const int ConnectGraceMs = 250;

	/// <summary>超過這個耗時就記一行「這一顆問得慢」，用來抓偶發的管道異常。</summary>
	private const double SlowQueryMs = 5;

	/// <summary>
	/// 服務端失聯後的冷卻期（毫秒）：這段時間內直接放棄查詢，不再嘗試連線。
	/// </summary>
	/// <remarks>
	/// *為何需要*：這段程式跑在應用行程的 UI 執行緒上。服務端一旦死掉，每顆鍵都要付
	/// <see cref="ConnectTimeoutMs"/> 的連線逾時，連按十六顆鍵就是一秒以上的卡頓
	/// （AltNav 這類連發按鍵的工具尤其明顯）。冷卻期內直接回 null、把按鍵交給應用，
	/// 逾時後再試一次。
	/// </remarks>
	private const int FailureBackoffMs = 2000;

	/// <summary>冷卻期截止的 Environment.TickCount64；小於等於目前時間表示可以重試。</summary>
	private static long RetryAfterTick;

	/// <summary>序列化存取管道；多個應用行程各有一份，行程內只有 UI 執行緒會用。</summary>
	private static readonly object PipeLock = new();

	/// <summary>
	/// 本安裝專屬的管道名稱；第一次使用時由本 DLL 的路徑算出。
	/// </summary>
	/// <remarks>
	/// 外殼與服務端永遠同層（同一次發布的產物），故兩邊各自從自己的目錄算出同一個名稱。
	/// 延遲計算：靜態初始化時 `GetOwnModulePath` 需要 kernel32 已就緒，第一次按鍵時算最安全。
	/// </remarks>
	private static string? PipeNameField;

	/// <summary>目前的長連線；null 表示尚未連上或剛被重設。</summary>
	private static NamedPipeClientStream? Pipe;
	private static StreamReader? PipeReader;
	private static StreamWriter? PipeWriter;

	/// <summary>
	/// 這一次呼叫是不是剛建立連線。
	/// </summary>
	/// <remarks>
	/// 剛連上的第一顆鍵可能撞上服務端正在暖機（載入 Rime、建視窗），
	/// 給它一個較寬的預算；之後每一顆都用 <see cref="ReadTimeoutMs"/>。
	/// 否則冷啟動的第一顆鍵會被 25ms 的預算誤殺。
	/// </remarks>
	private static bool ConnectedJustNow;

	/// <summary>丟掉目前的連線與讀寫器；之後的呼叫會重連。</summary>
	private static void ResetPipe() {
		try { PipeWriter?.Dispose(); } catch { }
		try { PipeReader?.Dispose(); } catch { }
		try { Pipe?.Dispose(); } catch { }
		PipeWriter = null;
		PipeReader = null;
		Pipe = null;
	}

	/// <summary>確保連線可用；連不上回 false，不拋。</summary>
	private static bool EnsureConnected() {
		ConnectedJustNow = false;
		if (Pipe is not null && Pipe.IsConnected) {
			return true;
		}
		ResetPipe();
		try {
			// CurrentUserOnly：只有同一個使用者的行程能連上。少了它，同機任何行程都能
			// 送出「按鍵」請求，而回應裏的 commit 會被本殼寫進前景應用的文件。
			var Client = new NamedPipeClientStream(".", GetPipeName(), PipeDirection.InOut, PipeOptions.CurrentUserOnly);
			Client.Connect(ConnectTimeoutMs);
			Pipe = Client;
			PipeWriter = new StreamWriter(Client, new UTF8Encoding(false)) { AutoFlush = true };
			PipeReader = new StreamReader(Client, Encoding.UTF8);
			ConnectedJustNow = true;
			return true;
		}
		catch (Exception Exception) {
			LogWarn("connect failed " + Exception.GetType().Name);
			ResetPipe();
			return false;
		}
	}

	/// <summary>
	/// 把一顆按鍵送給服務端並取回結果。
	/// </summary>
	/// <param name="VirtualKey">Win32 虛擬鍵碼。</param>
	/// <param name="Mods">修飾鍵位；0 表示沒有。</param>
	/// <param name="X">插入點螢幕 X，供候選窗定位。</param>
	/// <param name="Y">插入點螢幕 Y。</param>
	/// <param name="IsUp">true 表示擡起。</param>
	/// <param name="Scan">掃描碼；分左右 Shift 用。</param>
	/// <param name="Ext">擴展鍵位。</param>
	/// <returns>服務端的回應；連不上、逾時或回應壞掉時為 null（呼叫端放行這顆鍵）。</returns>
	/// <remarks>
	/// *逾時之後絕對不重送*：請求一旦寫進管道，服務端就可能已經處理過它。
	/// 先前這支在逾時後會重連並**再送一次同一顆鍵**，服務端日誌裏就看得到多出來的那一顆
	/// （四顆按下卻有五顆擡起）。重送的上屏鍵會讓引擎上屏兩次，修飾鍵則會讓應用收到
	/// 一組重複的按下／擡起。現在：寫失敗 → 這顆鍵沒送出去，重連是安全的；
	/// 讀逾時 → 這顆鍵送出去了，直接放行、丟連線、下一顆再重連。
	///
	/// *為什麼要記三個時戳*：寫出、寫完、讀完。服務端只記它自己處理的那 0.1～0.4ms，
	/// 先前出現 310ms 時分不出是「我們寫出去到它讀到」還是「它寫回到我們讀到」。
	/// 三段分開記，下次一眼就知道延遲在誰身上。
	/// </remarks>
	private static TsfReply? QueryKey(int VirtualKey, int Mods, int X, int Y, bool IsUp, int Scan, int Ext) {
		lock (PipeLock) {
			// 服務端剛失敗過就先不試：這段路徑在 UI 執行緒上，等不起。
			if (Environment.TickCount64 < RetryAfterTick) {
				return null;
			}
			if (!EnsureConnected()) {
				MarkUnreachable();
				return null;
			}
			var Req = new TsfKeyRequest {
				VirtualKey = VirtualKey,
				Mods = Mods,
				X = X,
				Y = Y,
				IsUp = IsUp,
				Scan = Scan,
				Ext = Ext,
			};
			var WriteStartTs = System.Diagnostics.Stopwatch.GetTimestamp();
			try {
				PipeWriter!.WriteLine(Req.Format());
			}
			catch (Exception Exception) {
				// 沒寫出去：這顆鍵沒有被服務端看到，重連之後再問一次不會重複處理。
				LogWarn("query write failed vk=0x" + VirtualKey.ToString("X2") + " " + Exception.GetType().Name);
				ResetPipe();
				MarkUnreachable();
				return null;
			}
			var WriteDoneTs = System.Diagnostics.Stopwatch.GetTimestamp();
			var Budget = ConnectedJustNow ? ConnectGraceMs : ReadTimeoutMs;
			var Line = ReadLineWithTimeout(Budget);
			var ReadDoneTs = System.Diagnostics.Stopwatch.GetTimestamp();
			var WriteMs = System.Diagnostics.Stopwatch.GetElapsedTime(WriteStartTs, WriteDoneTs).TotalMilliseconds;
			var ReadMs = System.Diagnostics.Stopwatch.GetElapsedTime(WriteDoneTs, ReadDoneTs).TotalMilliseconds;
			if (Line is null) {
				// 送出去了但沒等到回應：不重送，直接放行。
				LogWarn("query timeout vk=0x" + VirtualKey.ToString("X2") + (IsUp ? " up" : " dn")
					+ " write=" + WriteMs.ToString("0.00") + "ms read=" + ReadMs.ToString("0.00") + "ms（已送出，不重送）");
				ResetPipe();
				RetryAfterTick = Environment.TickCount64 + TimeoutBackoffMs;
				return null;
			}
			if (WriteMs + ReadMs > SlowQueryMs) {
				LogWarn("query slow vk=0x" + VirtualKey.ToString("X2") + (IsUp ? " up" : " dn")
					+ " write=" + WriteMs.ToString("0.00") + "ms read=" + ReadMs.ToString("0.00") + "ms");
			}
			RetryAfterTick = 0;
			return TsfReply.Parse(Line);
		}
	}

	/// <summary>連不上服務端：冷卻一段時間，並嘗試把它拉起來。</summary>
	private static void MarkUnreachable() {
		RetryAfterTick = Environment.TickCount64 + FailureBackoffMs;
		LogWarn("server unreachable; backoff " + FailureBackoffMs + "ms");
		TryStartServer();
	}

	/// <summary>上次嘗試拉起服務端的時間（TickCount64）；用來節流，避免每顆鍵都開一個行程。</summary>
	private static long LastStartAttemptTick;

	/// <summary>兩次拉起服務端之間的最小間隔。</summary>
	private const int StartServerCooldownMs = 30000;

	/// <summary>
	/// 服務端不在時，嘗試把它拉起來（與 Weasel 的 on-demand start 同一個思路）。
	/// </summary>
	/// <remarks>
	/// *為何要這樣*：服務端崩掉之後沒有任何東西會重啟它，使用者看到的現象是「輸入法突然完全沒了」，
	/// 而且要自己去按啟動器才會好。外殼就在安裝目錄裡，服務端是它的兄弟檔案，直接開一次即可。
	/// 失敗（檔案不在、被政策擋住）就什麼都不做，下次按鍵再說；這裡絕不拋例外，
	/// 因為呼叫端是應用行程的 UI 執行緒。
	///
	/// *為何要一個具名互斥*：行程內的節流只保護自己。服務端崩掉時，幾十個各自載入本外殼的
	/// 應用會在同一個瞬間同時嘗試拉起它——那正好是最不該發生的行為。互斥鎖跨行程，
	/// 只有第一個搶到的行程會真的啟動，其餘直接返回。
	///
	/// *拉起的正確位置終究是排程工作*：這裡只是「使用者正在打字，等不及」的保底。
	/// 服務端自己的常駐（登入自啟＋失敗重啟）由安裝腳本建立的排程工作負責。
	/// </remarks>
	private static void TryStartServer() {
		var Now = Environment.TickCount64;
		if (Now - LastStartAttemptTick < StartServerCooldownMs) {
			return;
		}
		LastStartAttemptTick = Now;
		try {
			var Dir = Path.GetDirectoryName(GetOwnModulePath());
			if (string.IsNullOrEmpty(Dir)) {
				return;
			}
			var Exe = Path.Combine(Dir, "Ngan.Ime.Windows.exe");
			if (!File.Exists(Exe)) {
				return;
			}
			// 每個安裝目錄一個互斥鎖：不同版本互不阻塞。
			var MutexName = "Local\\NganIme.Server.Start." + TsfIpc.PipeNameFor(Dir).Replace("NganIme.Tsf.v1.", "");
			using var StartGate = new System.Threading.Mutex(false, MutexName);
			if (!StartGate.WaitOne(0)) {
				Log("another process is already starting the server");
				return;
			}
			try {
				var Info = new System.Diagnostics.ProcessStartInfo(Exe) {
					Arguments = "--tsf",
					WorkingDirectory = Dir,
					UseShellExecute = false,
					CreateNoWindow = true,
				};
				System.Diagnostics.Process.Start(Info);
				LogWarn("server was not running; started it from " + Dir);
			}
			finally {
				StartGate.ReleaseMutex();
			}
		}
		catch (Exception Exception) {
			LogWarn("start server failed " + Exception.GetType().Name);
		}
	}

	/// <summary>本安裝專屬的管道名稱；第一次使用時算出並記住。</summary>
	private static string GetPipeName() {
		var Cached = PipeNameField;
		if (Cached is not null) {
			return Cached;
		}
		var Dir = Path.GetDirectoryName(GetOwnModulePath());
		if (string.IsNullOrEmpty(Dir)) {
			// 取不到自己的路徑：退回前綴。此時只可能連到「同一個前綴」的服務端，
			// 不至於完全不能用，也不會誤連到另一個版本（那個版本的名稱帶雜湊）。
			Dir = "";
		}
		Cached = TsfIpc.PipeNameFor(Dir);
		PipeNameField = Cached;
		return Cached;
	}

	/// <summary>取得本外殼 DLL 的完整路徑；NativeAOT 下不能用組件位置。</summary>
	private static string GetOwnModulePath() {
		var Handle = IntPtr.Zero;
		if (GetModuleHandleEx(ModuleHandleFromAddress | ModuleHandleUnchangedRefCount, GetOwnModuleAnchor(), out Handle) && Handle != IntPtr.Zero) {
			var Buffer = new StringBuilder(1024);
			var Length = GetModuleFileName(Handle, Buffer, (uint)Buffer.Capacity);
			if (Length > 0) {
				return Buffer.ToString(0, (int)Length);
			}
		}
		return string.Empty;
	}

	/// <summary>取一個「位在本模組裡」的函式位址，供 GetModuleHandleEx 反查模組。</summary>
	private static IntPtr GetOwnModuleAnchor() {
		delegate* unmanaged<IntPtr> Anchor = &MarkerForModuleLookup;
		return (IntPtr)Anchor;
	}

	[System.Runtime.InteropServices.UnmanagedCallersOnly]
	private static IntPtr MarkerForModuleLookup() => IntPtr.Zero;

	private const uint ModuleHandleFromAddress = 0x00000004;
	private const uint ModuleHandleUnchangedRefCount = 0x00000001;

	[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetModuleHandleEx(uint Flags, IntPtr ModuleName, out IntPtr Module);

	[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
	private static extern uint GetModuleFileName(IntPtr Module, StringBuilder FileName, uint Size);

	/// <summary>
	/// 在逾時內讀一行；逾時回 null，由呼叫端丟掉連線。
	/// </summary>
	/// <remarks>
	/// StreamReader 沒有可用的讀取逾時（NamedPipeClientStream 的 ReadTimeout 不支援），
	/// 故以「非同步讀 ＋ 有界等待」實現。
	///
	/// *逾時之後為什麼不再等它*：先前會在逾時後再等 200ms 讓那個讀取任務收尾——
	/// 等於把 300ms 的預算變成 500ms。現在只是掛一個續接把例外吃掉，然後立刻返回：
	/// 這條連線接下來就會被 Dispose，讀取任務自然以例外收場，不需要我們在這裏等。
	///
	/// *注意*：`StreamReader.ReadLineAsync(ct)` 的取消不一定能真正中止底層讀取，
	/// 所以絕對不能在同一條連線上繼續讀下一行——這也是呼叫端逾時後一定要重連的原因。
	/// </remarks>
	private static string? ReadLineWithTimeout(int BudgetMs) {
		using var Cts = new System.Threading.CancellationTokenSource(BudgetMs);
		try {
			var ReadTask = PipeReader!.ReadLineAsync(Cts.Token).AsTask();
			if (!ReadTask.Wait(BudgetMs)) {
				Cts.Cancel();
				// 不等它，只把未觀察的例外收掉；連線馬上會被丟掉。
				_ = ReadTask.ContinueWith(
					static Done => { _ = Done.Exception; },
					System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted | System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
					System.Threading.Tasks.TaskScheduler.Default);
				return null;
			}
			return ReadTask.GetAwaiter().GetResult();
		}
		catch (OperationCanceledException) {
			// 逾時：交給呼叫端重連。
			return null;
		}
		catch (AggregateException Exception) when (Exception.InnerException is OperationCanceledException) {
			return null;
		}
	}

	/// <summary>
	/// 送一條不需要結果的命令（目前只有收起候選窗），並把服務端的回應讀掉。
	/// </summary>
	/// <remarks>
	/// 一定要讀回應：服務端每條請求都回一行，若不讀就關閉連線，
	/// 服務端會看到「客戶端斷線」而記一筆錯誤日誌。
	/// </remarks>
	private static void SendCommand(TsfKeyRequest Req) {
		lock (PipeLock) {
			if (!EnsureConnected()) {
				return;
			}
			try {
				PipeWriter!.WriteLine(Req.Format());
				if (ReadLineWithTimeout(ReadTimeoutMs) is null) {
					// 沒讀到回應：這條連線上還掛著一個沒收的回應，留著會讓下一顆鍵
					// 讀到上一個請求的答案（請求與回應錯位）。直接丟掉。
					LogWarn("command timeout; dropping connection");
					ResetPipe();
				}
			}
			catch (Exception Exception) {
				Log("command failed " + Exception.GetType().Name);
				ResetPipe();
			}
		}
	}

	/// <summary>請服務端收起候選窗。</summary>
	private static void SendHide() {
		SendCommand(new TsfKeyRequest { Kind = ETsfReqKind.Hide });
	}
}
