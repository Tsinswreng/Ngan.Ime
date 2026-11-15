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
/// </remarks>
internal static unsafe partial class NativeTsf {

	/// <summary>連線建立的逾時（毫秒）。</summary>
	private const int ConnectTimeoutMs = 200;

	/// <summary>讀取回應的逾時（毫秒）；太長會讓應用明顯卡頓。</summary>
	private const int ReadTimeoutMs = 300;

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

	/// <summary>目前的長連線；null 表示尚未連上或剛被重設。</summary>
	private static NamedPipeClientStream? Pipe;
	private static StreamReader? PipeReader;
	private static StreamWriter? PipeWriter;

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
		if (Pipe is not null && Pipe.IsConnected) {
			return true;
		}
		ResetPipe();
		try {
			var Client = new NamedPipeClientStream(".", TsfIpc.PipeName, PipeDirection.InOut);
			Client.Connect(ConnectTimeoutMs);
			Pipe = Client;
			PipeWriter = new StreamWriter(Client, new UTF8Encoding(false)) { AutoFlush = true };
			PipeReader = new StreamReader(Client, Encoding.UTF8);
			return true;
		}
		catch (Exception Exception) {
			Log("connect failed " + Exception.GetType().Name);
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
	/// <returns>服務端的回應；連不上、逾時或回應壞掉時為 null。</returns>
	private static TsfReply? QueryKey(int VirtualKey, int Mods, int X, int Y, bool IsUp) {
		lock (PipeLock) {
			// 服務端剛失敗過就先不試：這段路徑在 UI 執行緒上，等不起。
			if (Environment.TickCount64 < RetryAfterTick) {
				return null;
			}
			for (var Attempt = 0; Attempt < 2; Attempt++) {
				if (!EnsureConnected()) {
					MarkFailed();
					return null;
				}
				try {
					var Req = new TsfKeyRequest {
						VirtualKey = VirtualKey,
						Mods = Mods,
						X = X,
						Y = Y,
						IsUp = IsUp,
					};
					PipeWriter!.WriteLine(Req.Format());
					var Line = ReadLineWithTimeout();
					if (Line is null) {
						ResetPipe();
						continue;
					}
					RetryAfterTick = 0;
					return TsfReply.Parse(Line);
				}
				catch (Exception Exception) {
					Log("query attempt " + Attempt + " failed " + Exception.GetType().Name);
					ResetPipe();
				}
			}
			MarkFailed();
			return null;
		}
	}

	/// <summary>記下這次失敗，接下來 <see cref="FailureBackoffMs"/> 內不再嘗試連線。</summary>
	private static void MarkFailed() {
		RetryAfterTick = Environment.TickCount64 + FailureBackoffMs;
		Log("server unreachable; backoff " + FailureBackoffMs + "ms");
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
			var Info = new System.Diagnostics.ProcessStartInfo(Exe) {
				Arguments = "--tsf",
				WorkingDirectory = Dir,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			System.Diagnostics.Process.Start(Info);
			Log("started server on demand");
		}
		catch (Exception Exception) {
			Log("start server failed " + Exception.GetType().Name);
		}
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
	/// 在逾時內讀一行；逾時回 null 並放棄連線。
	/// </summary>
	/// <remarks>
	/// StreamReader 沒有可用的讀取逾時（NamedPipeClientStream 的 ReadTimeout 不支援），
	/// 故以「非同步讀 ＋ 有界等待」實現。逾時後必須丟掉連線，否則下一次讀會接著等同一條死連線。
	/// </remarks>
	private static string? ReadLineWithTimeout() {
		var ReadTask = PipeReader!.ReadLineAsync();
		if (!ReadTask.Wait(ReadTimeoutMs)) {
			return null;
		}
		return ReadTask.Result;
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
				ReadLineWithTimeout();
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
