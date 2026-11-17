namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Ngan.Ime.Tsf.Ipc;

[Doc($$"""
外殼與服務端之間的管道客戶端。
*這支照 Weasel 的 IPC 抄*，逐條對應如下（原始碼在 `E:\_code\_clone\rime\weasel`）：

+ 一次交易就是「連線 → 送出 → 阻塞讀回」：Weasel `include/PipeChannel.h:92` 的
  `Transact` 只有三行——`_Ensure()`、`_Send()`、`_ReceiveResponse()`。
  **沒有逾時、沒有重送、沒有冷卻**：`WeaselIPC/PipeChannel.cpp:101` 的 `_Receive`
  就是阻塞 `ReadFile`，等到有資料為止。
+ 連線只在「無效」時才重建：`PipeChannelBase::_Ensure`。重建不會順手重送剛才那顆鍵。
+ 每一顆鍵都先問一次「服務端還在嗎」：`WeaselTSF/WeaselTSF.cpp` 的 `_EnsureServerConnected`
  呼叫 `m_client.Echo()`；成功就處理按鍵，失敗就重連。
+ 連續六次失敗才拉起服務端，而且**在脫離的背景執行緒裡**拉起、等 500ms 再重連：
  同一個 `_EnsureServerConnected`（`retry >= 6` 那段）。拉起用跨行程具名互斥去重，
  對應它的 `WeaselDeployerExclusiveMutex`。
+ 服務端不在時，按鍵照樣交還給應用（`*pfEaten = FALSE`），不是丟掉不問。

*與 Weasel 唯一的差別（傳輸編碼）*：Weasel 用 message-mode 管道傳固定結構（`_MsgSize`／`_ResSize`），
我們用同一條管道傳「一行文字」。框架方式不同，但「一次交易、不重送、不逾時、不冷卻」的語義相同。
這一點是本專案既有的協議（`TsfIpc`），不在這一輪改動範圍內。

*已知代價，與 Weasel 相同*：讀取是阻塞的，服務端若卡住，呼叫端（應用的 UI 執行緒）也一起等。
先前版本為了避開這個而加了逾時與冷卻，結果是「逾時後丟鍵」，症狀比卡頓更糟；
這裡選擇與 Weasel 一致：寧可等，不丟鍵。
""")]
internal static unsafe partial class NativeTsf {

	[Doc("診斷開關：環境變數 NGAN_TSF_DIAG=1 時，把每次位置推送也寫進日誌。")]
	private static readonly bool DiagEnabled = System.Environment.GetEnvironmentVariable("NGAN_TSF_DIAG") == "1";

	[Doc($$"""
	連線等待上限（毫秒）；對應 Weasel `_Connect` 裡的 `WaitNamedPipe(name, 500)`。
	""")]
	private const i32 ConnectWaitMs = 500;

	[Doc($$"""
	拉起服務端之後等它開始監聽的時間（毫秒）；對應 Weasel 的 `sleep_for(500ms)`。
	""")]
	private const i32 StartServerWaitMs = 500;

	[Doc($$"""
	連續幾次探測失敗才拉起服務端；對應 Weasel `_EnsureServerConnected` 的 `retry >= 6`。
	""")]
	private const i32 StartServerAfterFailures = 6;

	[Doc($$"""
	連續失敗次數；成功一次就歸零。對應 Weasel 的靜態 `retry`。
	""")]
	private static i32 ConnectRetry;

	[Doc($$"""
	序列化存取管道；多個應用行程各有一份，行程內只有 UI 執行緒會用。
	""")]
	private static readonly object PipeLock = new();

	[Doc($$"""
	本安裝專屬的管道名稱；第一次使用時由本 DLL 的路徑算出。
	外殼與服務端永遠同層（同一次發布的產物），故兩邊各自從自己的目錄算出同一個名稱。
	延遲計算：靜態初始化時 `GetOwnModulePath` 需要 kernel32 已就緒，第一次按鍵時算最安全。
	""")]
	private static str? PipeNameField;

	[Doc($$"""
	目前的長連線；null 表示尚未連上或剛被重設。對應 Weasel 的 `hpipe == INVALID_HANDLE_VALUE`。
	""")]
	private static NamedPipeClientStream? Pipe;
	private static StreamReader? PipeReader;
	private static StreamWriter? PipeWriter;

	[Doc($$"""
	丟掉目前的連線與讀寫器（Weasel 的 `_FinalizePipe` / `_Reconnect` 前半）。
	""")]
	private static void Disconnect() {
		try { PipeWriter?.Dispose(); } catch { }
		try { PipeReader?.Dispose(); } catch { }
		try { Pipe?.Dispose(); } catch { }
		PipeWriter = null;
		PipeReader = null;
		Pipe = null;
	}

