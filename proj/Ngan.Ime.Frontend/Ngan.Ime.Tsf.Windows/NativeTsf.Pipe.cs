namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Ngan.Ime.Tsf.Ipc;

/// <summary>
/// 外殼與服務端之間的管道客戶端。
/// </summary>
/// <remarks>
/// *這支照 Weasel 的 IPC 抄*，逐條對應如下（原始碼在 `E:\_code\_clone\rime\weasel`）：
///
/// + 一次交易就是「連線 → 送出 → 阻塞讀回」：Weasel `include/PipeChannel.h:92` 的
///   `Transact` 只有三行——`_Ensure()`、`_Send()`、`_ReceiveResponse()`。
///   **沒有逾時、沒有重送、沒有冷卻**：`WeaselIPC/PipeChannel.cpp:101` 的 `_Receive`
///   就是阻塞 `ReadFile`，等到有資料為止。
/// + 連線只在「無效」時才重建：`PipeChannelBase::_Ensure`。重建不會順手重送剛才那顆鍵。
/// + 每一顆鍵都先問一次「服務端還在嗎」：`WeaselTSF/WeaselTSF.cpp` 的 `_EnsureServerConnected`
///   呼叫 `m_client.Echo()`；成功就處理按鍵，失敗就重連。
/// + 連續六次失敗才拉起服務端，而且**在脫離的背景執行緒裡**拉起、等 500ms 再重連：
///   同一個 `_EnsureServerConnected`（`retry >= 6` 那段）。拉起用跨行程具名互斥去重，
///   對應它的 `WeaselDeployerExclusiveMutex`。
/// + 服務端不在時，按鍵照樣交還給應用（`*pfEaten = FALSE`），不是丟掉不問。
///
/// *與 Weasel 唯一的差別（傳輸編碼）*：Weasel 用 message-mode 管道傳固定結構（`_MsgSize`／`_ResSize`），
/// 我們用同一條管道傳「一行文字」。框架方式不同，但「一次交易、不重送、不逾時、不冷卻」的語義相同。
/// 這一點是本專案既有的協議（`TsfIpc`），不在這一輪改動範圍內。
///
/// *已知代價，與 Weasel 相同*：讀取是阻塞的，服務端若卡住，呼叫端（應用的 UI 執行緒）也一起等。
/// 先前版本為了避開這個而加了逾時與冷卻，結果是「逾時後丟鍵」，症狀比卡頓更糟；
/// 這裡選擇與 Weasel 一致：寧可等，不丟鍵。
/// </remarks>
internal static unsafe partial class NativeTsf {

	/// <summary>連線等待上限（毫秒）；對應 Weasel `_Connect` 裡的 `WaitNamedPipe(name, 500)`。</summary>
	private const int ConnectWaitMs = 500;

	/// <summary>拉起服務端之後等它開始監聽的時間（毫秒）；對應 Weasel 的 `sleep_for(500ms)`。</summary>
	private const int StartServerWaitMs = 500;

	/// <summary>連續幾次探測失敗才拉起服務端；對應 Weasel `_EnsureServerConnected` 的 `retry >= 6`。</summary>
	private const int StartServerAfterFailures = 6;

	/// <summary>連續失敗次數；成功一次就歸零。對應 Weasel 的靜態 `retry`。</summary>
	private static int ConnectRetry;

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

	/// <summary>目前的長連線；null 表示尚未連上或剛被重設。對應 Weasel 的 `hpipe == INVALID_HANDLE_VALUE`。</summary>
	private static NamedPipeClientStream? Pipe;
	private static StreamReader? PipeReader;
	private static StreamWriter? PipeWriter;

	/// <summary>丟掉目前的連線與讀寫器（Weasel 的 `_FinalizePipe` / `_Reconnect` 前半）。</summary>
	private static void Disconnect() {
		try { PipeWriter?.Dispose(); } catch { }
		try { PipeReader?.Dispose(); } catch { }
		try { Pipe?.Dispose(); } catch { }
		PipeWriter = null;
		PipeReader = null;
		Pipe = null;
	}

