namespace Ngan.Ime.Windows.Tsf;

using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.Tsf.Ipc;
using Ngan.Ime.UI.Views.CandidatePopup;
using Tsinswreng.CsCore;

[Doc($$"""
TSF 管道服務端：按鍵經具名管道送進來，交給引擎處理，再把結果回給外殼。

*協議*：見 `TsfIpc`（`proj/_shared/TsfIpc.cs`），兩端共用同一份型別與解析器。
本檔只負責「傳輸」與「把協議物件翻成引擎呼叫」，不自己拼字串。

*管道名按安裝目錄算*：每次安裝都是一個獨立輸入法，各自有服務端；
共用一條固定名稱的管道會讓兩個版本的服務端同時監聽同一條名稱，
客戶端連到哪一個由系統決定。名稱由 `TsfIpc.PipeNameFor` 依本 exe 所在目錄算出，
外殼從自己的目錄算出同一個名稱。

*已知的職責重疊*：本類同時做監聽、連線生命週期、協議解析、引擎串行化與浮窗顯示。
拆分方案（`TsfPipeListener`／`TsfRequestHandler`／`TsfReplyMapper`）已記在 2026-11-15 的審查報告，
本輪先把「監聽迴圈不可無聲死亡」與「位置未知」兩件事修掉，拆分留給下一輪。

*一次按鍵的處理*：解析請求 → 依 @虛擬鍵碼 還原按鍵 → 依方向建事件 → 交給引擎 →
把引擎狀態與上屏文字打包成回應。引擎只有一個會話，多條連線以信號量串行化。

*錯誤不吞鍵*：任何失敗都回「沒吃掉」加一個錯誤碼，由系統把按鍵交給前景應用。
錯誤碼只進日誌與回應，不改變是否吞鍵，避免輸入法故障導致整臺機器打不了字。

*生命週期*：{{nameof(Start)}} 冪等；{{nameof(Stop)}} 取消監聽並斷線。
先前是靜態類、發射後不管，重複呼叫會開第二條監聽、也沒有收尾的路。

#Eg[
	```cs
	Svc.GetRequiredService<ISvcTsfPipeServer>().Start();
	```
]
""")]
public sealed class SvcTsfPipeServer : ISvcTsfPipeServer{

	[Doc("最大同時連線數；每個應用行程各持一條。")]
	const i32 MaxConnections = 16;

	[Doc("接受連線失敗後的重試間隔（ms），避免忙等。")]
	const i32 RetryDelayMs = 50;

	[Doc("引擎輸入入口。")]
	readonly IImeInput Input;

	[Doc("引擎可觀察狀態；回應要從這裡取組字串與候選頁。")]
	readonly IImeState State;

	[Doc("候選浮窗；只認介面，不知道它是 Avalonia 視窗。")]
	readonly ICandidatePopup Popup;

	[Doc("串行化引擎存取：多個應用各自持有一條連線，但引擎只有一個會話。")]
	readonly SemaphoreSlim EngineLock = new(1, 1);

	[Doc("本安裝專屬的管道名稱：由服務端 exe 所在目錄算出，與同層的外殼算出同一個名稱。")]
	static str PipeName => field ??= TsfIpc.PipeNameFor(AppContext.BaseDirectory);

	[Doc("診斷模式：環境變數 NGAN_TSF_TIMING=1 時，逐鍵記錄耗時與座標（平時不記，避免日誌爆量）。")]
	static readonly bool TimingEnabled = System.Environment.GetEnvironmentVariable("NGAN_TSF_TIMING") == "1";

	[Doc("監聽迴圈的取消源；Start 時建立、Stop 時取消。")]
	CancellationTokenSource? LoopCts;

	[Doc("是否已啟動；保證 Start 冪等。")]
	bool Started;

	[Doc("啟動／停止的互斥；避免兩個執行緒同時進出生命週期。")]
	readonly object LifecycleGate = new();

	[Doc("建立服務端；依賴全部由容器給。")]
	public SvcTsfPipeServer(IImeInput Input, IImeState State, ICandidatePopup Popup){
		this.Input = Input;
		this.State = State;
		this.Popup = Popup;
	}

	[Doc("開始監聽並嘗試連接引擎；重複呼叫無副作用。")]
	public void Start(){
		lock(LifecycleGate){
			if(Started){
				return;
			}
			Started = true;
			LoopCts = new CancellationTokenSource();
			var Ct = LoopCts.Token;
			_ = Task.Run(() => AcceptLoopAsy(Ct), Ct);
			// 主動連接一次，讓界面狀態反映實情；失敗也不影響之後按需初始化。
			// *必須與按鍵處理共用同一把引擎鎖*：引擎的 setup／initialize 不是執行緒安全的，
			// 這裡若與首次按鍵的按需初始化並行，就會初始化同一個引擎兩次；
			// 輕則會話沒有方案（按鍵全部不處理），重則原生層存取違規直接崩掉。
			_ = Task.Run(async () => {
				var Locked = false;
				try{
					await EngineLock.WaitAsync(Ct);
					Locked = true;
					await Input.ConnectAsy(Ct);
				}catch(OperationCanceledException){
					// Stop 之後才拿到取消：正常結束，不是錯誤。
				}catch(Exception Ex){
					AppLog.Error(Ex, "[TsfPipe] 連接引擎失敗，將在首次按鍵時重試");
				}finally{
					// 只有真的拿到鎖才釋放，否則會在 Stop 之後多減一次。
					if(Locked){
						EngineLock.Release();
					}
				}
			}, Ct);
		}
	}