	[Doc($$"""
	確保連線可用；連不上回 false，不拋。對應 Weasel `PipeChannelBase::_Ensure` + `_Connect`。
	只在「沒有連線」時才重建，已連上就直接用——這是 Weasel 的做法，也是長連線能成立的關鍵。
	服務端不存在時 `Connect` 會立刻失敗（找不到管道），不會卡住；管道存在但實例都忙時才等到
	{{nameof(ConnectWaitMs)}}，與 `WaitNamedPipe(name, 500)` 同一個行為。
	""")]
	private static bool EnsureConnected() {
		if (Pipe is not null) {
			return true;
		}
		try {
			// CurrentUserOnly：只有同一個使用者的行程能連上。少了它，同機任何行程都能
			// 送出「按鍵」請求，而回應裏的 commit 會被本殼寫進前景應用的文件。
			var client = new NamedPipeClientStream(".", GetPipeName(), PipeDirection.InOut, PipeOptions.CurrentUserOnly);
			client.Connect(ConnectWaitMs);
			Pipe = client;
			PipeWriter = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
			PipeReader = new StreamReader(client, Encoding.UTF8);
			return true;
		} catch (Exception Exception) {
			LogWarn("connect failed " + Exception.GetType().Name);
			Disconnect();
			return false;
		}
	}

	[Doc($$"""
	一次交易：確保連線 → 送出 → 阻塞讀回一行。對應 Weasel `PipeChannel::Transact`。
	*絕不重送*：請求只要寫進管道，服務端就可能已經處理過它；重送等於讓引擎把同一顆鍵處理兩次。
	Weasel 的 `Transact` 也沒有重送——失敗就是失敗，由呼叫端把按鍵交還給應用。
	呼叫端必須持有 {{nameof(PipeLock)}}。
	#Prm[{{nameof(Req)}}][請求。]
	#Rtn[回應；連不上、寫入或讀取失敗、對方關閉連線時為 null。]
	""")]
	private static TsfReply? Transact(TsfKeyRequest Req) {
		if (!EnsureConnected()) {
			return null;
		}
		try {
			PipeWriter!.WriteLine(Req.Format());
			var line = PipeReader!.ReadLine();
			if (line is null) {
				// 服務端關閉了連線（Weasel 的 `_Receive` 在這裡會拋錯）。
				// 不重送；下一顆鍵的探測會發現連線壞了並重連。
				LogWarn("pipe closed by server");
				return null;
			}
			return TsfReply.Parse(line);
		} catch (Exception Exception) {
			LogWarn("transact failed " + Exception.GetType().Name);
			return null;
		}
	}

	[Doc($$"""
	連線探測；對應 Weasel `ClientImpl::Echo`（送出 `WEASEL_IPC_ECHO`、回傳值非 0 表示活著）。
	#Rtn[true 表示服務端有回應。]
	""")]
	private static bool Echo() {
		var reply = Transact(new TsfKeyRequest { Kind = ETsfReqKind.Echo });
		return reply is not null;
	}

	[Doc($$"""
	每一顆鍵進來時先確認服務端可用；對應 Weasel `WeaselTSF::_EnsureServerConnected`。
	與 Weasel 一字不差的三段：
	+ 探測成功就用；
	+ 探測失敗就重連、累計失敗次數；
	+ 連續失敗達到 {{nameof(StartServerAfterFailures)}} 就在背景拉起服務端，並把計數歸零。
	拉起之後 Weasel 會等 500ms 再重連一次，這裡的等待放在背景執行緒裡，不佔按鍵路徑。
	#Rtn[true 表示可以處理這顆鍵；false 表示把按鍵交還給應用。]
	""")]
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