	/// <summary>
	/// 確保連線可用；連不上回 false，不拋。對應 Weasel `PipeChannelBase::_Ensure` + `_Connect`。
	/// </summary>
	/// <remarks>
	/// 只在「沒有連線」時才重建，已連上就直接用——這是 Weasel 的做法，也是長連線能成立的關鍵。
	/// 服務端不存在時 `Connect` 會立刻失敗（找不到管道），不會卡住；管道存在但實例都忙時才等到
	/// <see cref="ConnectWaitMs"/>，與 `WaitNamedPipe(name, 500)` 同一個行為。
	/// </remarks>
	private static bool EnsureConnected() {
		if (Pipe is not null) {
			return true;
		}
		try {
			// CurrentUserOnly：只有同一個使用者的行程能連上。少了它，同機任何行程都能
			// 送出「按鍵」請求，而回應裏的 commit 會被本殼寫進前景應用的文件。
			var Client = new NamedPipeClientStream(".", GetPipeName(), PipeDirection.InOut, PipeOptions.CurrentUserOnly);
			Client.Connect(ConnectWaitMs);
			Pipe = Client;
			PipeWriter = new StreamWriter(Client, new UTF8Encoding(false)) { AutoFlush = true };
			PipeReader = new StreamReader(Client, Encoding.UTF8);
			return true;
		}
		catch (Exception Exception) {
			LogWarn("connect failed " + Exception.GetType().Name);
			Disconnect();
			return false;
		}
	}

	/// <summary>
	/// 一次交易：確保連線 → 送出 → 阻塞讀回一行。對應 Weasel `PipeChannel::Transact`。
	/// </summary>
	/// <param name="Req">請求。</param>
	/// <returns>回應；連不上、寫入或讀取失敗、對方關閉連線時為 null。</returns>
	/// <remarks>
	/// *絕不重送*：請求只要寫進管道，服務端就可能已經處理過它；重送等於讓引擎把同一顆鍵處理兩次。
	/// Weasel 的 `Transact` 也沒有重送——失敗就是失敗，由呼叫端把按鍵交還給應用。
	/// 呼叫端必須持有 <see cref="PipeLock"/>。
	/// </remarks>
	private static TsfReply? Transact(TsfKeyRequest Req) {
		if (!EnsureConnected()) {
			return null;
		}
		try {
			PipeWriter!.WriteLine(Req.Format());
			var Line = PipeReader!.ReadLine();
			if (Line is null) {
				// 服務端關閉了連線（Weasel 的 `_Receive` 在這裡會拋錯）。
				// 不重送；下一顆鍵的探測會發現連線壞了並重連。
				LogWarn("pipe closed by server");
				return null;
			}
			return TsfReply.Parse(Line);
		}
		catch (Exception Exception) {
			LogWarn("transact failed " + Exception.GetType().Name);
			return null;
		}
	}

	/// <summary>
	/// 連線探測；對應 Weasel `ClientImpl::Echo`（送出 `WEASEL_IPC_ECHO`、回傳值非 0 表示活著）。
	/// </summary>
	/// <returns>true 表示服務端有回應。</returns>
	private static bool Echo() {
		var Reply = Transact(new TsfKeyRequest { Kind = ETsfReqKind.Echo });
		return Reply is not null;
	}

	/// <summary>
	/// 每一顆鍵進來時先確認服務端可用；對應 Weasel `WeaselTSF::_EnsureServerConnected`。
	/// </summary>
	/// <returns>true 表示可以處理這顆鍵；false 表示把按鍵交還給應用。</returns>
	/// <remarks>
	/// 與 Weasel 一字不差的三段：
	/// + 探測成功就用；
	/// + 探測失敗就重連、累計失敗次數；
	/// + 連續失敗達到 <see cref="StartServerAfterFailures"/> 就在背景拉起服務端，並把計數歸零。
	/// 拉起之後 Weasel 會等 500ms 再重連一次，這裡的等待放在背景執行緒裡，不佔按鍵路徑。
	/// </remarks>
	private static bool EnsureServerConnected() {
		if (Echo()) {
			return true;
		}
		Disconnect();
		ConnectRetry++;
		if (ConnectRetry >= StartServerAfterFailures) {
			StartServerInBackground();
			ConnectRetry = 0;
		}
		return Echo();
	}