	[Doc("停止監聽；未啟動時無副作用。")]
	public void Stop(){
		lock(LifecycleGate){
			if(!Started){
				return;
			}
			Started = false;
			LoopCts?.Cancel();
			LoopCts?.Dispose();
			LoopCts = null;
		}
		Popup.Hide();
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
					,MaxConnections
					,PipeTransmissionMode.Byte
					,PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
				);
				await Server.WaitForConnectionAsync(Ct);
				_ = Task.Run(() => ServeAsy(Server, Ct), Ct);
			}catch(OperationCanceledException){
				return;
			}catch(Exception Ex){
				AppLog.Error(Ex, "[TsfPipe] 接受連線失敗");
				await Task.Delay(RetryDelayMs, Ct).ConfigureAwait(false);
			}
		}
	}

	[Doc("服務一條連線：逐行讀請求、處理、回一行；連線關閉或取消才結束。")]
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
					string Reply;
					await EngineLock.WaitAsync(Ct);
					try{
						Reply = await HandleAsync(Line, Ct);
					}finally{
						EngineLock.Release();
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
	static uint ReadClientProcessId(NamedPipeServerStream Server){
		try{
			return GetNamedPipeClientProcessId(Server.SafePipeHandle.DangerousGetHandle(), out var Pid) ? Pid : 0;
		}catch{
			return 0;
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool GetNamedPipeClientProcessId(IntPtr Pipe, out uint ProcessId);

	[Doc($$"""
	處理一條請求並組出回應；畸形輸入回「未吃掉」加錯誤碼，不拋。
	#Prm[{{nameof(Line)}}][請求行；格式見 {{nameof(TsfIpc)}}。]
	#Rtn[回應行。]
	""")]
	async Task<str> HandleAsync(str Line, CT Ct){
		if(Line.Length > TsfIpc.MaxLineLength){
			return TsfReply.Fail(ETsfErr.LineTooLong).Format();
		}
		var Req = TsfKeyRequest.Parse(Line);
		if(Req is null){
			return TsfReply.Fail(ETsfErr.BadRequest).Format();
		}
		if(Req.Ver != TsfIpc.Version){
			return TsfReply.Fail(ETsfErr.UnsupportedVersion).Format();
		}
		if(Req.Kind == ETsfReqKind.Hide){
			Popup.Hide();
			return TsfReply.Empty().Format();
		}
		return await HandleKeyAsync(Req, Ct);
	}

	[Doc($$"""
	處理一顆按鍵：還原按鍵、交給引擎、打包結果。
	#Prm[{{nameof(Req)}}][按鍵請求。]
	#Rtn[回應行。]
	""")]
	async Task<str> HandleKeyAsync(TsfKeyRequest Req, CT Ct){
		var Key = Resolve(Req.VirtualKey, Req.Scan, Req.Ext);
		if(Key is null){
			// 認不出來的鍵不算錯誤：回「沒吃掉」，由系統交給前景應用。
			// 此時不動候選窗，否則按一下未登記的鍵就會把組字中的候選閃掉。
			return TsfReply.Fail(ETsfErr.UnknownKey).Format();
		}
		var Watch = TimingEnabled ? System.Diagnostics.Stopwatch.StartNew() : null;
		IRespOnKeyEvent Resp;
		try{
			Resp = await Input.Input([MkEvent(Key, Req)], Ct);
		}catch(Exception Ex){
			AppLog.Error(Ex, "[TsfPipe] 送鍵失敗");
			return TsfReply.Fail(ETsfErr.EngineFailed).Format();
		}
		var Reply = MkReply(Resp);
		// 只有按下才動浮窗：擡起時引擎多半沒吃，但畫面不該跟著抖。
		if(!Req.IsUp){
			ShowPopup(Reply, Req);
		}
		if(Watch is not null){
			Watch.Stop();
			// 診斷用：外殼每顆鍵都會到這裡；看得到耗時與座標，才分得清「卡在服務端」還是「卡在外殼」。
			AppLog.Info($"[Key] vk={Req.VirtualKey} up={(Req.IsUp ? 1 : 0)} mods={Req.Mods} x={Req.X} y={Req.Y} eaten={(Reply.Eaten ? 1 : 0)} ms={Watch.Elapsed.TotalMilliseconds:F2}");
		}
		return Reply.Format();
	}

	[Doc("依請求建一個按下或擡起的按鍵事件，並把修飾鍵狀態帶上。")]
	static KeyEvent MkEvent(IKeyChar Key, TsfKeyRequest Req){
		var Board = new KeyBoardState{ AllDownKeys = new HashSet<IKeyChar>() };
		if((Req.Mods & TsfIpc.ModShift) != 0){
			Board.AllDownKeys.Add(KeyChars.Shift_L);
		}
		if((Req.Mods & TsfIpc.ModCtrl) != 0){
			Board.AllDownKeys.Add(KeyChars.Ctrl_L);
		}
		if((Req.Mods & TsfIpc.ModAlt) != 0){
			Board.AllDownKeys.Add(KeyChars.Alt_L);
		}
		return new KeyEvent{
			KeyChar = Key,
			KeyState = Req.IsUp ? KeyStates.Up : KeyStates.Down,
			KeyBoardState = Board,
		};
	}

	[Doc($$"""
	把引擎的回報與狀態打包成協議物件。
	#Prm[{{nameof(Resp)}}][引擎對這批按鍵的回應。]
	#Rtn[回應物件；候選與上屏都已複製成托管資料。]
	""")]
	TsfReply MkReply(IRespOnKeyEvent Resp){
		var Page = State.Candidates;
		var Reply = new TsfReply{
			// 唯一判據：引擎把所有鍵都吃掉了。
			// 不能用「有沒有組字串／候選」判斷——退格刪到空、Escape 取消組字
			// 這類按鍵引擎吃掉了卻不留下狀態，用狀態判斷會把它們放行給應用。
			Eaten = Resp.UnhandledKeys.Count == 0,
			Preedit = State.Preedit ?? "",
			Highlight = Page?.HighlightedIndex ?? 0,
		};
		foreach(var One in Page?.Data ?? []){
			Reply.Candidates.Add(new TsfCandidate{ Text = One.Text ?? "", Comment = One.Comment ?? "" });
		}
		foreach(var One in Resp.Commits){
			Reply.Commits.Add(One);
		}
		return Reply;
	}

	[Doc($$"""
	把回應內容交給浮窗；沒有內容時收起。
	#Prm[{{nameof(Reply)}}][已打包的回應。]
	#Prm[{{nameof(Req)}}][原始請求；取插入點座標。]
	""")]
	void ShowPopup(TsfReply Reply, TsfKeyRequest Req){
		if(Reply.Preedit.Length == 0 && Reply.Candidates.Count == 0){
			Popup.Hide();
			return;
		}
		var Page = new CandidatePage{
			HighlightedIndex = Reply.Highlight,
			Data = [.. Reply.Candidates.ConvertAll(One => (ICandidate)new Candidate{ Text = One.Text, Comment = One.Comment })],
		};
		// (0,0) 是協議定義的「拿不到插入點」，不是螢幕左上角。傳 null 讓浮窗保持原位，
		// 否則量測一失敗，候選窗就會被搬到螢幕角落並一直留在那裏。
		PixelPoint? Anchor = (Req.X == 0 && Req.Y == 0) ? null : new PixelPoint(Req.X, Req.Y);
		Popup.Show(Reply.Preedit, Page, Anchor);
	}

	[Doc($$"""
	把請求還原成一個鍵。
	#Prm[{{nameof(Vk)}}][Win32 虛擬鍵碼。]
	#Prm[{{nameof(Scan)}}][掃描碼；0 表示外殼沒提供。]
	#Prm[{{nameof(Ext)}}][擴展鍵位。]
	#Rtn[對應的鍵；未登記的鍵回 null（回報「沒吃掉」）。]
	""")]
	/// <remarks>
	/// *為何需要掃描碼*：`VK_SHIFT` 對左右 Shift 是同一個值（0x10），
	/// 但「左 Shift 切英文、右 Shift 切中文」是很常見的配置。
	/// 協定第 2 版起外殼會帶掃描碼與擴展鍵位，這裡據此把左右分開；
	/// 舊版外殼沒有這兩欄（Scan 為 0），退回原本「左右不分、一律算左邊」的行為。
	/// </remarks>
	static IKeyChar? Resolve(i32 Vk, i32 Scan, i32 Ext){
		if(Scan != 0){
			switch(Scan){
				case 0x2A: return KeyChars.Shift_L;
				case 0x36: return KeyChars.Shift_R;
				case 0x1D: return Ext != 0 ? KeyChars.Ctrl_R : KeyChars.Ctrl_L;
				case 0x38: return Ext != 0 ? KeyChars.Alt_R : KeyChars.Alt_L;
				case 0x5B: return KeyChars.Meta_L;
				case 0x5C: return KeyChars.Meta_L;
			}
		}
		return KeyEventConverter.Inst.KeyByVk.GetValueOrDefault(Vk);
	}
}