	[Doc($$"""
	在背景執行緒裡拉起服務端，等它開始監聽之後重連。
	對應 Weasel `_EnsureServerConnected` 裡那段 `std::thread(...).detach()`。
	*為何要背景*：Weasel 把 `ShellExecuteW(start_service.bat)` 放進 detached 執行緒，
	因為服務端的啟動（我們這邊是 Rime 初始化，實測約 1.4 秒）不能卡在按鍵回調裏。
	*為何要具名互斥*：服務端崩掉時，幾十個各自載入本外殼的應用會同時嘗試拉起它；
	互斥只讓第一個真的啟動，對應 Weasel 的 `WeaselDeployerExclusiveMutex`。
	""")]
	private static void StartServerInBackground() {
		try {
			var dir = Path.GetDirectoryName(GetOwnModulePath());
			if (str.IsNullOrEmpty(dir)) {
				return;
			}
			var exe = Path.Combine(dir, "Ngan.Ime.Windows.exe");
			if (!File.Exists(exe)) {
				return;
			}
			// 每個安裝目錄一個互斥名稱：不同版本互不阻塞。
			var mutexName = "Local\\NganIme.Server.Start." + TsfIpc.PipeNameFor(dir).Replace("NganIme.Tsf.v1.", "");
			// 只靠「名稱是否已存在」去重，不取所有權：Weasel 也是看 `ERROR_ALREADY_EXISTS`。
			var gate = new Mutex(false, mutexName, out var createdNew);
			try {
				if (!createdNew) {
					Log("another process is already starting the server");
					return;
				}
				var worker = new Thread(() => {
					try {
						var info = new ProcessStartInfo(exe) {
							Arguments = "--tsf",
							WorkingDirectory = dir,
							UseShellExecute = false,
							CreateNoWindow = true,
						};
						Process.Start(info);
						LogWarn("server was not running; started it from " + dir);
						// 等它把管道監聽起來，再重連一次；對應 Weasel 的 sleep_for(500ms) + _Reconnect()。
						Thread.Sleep(StartServerWaitMs);
						lock (PipeLock) {
							Disconnect();
							EnsureConnected();
						}
					} catch (Exception Exception) {
						LogWarn("start server failed " + Exception.GetType().Name);
					}
				}) { IsBackground = true, Name = "NganIme.StartServer" };
				worker.Start();
			} finally {
				// 只關掉本行程這一份 handle；名稱會隨最後一個 handle 關閉而消失。
				gate.Dispose();
			}
		} catch (Exception Exception) {
			LogWarn("start server failed " + Exception.GetType().Name);
		}
	}

	[Doc($$"""
	把一顆按鍵送給服務端並取回結果。
	流程與 Weasel `_ProcessKeyEvent` 相同：先 `_EnsureServerConnected()`，可用才送；
	不可用就回「沒吃掉」，讓系統把這顆鍵交給前景應用。
	**不會有「冷卻期內連問都不問」這回事**——那是先前自創的規則，已刪除。
	#Prm[{{nameof(VirtualKey)}}][Win32 虛擬鍵碼。]
	#Prm[{{nameof(Mods)}}][修飾鍵位；0 表示沒有。]
	#Prm[{{nameof(X)}}][插入點螢幕 X，供候選窗定位。]
	#Prm[{{nameof(Y)}}][插入點螢幕 Y。]
	#Prm[{{nameof(IsUp)}}][true 表示擡起。]
	#Prm[{{nameof(Scan)}}][掃描碼；分左右 Shift 用。]
	#Prm[{{nameof(Ext)}}][擴展鍵位。]
	#Rtn[服務端的回應；服務端不可用時為 null（呼叫端把按鍵交還給應用）。]
	""")]
	private static TsfReply? QueryKey(i32 VirtualKey, i32 Mods, i32 X, i32 Y, bool IsUp, i32 Scan, i32 Ext) {
		lock (PipeLock) {
			if (!EnsureServerConnected()) {
				return null;
			}
			// 每顆鍵一個序號：位置更新帶的是「觸發量測的那顆鍵」的序號，
			// 服務端因此分得出遲到的位置是屬於哪一顆鍵（見 TsfIpc.Version 的說明）。
			var Seq = Interlocked.Increment(ref KeySeqCounter);
			LastKeySeq = Seq;
			return Transact(new TsfKeyRequest {
				Kind = ETsfReqKind.Key,
				VirtualKey = VirtualKey,
				Mods = Mods,
				X = X,
				Y = Y,
				IsUp = IsUp,
				Scan = Scan,
				Ext = Ext,
				Seq = Seq,
			});
		}
	}

	[Doc("按鍵序號的計數器；只增不減，跨執行緒用原子遞增。")]
	private static i32 KeySeqCounter;

	[Doc($$"""
	最後一顆送出的按鍵序號。
	*為何是靜態欄位而不是狀態物件*：量測會話是非同步的，它要帶的序號就是「剛剛送出的那顆鍵」；
	TSF 的回調與它排出的編輯會話在同一條 UI 執行緒上依序執行，故讀到的必定是同一顆鍵。
	""")]
	private static i32 LastKeySeq;

	[Doc($$"""
	本安裝專屬的管道名稱；第一次使用時算出並記住。
	""")]
	private static str GetPipeName() {
		var cached = PipeNameField;
		if (cached is not null) {
			return cached;
		}
		var dir = Path.GetDirectoryName(GetOwnModulePath());
		if (str.IsNullOrEmpty(dir)) {
			// 取不到自己的路徑：退回前綴。此時只可能連到「同一個前綴」的服務端，
			// 不至於完全不能用，也不會誤連到另一個版本（那個版本的名稱帶雜湊）。
			dir = "";
		}
		cached = TsfIpc.PipeNameFor(dir);
		PipeNameField = cached;
		return cached;
	}