	/// <summary>
	/// 在背景執行緒裡拉起服務端，等它開始監聽之後重連。
	/// 對應 Weasel `_EnsureServerConnected` 裡那段 `std::thread(...).detach()`。
	/// </summary>
	/// <remarks>
	/// *為何要背景*：Weasel 把 `ShellExecuteW(start_service.bat)` 放進 detached 執行緒，
	/// 因為服務端的啟動（我們這邊是 Rime 初始化，實測約 1.4 秒）不能卡在按鍵回調裏。
	/// *為何要具名互斥*：服務端崩掉時，幾十個各自載入本外殼的應用會同時嘗試拉起它；
	/// 互斥只讓第一個真的啟動，對應 Weasel 的 `WeaselDeployerExclusiveMutex`。
	/// </remarks>
	private static void StartServerInBackground() {
		try {
			var Dir = Path.GetDirectoryName(GetOwnModulePath());
			if (string.IsNullOrEmpty(Dir)) {
				return;
			}
			var Exe = Path.Combine(Dir, "Ngan.Ime.Windows.exe");
			if (!File.Exists(Exe)) {
				return;
			}
			// 每個安裝目錄一個互斥名稱：不同版本互不阻塞。
			var MutexName = "Local\\NganIme.Server.Start." + TsfIpc.PipeNameFor(Dir).Replace("NganIme.Tsf.v1.", "");
			// 只靠「名稱是否已存在」去重，不取所有權：Weasel 也是看 `ERROR_ALREADY_EXISTS`。
			var Gate = new Mutex(false, MutexName, out var CreatedNew);
			try {
				if (!CreatedNew) {
					Log("another process is already starting the server");
					return;
				}
				var Worker = new Thread(() => {
					try {
						var Info = new ProcessStartInfo(Exe) {
							Arguments = "--tsf",
							WorkingDirectory = Dir,
							UseShellExecute = false,
							CreateNoWindow = true,
						};
						Process.Start(Info);
						LogWarn("server was not running; started it from " + Dir);
						// 等它把管道監聽起來，再重連一次；對應 Weasel 的 sleep_for(500ms) + _Reconnect()。
						Thread.Sleep(StartServerWaitMs);
						lock (PipeLock) {
							Disconnect();
							EnsureConnected();
						}
					}
					catch (Exception Exception) {
						LogWarn("start server failed " + Exception.GetType().Name);
					}
				}) { IsBackground = true, Name = "NganIme.StartServer" };
				Worker.Start();
			}
			finally {
				// 只關掉本行程這一份 handle；名稱會隨最後一個 handle 關閉而消失。
				Gate.Dispose();
			}
		}
		catch (Exception Exception) {
			LogWarn("start server failed " + Exception.GetType().Name);
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
	/// <returns>服務端的回應；服務端不可用時為 null（呼叫端把按鍵交還給應用）。</returns>
	/// <remarks>
	/// 流程與 Weasel `_ProcessKeyEvent` 相同：先 `_EnsureServerConnected()`，可用才送；
	/// 不可用就回「沒吃掉」，讓系統把這顆鍵交給前景應用。
	/// **不會有「冷卻期內連問都不問」這回事**——那是先前自創的規則，已刪除。
	/// </remarks>
	private static TsfReply? QueryKey(int VirtualKey, int Mods, int X, int Y, bool IsUp, int Scan, int Ext) {
		lock (PipeLock) {
			if (!EnsureServerConnected()) {
				return null;
			}
			return Transact(new TsfKeyRequest {
				Kind = ETsfReqKind.Key,
				VirtualKey = VirtualKey,
				Mods = Mods,
				X = X,
				Y = Y,
				IsUp = IsUp,
				Scan = Scan,
				Ext = Ext,
			});
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
	/// 送一條不需要結果的命令（目前只有收起候選窗），並把服務端的回應讀掉。
	/// </summary>
	/// <remarks>
	/// 與按鍵走同一條交易路徑（Weasel 的 `_SendMessage` 也是所有命令共用一條 `Transact`）：
	/// 送出、阻塞讀回、不重送。沒讀到回應就代表連線壞了，丟掉它，下一顆鍵會重連。
	/// </remarks>
	private static void SendCommand(TsfKeyRequest Req) {
		lock (PipeLock) {
			if (Transact(Req) is null) {
				Disconnect();
			}
		}
	}

	/// <summary>請服務端收起候選窗。</summary>
	private static void SendHide() {
		SendCommand(new TsfKeyRequest { Kind = ETsfReqKind.Hide });
	}
}
