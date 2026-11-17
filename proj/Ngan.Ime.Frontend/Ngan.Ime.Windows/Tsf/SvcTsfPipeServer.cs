namespace Ngan.Ime.Windows.Tsf;

using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.Tsf.Ipc;
using Ngan.Ime.UI.Views.CandidatePopup;

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

	[Doc("引擎輸入入口。")]
	readonly IImeInput Input;

	[Doc("引擎可觀察狀態；回應要從這裡取組字串與候選頁。")]
	readonly IImeState State;

	[Doc("候選浮窗；只認介面，不知道它是 Avalonia 視窗。")]
	readonly ICandidatePopup Popup;

	[Doc("串行化引擎存取：多個應用各自持有一條連線，但引擎只有一個會話。")]
	readonly SemaphoreSlim EngineLock = new(1, 1);

	[Doc("管道監聽端；只負責收發一行字，不碰協議與引擎。")]
	readonly TsfPipeListener Listener = new(TsfIpc.PipeNameFor(AppContext.BaseDirectory));

	[Doc("診斷模式：環境變數 NGAN_TSF_TIMING=1 時，逐鍵記錄耗時與座標（平時不記，避免日誌爆量）。")]
	static readonly bool TimingEnabled = System.Environment.GetEnvironmentVariable("NGAN_TSF_TIMING") == "1";

	[Doc("待顯示的引擎回應：等外殼推來插入點位置才放上浮窗。")]
	TsfReply? PendingReply;

	[Doc("待顯示內容所屬的按鍵序號；位置更新的序號早於它就代表「這是上一顆鍵的位置」。")]
	i32 PendingSeq;

	[Doc("已經套用過的最新位置序號；更早的位置一律丟棄，避免把浮窗搬回舊位置。")]
	i32 LastPosSeq;

	[Doc("待顯示內容的互斥；按鍵執行緒與位置更新執行緒都會碰它。")]
	readonly object PopupGate = new();

	[Doc("是否已啟動；保證 Start 冪等。")]
	bool Started;

	[Doc("啟動／停止的互斥；避免兩個執行緒同時進出生命週期。")]
	readonly object LifecycleGate = new();

	[Doc("最近一次量到的插入點；狀態提示貼着它顯示。")]
	readonly CaretAnchor Anchor;

	[Doc("建立服務端；依賴全部由容器給。")]
	public SvcTsfPipeServer(IImeInput Input, IImeState State, ICandidatePopup Popup, CaretAnchor Anchor){
		this.Input = Input;
		this.State = State;
		this.Popup = Popup;
		this.Anchor = Anchor;
	}

	[Doc($$"""
	初始化引擎之後才開始監聽；重複呼叫無副作用。

	*順序照 Weasel*：`WeaselServerApp::Run` 先 `m_handler->Initialize()`（Rime 初始化），
	之後才 `m_server.Run()` 起管道監聽執行緒。反過來（先監聽、引擎在另一條 Task 裡連）
	會讓啟動那一到兩秒內進來的按鍵全部排在 `EngineLock` 上，而外殼那一端是無逾時阻塞讀——
	每一個正在打字的應用都會凍住，直到初始化結束。初始化期間管道根本不存在，
	外殼的探測會立刻失敗（`Connect` 直接拋），按鍵即時交還給應用，這才是正確的行為。

	*初始化失敗仍要監聽*：按鍵會拿到 {{nameof(ETsfErr.EngineFailed)}} 並被放行，
	服務端不會變成「界面在、管道不在」的殭屍；下一次按鍵會再按需初始化一次。

	*引擎鎖仍然共用*：引擎的 setup／initialize 不是執行緒安全的，初始化與按鍵處理
	必須在同一把鎖下，否則會初始化同一個引擎兩次（輕則會話沒有方案，重則原生層存取違規）。
	""")]
	public void Start(){
		lock(LifecycleGate){
			if(Started){
				return;
			}
			Started = true;
			_ = Task.Run(async () => {
				try{
					await EngineLock.WaitAsync();
					try{
						await Input.ConnectAsy(default);
					}finally{
						// 只有真的拿到鎖才釋放，否則會在 Stop 之後多減一次。
						EngineLock.Release();
					}
				}catch(Exception Ex){
					// 初始化失敗仍要監聽：按鍵會拿到 EngineFailed 並被放行。
					AppLog.Error(Ex, "[TsfPipe] 連接引擎失敗，按鍵將回報未處理，並在下次按鍵時重試");
				}
				// 初始化完成（或失敗）之後才開始接受連線：照 Weasel 的順序。
				// 初始化期間可能已經被 Stop：那就不要再開監聽。
				lock(LifecycleGate){
					if(!Started){
						return;
					}
					Listener.Start(HandleAsync);
				}
			});
		}
	}

	[Doc($$"""
	停止監聽；未啟動時無副作用。

	*不 Dispose 取消源*：那是 {{nameof(TsfPipeListener)}} 的責任，它也不能 Dispose——
	背景任務可能還拿着 token（`ReadLineAsync(Ct)`），Dispose 之後再註冊會拋
	`ObjectDisposedException`，而先前那條路只有 `catch(Exception)` 接着，變成沒有痕跡的孤兒任務。
	""")]
	public void Stop(){
		lock(LifecycleGate){
			if(!Started){
				return;
			}
			Started = false;
			Listener.Stop();
		}
		Popup.Hide();
	}

	[Doc("最後一個記過日誌的不符版本號；同一個版本只記一次，避免每顆鍵刷一則。")]
	static i32 LastWarnedBadVer = i32.MinValue;

	[Doc("協議版本不符時記一次 Warn（帶雙方版本）；同一版本號只記一次。")]
	static void WarnBadVersionOnce(i32 Ver){
		if(Interlocked.Exchange(ref LastWarnedBadVer, Ver) != Ver){
			AppLog.Warn($"[TsfPipe] 協議版本不符：請求 {Ver}，本服務端 {TsfIpc.Version}；"
				+ "該版本的所有按鍵都會被放行（外殼與服務端必須是同一次發布的產物）");
		}
	}

	[Doc($$"""
	處理一條請求並組出回應；畸形輸入回「未吃掉」加錯誤碼，不拋。
	#Prm[{{nameof(Line)}}][請求行；格式見 {{nameof(TsfIpc)}}。]
	#Rtn[回應行。]
	""")]
	async Task<str?> HandleAsync(str Line, CT Ct){
		if(Line.Length > TsfIpc.MaxLineLength){
			return TsfReply.Fail(ETsfErr.LineTooLong).Format();
		}
		var Req = TsfKeyRequest.Parse(Line);
		if(Req is null){
			return TsfReply.Fail(ETsfErr.BadRequest).Format();
		}
		if(Req.Ver != TsfIpc.Version){
			// 版本不符是「整個功能靜默失效」的級別：外殼每一顆鍵都拿到「沒吃掉」，
			// 使用者看到的是「那幾個程式突然打不出中文」。所以一定要留下痕跡，
			// 但同一版本只記一次，免得每次按鍵都刷一則。
			WarnBadVersionOnce(Req.Ver);
			return TsfReply.Fail(ETsfErr.UnsupportedVersion).Format();
		}
		if(Req.Kind == ETsfReqKind.Hide){
			Popup.Hide();
			return TsfReply.Empty().Format();
		}
		if(Req.Kind == ETsfReqKind.Echo){
			// 探測不碰引擎：外殼每一顆鍵都先問它一次（照 Weasel 的 ECHO）。
			// 能在這裡回答就代表「管道通、服務端活著」，不必等引擎鎖。
			return TsfReply.Empty().Format();
		}
		if(Req.Kind == ETsfReqKind.Position){
			// 只移動浮窗，不碰引擎（Weasel 的 UpdateInputPosition 也只碰 UI）。
			// 外殼在每次量到插入點時就推一條過來，浮窗因此即時跟隨光標。
			// 與上次完全相同的位置不重複套用：Weasel 的 `MoveTo` 也有一條抖動抑制
			// （`WeaselPanel.cpp:1135-1137`），免得同一顆鍵的第二條位置再搬一次。
			// *過期位置直接丟棄*：位置跑在非同步的編輯會話裡，可能比下一顆鍵的請求更晚到。
			// 若把它套到「已經換成下一顆鍵的內容」上，面板就會先出現在上一顆鍵的位置、
			// 等下一條位置到了再搬一次——那正是使用者看到的「跳一下」。
			// Weasel 靠同步 IPC 天然有序；我們靠這個序號達到同樣的效果。
			if(Req.Seq < LastPosSeq){
				AppLog.Info($"[Pos] stale seq={Req.Seq} last={LastPosSeq} x={Req.X} y={Req.Y} top={Req.Top}");
				return null;
			}
			// 內容比這條位置新：這是上一顆鍵的位置，連錨點都不該覆寫，
			// 否則等真正的位置到來之前，面板會先被擺在這個舊座標上。
			if(HasPendingShow(out var WaitingSeq) && Req.Seq < WaitingSeq){
				AppLog.Info($"[Pos] wait seq={Req.Seq} pending={WaitingSeq} x={Req.X} y={Req.Y} top={Req.Top}");
				return null;
			}
			LastPosSeq = Req.Seq;
			var Moved = !Anchor.Has
				|| Anchor.X != Req.X || Anchor.Y != Req.Y || Anchor.Top != Req.Top;
			Anchor.Set(Req.X, Req.Y, Req.Top);
			// 有待顯示的內容就一次做完「套內容 ＋ 定尺寸 ＋ 定位 ＋ 顯示」；沒有就只換位置。
			// 拆成兩步會先畫在舊位置再搬，使用者就會看到跳一下。
			var Showed = ApplyPendingShow(Req.Seq);
			if(!Showed && Moved){
				Popup.MoveTo(Req.X, Req.Y, Req.Top);
			}
			AppLog.Info($"[Pos] x={Req.X} y={Req.Y} top={Req.Top} show={(Showed ? 1 : 0)} moved={(Moved ? 1 : 0)}");
			// 單向請求：不回一行，外殼也不必等。
			return null;
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
		TsfReply Reply;
		// 引擎只有一個會話（照 Weasel 的 `g_api_mutex`）：送鍵與「取狀態快照」必須在同一把鎖內，
		// 否則兩個應用的鍵交錯時，A 的回應會讀到 B 之後的候選與組字。
		// 探測、收起、位置更新不碰引擎，所以不進這把鎖——否則浮窗跟隨會被別人的按鍵拖住。
		await EngineLock.WaitAsync(Ct);
		try{
			Resp = await Input.Input([MkEvent(Key, Req)], Ct);
			Reply = MkReply(Resp);
		}catch(Exception Ex){
			AppLog.Error(Ex, "[TsfPipe] 送鍵失敗");
			return TsfReply.Fail(ETsfErr.EngineFailed).Format();
		}finally{
			EngineLock.Release();
		}
		if(!Anchor.Has && (Req.X != 0 || Req.Y != 0)){
			// 按鍵請求順帶的座標也是量測結果，但它**沒有上緣欄**：只能把下緣當上緣（高度 0）。
			// 這種退化值只當「從來沒收到位置更新」時的兜底；一旦有過真正的 `P`，
			// 就不再用它覆寫——否則每顆鍵都會把上緣打成下緣，翻到上方時面板下緣
			// 正好壓在文字行上（照 Weasel 的 CUAS 分支，零高矩形不用來定位）。
			Anchor.Set(Req.X, Req.Y, Req.Y);
		}
		// 只有按下才動浮窗：擡起時引擎多半沒吃，但畫面不該跟著抖。
		if(!Req.IsUp){
			// 模式提示不在这里触发：Weasel 是「引擎發出 option 通知時才顯示」
			//（`RimeWithWeasel.cpp` 的 `_ShowMessage`），不是每顆鍵都彈。
			// 觸發點在 SvcModeTip 訂閱的 IImeState.OptionNotified。
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
			ClearPendingShow();
			Popup.Hide();
			return;
		}
		// 照 Weasel 的單一位置寫入者：內容先存着，等外殼量到插入點推來位置再顯示。
		// 先前是「先用按鍵請求裡的舊座標顯示、收到位置再搬」，於是每顆鍵都先出現在
		// 上一顆鍵的位置、然後跳一下。
		lock(PopupGate){
			PendingReply = Reply;
			PendingSeq = Req.Seq;
		}
		AppLog.Info($"[Pos] pending seq={Req.Seq} cand={Reply.Candidates.Count} preedit={Reply.Preedit.Length}");
	}

	[Doc($$"""
	把「等位置」的內容放上浮窗，並用目前已知的位置一次顯示完。
	只有序號不早於內容的位置才算「這一顆鍵的位置」；更早的留著內容、等真正的那一條。
	#Prm[{{nameof(Seq)}}][這條位置更新的序號。]
	#Rtn[true 表示確實有內容被放上去。]
	""")]
	bool ApplyPendingShow(i32 Seq){
		TsfReply? Reply;
		lock(PopupGate){
			if(PendingReply is not null && Seq < PendingSeq){
				// 內容比這條位置新：這條位置是上一顆鍵的，內容留給它自己的位置。
				return false;
			}
			Reply = PendingReply;
			PendingReply = null;
		}
		if(Reply is null){
			return false;
		}
		var Page = new CandidatePage{
			HighlightedIndex = Reply.Highlight,
			Data = [.. Reply.Candidates.ConvertAll(One => (ICandidate)new Candidate{ Text = One.Text, Comment = One.Comment })],
		};
		// 位置與顯示在同一次呼叫裡完成（見 ICandidatePopup.ShowAt 的說明）。
		Popup.ShowAt(Reply.Preedit, Page, Anchor.X, Anchor.Y, Anchor.Top);
		return true;
	}

	[Doc($$"""
	是否還有內容在等位置；有待顯示時回傳它的序號。
	#Prm[{{nameof(Seq)}}][待顯示內容的序號；沒有內容時為 0。]
	#Rtn[true 表示有內容在等。]
	""")]
	bool HasPendingShow(out i32 Seq){
		lock(PopupGate){
			Seq = PendingSeq;
			return PendingReply is not null;
		}
	}

	[Doc("丟掉待顯示的內容；沒有時無副作用。")]
	void ClearPendingShow(){
		lock(PopupGate){
			PendingReply = null;
			PendingSeq = 0;
		}
	}

	[Doc($$"""
	把請求還原成一個鍵。

	表與判斷都在 {{nameof(KeyResolverByScan)}}（`Ngan.Ime.Core.Keys`）：
	那是一段純資料 ＋ 純判斷，放 Core 才能被測試直接餵數字驗；
	這裡只負責把服務端的 Vk 表（`KeyEventConverter`）餵進去。

	#Prm[{{nameof(Vk)}}][Win32 虛擬鍵碼。]
	#Prm[{{nameof(Scan)}}][掃描碼；0 表示外殼沒提供。]
	#Prm[{{nameof(Ext)}}][擴展鍵位。]
	#Rtn[對應的鍵；未登記的鍵回 null（回報「沒吃掉」）。]
	""")]
	static IKeyChar? Resolve(i32 Vk, i32 Scan, i32 Ext){
		return KeyResolverByScan.Resolve(Vk, Scan, Ext, KeyEventConverter.Inst.KeyByVk);
	}
}
