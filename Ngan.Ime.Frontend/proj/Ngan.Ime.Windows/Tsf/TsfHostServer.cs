namespace Ngan.Ime.Windows.Tsf;

using System.IO.Pipes;
using System.Text;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using Tsinswreng.CsCore;

[Doc($$"""
TSF 外殼的宿主端：按鍵經具名管道送進來，交給引擎處理，再把 preedit／候選／commit 回給外殼。

*協議*（單行 UTF-8，欄位以 `|` 分隔）：
- 請求：`K|<vk>|<mods>|<x>|<y>`；
- 回應：`R|<eaten>|<highlight>|<preedit b64>|<n>|<cand b64>~<comment b64>|…|<m>|<commit b64>|…`。
外殼端（`Ngan.Ime.Tsf.Windows`）是另一份獨立實現，改協議時兩邊必須同步；
本版尚未帶協議版本號，故只能成對升級。

*為何按鍵要成對送*：請求只帶「系統按下的那顆鍵」，但引擎需要看到按下與擡起兩個事件，
否則修飾鍵與 Enter/Space 的狀態會殘留。故這裡在一個請求內補出 Down ＋ Up。

*AOT*：鍵名表直接取自 {{nameof(KeyChars)}} 的手寫登記表（`KeyChars.ByName`），
不再用 `typeof(KeyChars).GetProperties()` 反射——那在 NativeAOT 裁剪後會得到空表，
症狀是「所有鍵都解析失敗」。

*安全*：管道使用默認 ACL（建立者與管理員可存取），同一使用者下的行程仍可連上。
真正的防護需要「協議握手 token ＋ 外殼端配合」，屬下一輪工作；本檔的注釋已標明。

#Eg[
	```cs
	TsfHostServer.Start();
	```
]
""")]
public static class TsfHostServer{

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

	[Doc("啟動管道監聽迴圈（背景）。")]
	public static void Start(){
		_ = Task.Run(AcceptLoopAsy);
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
				AppLog.Error(Ex, "[TsfHost] 接受連線失敗");
				Server.Dispose();
				await Task.Delay(RetryDelayMs);
			}
		}
	}

	[Doc("服務一條連線：讀一行請求、處理、回一行回應。")]
	static async Task ServeAsy(NamedPipeServerStream Server){
		using(Server){
			try{
				using var Reader = new StreamReader(Server, Encoding.UTF8, leaveOpen: true);
				using var Writer = new StreamWriter(Server, new UTF8Encoding(false), leaveOpen: true){ AutoFlush = true };
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
				Server.WaitForPipeDrain();
			}catch(Exception Ex){
				AppLog.Error(Ex, "[TsfHost] 處理連線失敗");
			}
		}
	}

	[Doc($$"""
	處理一條請求並組出回應；畸形輸入回「未吃掉」而不是拋。
	#Prm[{{nameof(Line)}}][請求行，格式見類註釋。]
	#Rtn[回應行。]
	""")]
	static async Task<str> HandleAsync(str Line){
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
		var Svc = Di.SvcProviderOrNull?.GetService<IImeInput>();
		var State = Di.SvcProviderOrNull?.GetService<IImeState>();
		var Key = Resolve(Vk);
		if(Svc is null || State is null || Key is null){
			HideWindow();
			return EmptyReply();
		}

		var Commits = new List<str>();
		void OnCommit(obj? Sender, str Text) => Commits.Add(Text);
		Svc.OnCommit += OnCommit;
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
			// 成對送出：引擎需要看到按下與擡起，否則前臺應用的修飾鍵狀態會殘留。
			await Svc.Input([
				new KeyEvent{ KeyChar = Key, KeyState = KeyStates.Down, KeyBoardState = Board },
				new KeyEvent{ KeyChar = Key, KeyState = KeyStates.Up, KeyBoardState = Board },
			], CancellationToken.None);
		}catch(Exception Ex){
			AppLog.Error(Ex, "[TsfHost] 送鍵失敗");
			return EmptyReply();
		}finally{
			Svc.OnCommit -= OnCommit;
		}

		var Preedit = State.Preedit ?? "";
		var Page = State.Candidates;
		var Eaten = Commits.Count > 0 || !string.IsNullOrEmpty(Preedit) || (Page.Data?.Count ?? 0) > 0;
		PostUpdate(Preedit, Page, X, Y);
		return BuildReply(Eaten, Preedit, Page, Commits);
	}

	[Doc("把虛擬鍵碼還原成鍵；目前只覆蓋「按鍵本身能表達」的鍵，其餘回 null（未吃掉）。")]
	static IKeyChar? Resolve(i32 Vk){
		str? Name = Vk switch {
			>= 0x41 and <= 0x5A => ((char)('a' + Vk - 0x41)).ToString(),
			>= 0x30 and <= 0x39 => ((char)('0' + Vk - 0x30)).ToString(),
			0x20 => " ",
			0x08 => nameof(KeyChars.Backspace),
			0x0D => nameof(KeyChars.Enter),
			0x09 => nameof(KeyChars.Tab),
			0x1B => null,
			_ => null,
		};
		if(Name is null){
			return null;
		}
		return KeyChars.Find(Name);
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

	[Doc("切回 UI 線程更新候選窗（控件只能在 UI 線程碰）。")]
	static void PostUpdate(str Preedit, CandidatePage? Page, i32 X, i32 Y){
		Dispatcher.UIThread.Post(() => {
			Window ??= new TsfCandidateWindow();
			Window.Update(Preedit, Page, X, Y);
		});
	}

	[Doc("收起候選窗。")]
	static void HideWindow(){
		Dispatcher.UIThread.Post(() => Window?.HideIfVisible());
	}
}
