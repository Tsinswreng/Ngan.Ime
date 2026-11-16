namespace Ngan.Ime.Windows.Tsf;

using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Ngan.Ime.Tsf.Ipc;

[Doc($$"""
具名管道的監聽端：只負責「接受連線、逐行收發」，不碰協議內容也不碰引擎。

*為何要拆出來*：服務端原本一個類同時做監聽、連線生命週期、協議解析、引擎串行化與浮窗
（該類的註釋自己承認）。監聽這一段是唯一必須碰 `NamedPipeServerStream` 的部分，
把它獨立之後，請求處理就只剩「一行字進、一行字出」，測試與換傳輸（例如共享記憶體）
都不必動到它。

*與 Weasel 的對應*：對應 `WeaselIPC/PipeChannel` 的伺服端
（`_ConnectServerPipe` 用 `PIPE_UNLIMITED_INSTANCES`）＋`PipeServer::Listen`。
每條連線一個背景任務，請求交給 {{nameof(Handle)}}，回應照原樣寫回。

*實例數不設上限*：照 Weasel 的 `PIPE_UNLIMITED_INSTANCES`。先前寫死 16，
第 17 個載入外殼的應用就再也連不上——那個應用從此打不了字，症狀只是「某些程式沒反應」。
""")]
internal sealed class TsfPipeListener{

	[Doc("接受連線失敗後的重試間隔（ms），避免忙等。")]
	const i32 RetryDelayMs = 50;

	[Doc("本安裝專屬的管道名稱；由服務端 exe 所在目錄算出，與同層外殼算出同一個名稱。")]
	public str PipeName{get;}

	[Doc("收到一行請求時呼叫：一行進、一行出。")]
	Func<str, CT, Task<str?>>? Handle;

	[Doc("監聽迴圈的取消源；Start 時建立、Stop 時取消。")]
	CancellationTokenSource? LoopCts;

	[Doc("建立監聽端；管道名只算一次。")]
	public TsfPipeListener(str PipeName){
		this.PipeName = PipeName;
	}

	[Doc($$"""
	開始接受連線；重複呼叫無副作用。
	#Prm[{{nameof(OnRequest)}}][收到請求時的處理器。]
	""")]
	public void Start(Func<str, CT, Task<str?>> OnRequest){
		if(LoopCts is not null){
			return;
		}
		Handle = OnRequest;
		LoopCts = new CancellationTokenSource();
		var Ct = LoopCts.Token;
		_ = Task.Run(() => AcceptLoopAsy(Ct), Ct);
	}

	[Doc("停止接受連線並取消在途的讀寫；重複呼叫無副作用。")]
	public void Stop(){
		// 不 Dispose：背景任務可能還拿着 token，Dispose 之後再註冊會拋 ObjectDisposedException。
		LoopCts?.Cancel();
		LoopCts = null;
	}

	[Doc("持續接受連線；取消時結束，每條連線交給一個背景任務。")]
	async Task AcceptLoopAsy(CT Ct){
		while(!Ct.IsCancellationRequested){
			// 每輪新建一個實例：NamedPipeServerStream 斷線後不可重用。
			// 所有權交給 ServeAsy：它結束時才釋放，不能在本輪結束時就 dispose。
			// 建立實例本身也可能失敗（實例數用滿、被政策擋住）。必須放在 try 裏：
			// 先前它在 try 外面，一拋就讓整個監聽迴圈（fire-and-forget 的工作）無聲結束，
			// 行程還活着、介面還在，但輸入法永久沒反應。
			try{
				var Server = new NamedPipeServerStream(
					PipeName
					,PipeDirection.InOut
					,NamedPipeServerStream.MaxAllowedServerInstances
					,PipeTransmissionMode.Byte
					,PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
				);
				await Server.WaitForConnectionAsync(Ct);
				_ = Task.Run(() => ServeAsy(Server, Ct), Ct);
			}catch(OperationCanceledException){
				return;
			}catch(Exception Ex){
				AppLog.Error(Ex, "[TsfPipe] 接受連線失敗");
				try{
					await Task.Delay(RetryDelayMs, Ct).ConfigureAwait(false);
				}catch(OperationCanceledException){
					return;
				}
			}
		}
	}

	[Doc("服務一條連線：逐行讀請求、交給處理器、回一行；連線關閉或取消才結束。")]
	async Task ServeAsy(NamedPipeServerStream Server, CT Ct){
		await using(Server){
			try{
				AppLog.Trace($"[TsfPipe] 連線來源 pid={ReadClientProcessId(Server)}");
				using var Reader = new StreamReader(Server, Encoding.UTF8, leaveOpen: true);
				using var Writer = new StreamWriter(Server, new UTF8Encoding(false), leaveOpen: true){ AutoFlush = true };
				// 一條連線可送多個請求：外殼連一次、之後每顆鍵復用。
				while(!Ct.IsCancellationRequested){
					var Line = await Reader.ReadLineAsync(Ct);
					if(Line is null){
						return;
					}
					var Handler = Handle;
					if(Handler is null){
						return;
					}
					var Reply = await Handler(Line, Ct);
					if(Reply is null){
						// 單向請求（位置更新）：沒有回覆，也就沒有額外往返。
						continue;
					}
					await Writer.WriteLineAsync(Reply.AsMemory(), Ct);
				}
			}catch(OperationCanceledException){
				// 停止服務：正常結束，不是錯誤。
			}catch(IOException){
				// 客戶端正常斷線（外殼換行程、使用者切換輸入法）也會走到這裡。
				AppLog.Trace("[TsfPipe] 連線結束（客戶端斷線）");
			}catch(Exception Ex){
				AppLog.Error(Ex, "[TsfPipe] 連線異常結束");
			}
		}
	}

	[Doc("讀出這條連線的對端行程 id；取不到時回 0（只在日誌裏用）。")]
	static u32 ReadClientProcessId(NamedPipeServerStream Server){
		try{
			return GetNamedPipeClientProcessId(Server.SafePipeHandle.DangerousGetHandle(), out var Pid) ? Pid : 0;
		}catch{
			return 0;
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool GetNamedPipeClientProcessId(IntPtr Pipe, out u32 ProcessId);
}
