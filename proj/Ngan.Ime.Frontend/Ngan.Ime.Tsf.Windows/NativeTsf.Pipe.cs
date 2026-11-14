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
			for (var Attempt = 0; Attempt < 2; Attempt++) {
				if (!EnsureConnected()) {
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
					return TsfReply.Parse(Line);
				}
				catch (Exception Exception) {
					Log("query attempt " + Attempt + " failed " + Exception.GetType().Name);
					ResetPipe();
				}
			}
			return null;
		}
	}

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
