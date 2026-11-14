namespace Ngan.Ime.Windows.Tsf;

using System.IO.Pipes;
using System.Text;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using Tsinswreng.CsCore;

[Doc($$"""
TSF 服務端：按鍵經具名管道送進來，交給引擎處理，再把 preedit／候選／commit 回給外殼。

*協議*（單行 UTF-8，欄位以 `|` 分隔）：
- 請求：`K|<vk>|<mods>|<x>|<y>|<state>`，state 為 0 表示按下、1 表示擡起，省略時視為按下；
- 收起請求：`HIDE`，外殼在失焦或停用時送出；
- 回應：`R|<eaten>|<highlight>|<preedit b64>|<n>|<cand b64>~<comment b64>|…|<m>|<commit b64>|…`。
外殼端（`Ngan.Ime.Tsf.Windows`）是另一份獨立實現，改協議時兩邊必須同步；
本版尚未帶協議版本號，故只能成對升級。

*方向*：外殼把按下與擡起都送進來，由引擎自己判斷要不要處理。
引擎靠 `kReleaseMask` 分辨方向（見 `RimeKeyCharConverter`），
靠 release 才能生效的規則（例如修飾鍵鬆開時切換中英）因此拿得到事件。

*AOT*：鍵名表直接取自 {{nameof(KeyChars)}} 的手寫登記表（`KeyChars.ByName`），
不再用 `typeof(KeyChars).GetProperties()` 反射，那在 NativeAOT 裁剪後會得到空表，
症狀是「所有鍵都解析失敗」。

*安全*：管道使用默認 ACL（建立者與管理員可存取），同一使用者下的行程仍可連上。
真正的防護需要「協議握手 token ＋ 外殼端配合」，屬下一輪工作；本檔的注釋已標明。

#Eg[
	```cs
	TsfPipeServer.Start();
	```
]
""")]
public static class TsfPipeServer{

	[Doc("管道名稱；外殼端有同名常量，改名時兩邊都要改。")]
	public const str PipeName = "NganIme.Tsf.v1";

	[Doc("最大同時連線數。")]
	const i32 MaxConnections = 16;

	[Doc("連線失敗後的重試間隔（ms），避免忙等。")]
	const i32 RetryDelayMs = 50;

	[Doc("串行化引擎存取：多個應用（explorer、notepad）各自持有一條連線，但引擎只有一個會話。")]
	static readonly SemaphoreSlim EngineLock = new(1, 1);

	[Doc("唯一的候選窗；在 UI 線程建立與更新。")]
	static TsfCandidateWindow? Window;

	[Doc("啟動管道監聽迴圈並連接引擎（背景）。")]
	public static void Start(){
		_ = Task.Run(AcceptLoopAsy);
		// 主動連接一次：讓 IsConnected 與界面狀態反映真實情況；
		// 就算這裡失敗，之後的按鍵仍會由 RimeKeyProcessor 按需初始化引擎。
		_ = Task.Run(async () => {
			try{
				if(Di.SvcProviderOrNull?.GetService<IImeInput>() is {} Input){
					await Input.ConnectAsy();
				}
			}catch(Exception Ex){
				AppLog.Error(Ex, "[TsfPipe] 連接引擎失敗，將在首次按鍵時重試");
			}
		});
	}

	[Doc("持續接受連線；每條連線交給一個背景任務。")]
	static async Task AcceptLoopAsy(){
		while(true){
			var Server = new NamedPipeServerStream(
				PipeName
				,PipeDirection.InOut
				,MaxConnections
				,PipeTransmissionMode.Byte
				,PipeOptions.Asynchronous
			);
			try{
				await Server.WaitForConnectionAsync();
				_ = Task.Run(() => ServeAsy(Server));
			}catch(Exception Ex){
				AppLog.Error(Ex, "[TsfPipe] 接受連線失敗");
				Server.Dispose();
				await Task.Delay(RetryDelayMs);
			}
		}
	}

	[Doc("服務一條連線：逐行讀請求、處理、回一行；連線關閉才結束。")]
	static async Task ServeAsy(NamedPipeServerStream Server){
		using(Server){
			try{
				using var Reader = new StreamReader(Server, Encoding.UTF8, leaveOpen: true);
				using var Writer = new StreamWriter(Server, new UTF8Encoding(false), leaveOpen: true){ AutoFlush = true };
				// 一條連線可送多個請求：外殼端是「連一次、之後每顆鍵復用」，
				// 只處理一行就關閉會讓下一次寫入直接失敗（實測：每兩顆鍵就掉一次）。
				while(true){
					var Line = await Reader.ReadLineAsync();
					if(Line is null){
						return;
					}
					string Reply;
					await EngineLock.WaitAsync();
					try{
						Reply = await HandleAsync(Line);
					}finally{
						EngineLock.Release();
					}
					await Writer.WriteLineAsync(Reply);
					await Writer.FlushAsync();
				}
			}catch(IOException){
				// 客戶端正常斷線（外殼端換行程、使用者切換輸入法）會走到這裡，不是錯誤。
				AppLog.Trace("[TsfPipe] 連線結束（客戶端斷線）");
			}catch(Exception Ex){
				AppLog.Error(Ex, "[TsfPipe] 連線異常結束");
			}
		}
	}