	[Doc($$"""
	取得本外殼 DLL 的完整路徑；NativeAOT 下不能用組件位置。
	""")]
	private static str GetOwnModulePath() {
		var handle = IntPtr.Zero;
		if (GetModuleHandleEx(ModuleHandleFromAddress | ModuleHandleUnchangedRefCount, GetOwnModuleAnchor(), out handle) && handle != IntPtr.Zero) {
			var buffer = new StringBuilder(1024);
			var length = GetModuleFileName(handle, buffer, (u32)buffer.Capacity);
			if (length > 0) {
				return buffer.ToString(0, (i32)length);
			}
		}
		return str.Empty;
	}

	[Doc($$"""
	取一個「位在本模組裡」的函式位址，供 GetModuleHandleEx 反查模組。
	""")]
	private static IntPtr GetOwnModuleAnchor() {
		delegate* unmanaged<IntPtr> Anchor = &MarkerForModuleLookup;
		return (IntPtr)Anchor;
	}

	[System.Runtime.InteropServices.UnmanagedCallersOnly]
	private static IntPtr MarkerForModuleLookup() {
		return IntPtr.Zero;
	}

	private const u32 ModuleHandleFromAddress = 0x00000004;

	[Doc($$"""
	`GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT`：只查詢模組，不增加它的參考計數。
	*先前寫成 0x1 是錯的*：0x1 是 `GET_MODULE_HANDLE_EX_FLAG_PIN`，會把查到的模組
	**永久釘在行程裡**。`GetOwnModulePath` 拿它查自己還無所謂（本來就不該卸載），
	但看門狗的 `DescribeAddress` 是拿它去反查**任意**位址所屬的模組——
	等於把前景應用裡不相干的模組逐一釘住，永不卸載。
	三個值的官方定義（`libloaderapi.h`）：PIN = 0x1、UNCHANGED_REFCOUNT = 0x2、
	FROM_ADDRESS = 0x4。這裡兩個旗標都用得到，故值必須是 0x2。
	""")]
	private const u32 ModuleHandleUnchangedRefCount = 0x00000002;

	[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetModuleHandleEx(u32 Flags, IntPtr ModuleName, out IntPtr Module);

	[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
	private static extern u32 GetModuleFileName(IntPtr Module, StringBuilder FileName, u32 Size);

	[Doc($$"""
	送一條不需要結果的命令（目前只有收起候選窗），並把服務端的回應讀掉。
	與按鍵走同一條交易路徑（Weasel 的 `_SendMessage` 也是所有命令共用一條 `Transact`）：
	送出、阻塞讀回、不重送。沒讀到回應就代表連線壞了，丟掉它，下一顆鍵會重連。
	""")]
	private static void SendCommand(TsfKeyRequest Req) {
		lock (PipeLock) {
			if (Transact(Req) is null) {
				Disconnect();
			}
		}
	}

	[Doc($$"""
	請服務端收起候選窗。
	""")]
	private static void SendHide() {
		SendCommand(new TsfKeyRequest { Kind = ETsfReqKind.Hide });
	}

	[Doc($$"""
	把量到的插入點推給服務端，只為了移動候選窗。

	*時機照 Weasel*：`_SetCompositionPosition` 量到就呼叫 `_cand->UpdateInputPosition(rc)`
	（`WeaselTSF/Composition.cpp:228-243`）。我們的浮窗在另一支行程，只能靠這條請求即時推過去；
	不推就只會在下一顆按鍵時才動，看起來就是「大致跟隨、還常常蓋住光標」。
	""")]
	private static void SendCaretPosition(i32 X, i32 Y, i32 Top, i32 Seq) {
		// 單向：寫出去就結束，不讀回覆。
		// *為何不讀*：這條請求跑在 TSF 的 edit session 裡、應用的 UI 執行緒上，
		// 多一次阻塞往返就是多一次卡頓；而位置更新丟掉也無妨——下一顆鍵會再送一次。
		lock (PipeLock) {
			if (!EnsureConnected()) {
				return;
			}
			try {
				PipeWriter!.WriteLine(new TsfKeyRequest { Kind = ETsfReqKind.Position, X = X, Y = Y, Top = Top, Seq = Seq }.Format());
			} catch (Exception) {
				Disconnect();
			}
		}
		if (DiagEnabled) {
			LogWarn("[Pos] sent x=" + X + " y=" + Y + " top=" + Top + " seq=" + Seq);
		}
	}
}