	[Doc($$"""
	處理一條請求並組出回應；畸形輸入回「未吃掉」而不是拋。
	#Prm[{{nameof(Line)}}][請求行，格式見類註釋。]
	#Rtn[回應行。]
	""")]
	static async Task<str> HandleAsync(str Line){
		// 外殼在失焦或停用時送 HIDE，這裡只收起候選窗，不碰引擎狀態。
		if(Line == "HIDE"){
			HideWindow();
			return EmptyReply();
		}
		var Parts = Line.Split('|');
		if(Parts.Length < 5 || Parts[0] != "K"){
			return EmptyReply();
		}
		if(!i32.TryParse(Parts[1], out var Vk) || !i32.TryParse(Parts[2], out var Mods)){
			return EmptyReply();
		}
		if(!i32.TryParse(Parts[3], out var X) || !i32.TryParse(Parts[4], out var Y)){
			X = 0;
			Y = 0;
		}
		// 方向欄可省略：省略時視為按下，讓舊版外殼與煙霧測試仍然可用。
		var IsUp = Parts.Length > 5 && Parts[5] == "1";
		var Svc = Di.SvcProviderOrNull?.GetService<IImeInput>();
		var State = Di.SvcProviderOrNull?.GetService<IImeState>();
		var Key = Resolve(Vk);
		if(Svc is null || State is null || Key is null){
			// 認不出來的鍵不算錯誤：回「沒吃掉」，由系統交給前景應用。
			// 此時不收起候選窗，否則按一下 Shift 以外的未登記鍵就會把組字中的候選窗閃掉。
			return EmptyReply();
		}

		IRespOnKeyEvent Resp;
		try{
			var Board = new KeyBoardState{ AllDownKeys = new HashSet<IKeyChar>() };
			if((Mods & 1) != 0){
				Board.AllDownKeys.Add(KeyChars.Shift_L);
			}
			if((Mods & 2) != 0){
				Board.AllDownKeys.Add(KeyChars.Ctrl_L);
			}
			if((Mods & 4) != 0){
				Board.AllDownKeys.Add(KeyChars.Alt_L);
			}
			// 按下與擡起都送：引擎靠 release 掩碼分辨方向，靠 release 生效的規則才拿得到事件。
			Resp = await Svc.Input([
				new KeyEvent{ KeyChar = Key, KeyState = IsUp ? KeyStates.Up : KeyStates.Down, KeyBoardState = Board },
			], CancellationToken.None);
		}catch(Exception Ex){
			AppLog.Error(Ex, "[TsfPipe] 送鍵失敗");
			return EmptyReply();
		}

		var Commits = Resp.Commits.ToList();
		var Preedit = State.Preedit ?? "";
		var Page = State.Candidates;
		// Eaten 的唯一判據：引擎把所有鍵都吃掉了。
		// 不能用「有沒有 preedit／候選」來判斷，退格刪到空、Escape 取消組字這類按鍵
		// 引擎吃掉了卻不留下任何狀態，用狀態判斷會把它們放行給應用，等於同時刪兩次。
		var Eaten = Resp.UnhandledKeys.Count == 0;
		PostUpdate(Preedit, Page, X, Y);
		return BuildReply(Eaten, Preedit, Page, Commits);
	}

	[Doc("把虛擬鍵碼還原成鍵；未登記的鍵回 null（回報「沒吃掉」，由系統交給前景應用）。")]
	static IKeyChar? Resolve(i32 Vk){
		return KeyEventConverter.Inst.KeyByVk.GetValueOrDefault(Vk);
	}

	[Doc("沒吃掉任何東西的空回應。")]
	static str EmptyReply(){
		return "R|0|0||0|0";
	}

	[Doc("組回應：eaten、高亮、preedit、候選清單、commit 清單（字串都以 Base64 傳，避免分隔符衝突）。")]
	static str BuildReply(bool Eaten, str Preedit, CandidatePage? Page, List<str> Commits){
		var Builder = new StringBuilder();
		Builder.Append("R|").Append(Eaten ? 1 : 0).Append('|')
			.Append(Page?.HighlightedIndex ?? 0).Append('|')
			.Append(B64(Preedit)).Append('|');
		var Items = Page?.Data ?? [];
		Builder.Append(Items.Count).Append('|');
		foreach(var Item in Items){
			Builder.Append(B64(Item.Text)).Append('~').Append(B64(Item.Comment)).Append('|');
		}
		Builder.Append(Commits.Count).Append('|');
		foreach(var Text in Commits){
			Builder.Append(B64(Text)).Append('|');
		}
		return Builder.ToString();
	}

	[Doc("UTF-8 字串轉 Base64，避免 `|` 與 `~` 出現在負載裡。")]
	static str B64(str Text){
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(Text ?? ""));
	}

	[Doc("收起候選窗。")]
	static void HideWindow(){
		Dispatcher.UIThread.Post(() => Window?.HideIfVisible());
	}

	[Doc("切回 UI 線程更新候選窗（控件只能在 UI 線程碰）。")]
	static void PostUpdate(str Preedit, CandidatePage? Page, i32 X, i32 Y){
		Dispatcher.UIThread.Post(() => {
			Window ??= new TsfCandidateWindow();
			Window.Update(Preedit, Page, X, Y);
		});
	}
}
