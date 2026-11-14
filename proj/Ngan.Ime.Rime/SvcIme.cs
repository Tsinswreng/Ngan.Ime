using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using Rime.Api;
using Rime.Api.Types;
using Tsinswreng.CsInterop;
using Tsinswreng.CsCore;

namespace Ngan.Ime.Rime;


[Doc($$"""
`IImeState`／`IImeInput`／`IImeOptions` 的 Rime 實現。

*狀態從哪來*：每批按鍵處理完（{{nameof(SvcImeBase.AfterInput)}}）就從 librime 抓一次
`get_status` 與 `get_context`，複製成托管快照。界面只讀快照，不碰原生記憶體。

*鎖的分工*：
- {{nameof(StateLock)}}：保護「一次狀態同步」的完整性，避免讀到 status 與 context 不一致的中間態；
- {{nameof(ConnectLock)}}：序列化連接流程（`RimeSetup` 只能初始化一次）。
連接流程不持 {{nameof(StateLock)}} 做初始化——librime 初始化會阻塞數秒，持鎖會卡住所有按鍵。

*取消語義*：連接任務是共享的（多個界面可能同時要求連接），故任務本身**不綁定任何呼叫方的令牌**；
呼叫方只在自己的 `WaitAsync` 上取消。舊版把第一個呼叫方的令牌塞進共享任務，
導致「A 關了頁面 → B 的連接被連坐取消」。

*選項切換*：`ascii_mode`／`simplification` 走 librime 的 `get_option`/`set_option`，
並由 {{nameof(RimeSetup.OnOptionChanged)}} 回寫狀態（方案自己切換時也能同步）。

#Eg[
	```cs
	IImeInput Input = Di.GetRSvc<IImeInput>();
	Input.InputSafely([new KeyEvent{ KeyChar = KeyChars.LowerA, KeyState = KeyStates.Down }]);
	```
]
""")]
public class SvcIme : SvcImeBase {

	[Doc("取引擎單例的工廠；連接時才真的初始化。")]
	readonly Func<RimeSetup> GetRimeSetup;

	[Doc("狀態同步鎖；保證 status 與 context 一次讀完。")]
	readonly Lock StateLock = new();

	[Doc("連接流程鎖；序列化初始化。")]
	readonly SemaphoreSlim ConnectLock = new(1, 1);

	[Doc("選項切換鎖；兩個選項共用，避免同時 set_option 造成狀態交錯。")]
	readonly SemaphoreSlim OptionLock = new(1, 1);

	[Doc("共享的連接任務；完成（含失敗）後可重建。")]
	Task<nil>? ConnectTask;

	[Doc("最近一次成功連接的引擎實例；未連接時為 null。")]
	RimeSetup? Setup;

	[Doc("option 名稱緩衝的最大位元組數；現有兩個名稱都遠小於此。")]
	const i32 OptionNameBufferBytes = 32;

	[Doc("建立 Rime 服務；引擎與鍵處理器由容器注入。")]
	public SvcIme(
		IOsKeyProcessor OsKeyProcessor
		,IImeKeyProcessor ImeKeyProcessor
		,Func<RimeSetup> GetRimeSetup
	) : base(OsKeyProcessor, ImeKeyProcessor){
		this.GetRimeSetup = GetRimeSetup;
		// option 變更由 librime 主動通知（方案裡切換、快捷鍵等），故狀態要能從該回調同步回來。
		RimeSetup.OnOptionChanged += OnOptionChanged;
		// 每批按鍵處理完就從 librime 重抓一次狀態。
		// 少了這一行，Preedit／Candidates 永遠停在連接時的快照：
		// 服務端會以為「這顆鍵沒產生任何東西」而放行，症狀就是「打 a 直接上屏 a、候選框不彈」。
		AfterInput += OnAfterInputSync;
	}

	[Doc("連接（或重試連接）引擎；成功後立刻同步一次狀態。")]
	public override async Task ConnectAsy(CT Ct = default){
		Ct.ThrowIfCancellationRequested();
		var Task = ConnectTask;
		if(Task is null || Task.IsCompleted){
			await ConnectLock.WaitAsync(Ct);
			try{
				Task = ConnectTask;
				if(Task is null || Task.IsCompleted){
					// 共享任務：刻意不傳 Ct（見類註釋）。
					Task = ConnectCoreAsy();
					ConnectTask = Task;
				}
			}finally{
				ConnectLock.Release();
			}
		}
		await Task.WaitAsync(Ct);
	}

	[Doc("單次連接：背景預熱 → 取實例 → 同步狀態；失敗時重置單例以便重試。")]
	async Task<nil> ConnectCoreAsy(){
		IsConnecting = true;
		var Success = false;
		try{
			await RimeWarmup.EnsureWarmAsy();
			Setup = GetRimeSetup();
			SyncStateFromRime();
			IsConnected = true;
			Success = true;
			AppLog.Info("[Rime] 已連接");
		}catch(Exception Ex){
			IsConnected = false;
			// 半初始化的單例必須丟棄，否則 Lazy 會永久快取失敗。
			RimeSetup.Reset();
			Setup = null;
			AppLog.Error(Ex, "[Rime] 連接失敗");
			throw;
		}finally{
			IsConnecting = false;
			RaiseConnectCompleted(Success);
		}
		return NIL;
	}

	[Doc("每批按鍵處理完後同步狀態；在消費迴圈的線程上呼叫，故與按鍵處理天然串行。")]
	void OnAfterInputSync(obj? Sender, IEnumerable<IKeyEvent> Keys){
		SyncStateFromRime();
	}

	[Doc("切換英文模式。")]
	public override async Task<nil> ToggleAsciiModeAsy(CT Ct = default){
		await ToggleOptionAsy("ascii_mode", Ct);
		return NIL;
	}

	[Doc("切換簡繁。")]
	public override async Task<nil> ToggleSimplificationAsy(CT Ct = default){
		await ToggleOptionAsy("simplification", Ct);
		return NIL;
	}

	[Doc($$"""
	切換一個 librime option：讀當前值、寫反值，再同步狀態。
	#Prm[{{nameof(OptionName)}}][option 名，必須是 ASCII 且長度小於 {nameof(OptionNameBufferBytes)}。]
	#Prm[{{nameof(Ct)}}][取消令牌。]
	""")]
	async Task<nil> ToggleOptionAsy(str OptionName, CT Ct){
		Ct.ThrowIfCancellationRequested();
		await OptionLock.WaitAsync(Ct);
		try{
			ToggleOptionCore(OptionName);
			SyncStateFromRime();
		}finally{
			OptionLock.Release();
		}
		return NIL;
	}

	[Doc("真正讀寫 librime option 的同步部分；指針操作抽在這裡，因為 async 方法不能位於 unsafe 上下文。")]
	unsafe void ToggleOptionCore(str OptionName){
		var Engine = Setup ?? throw KeysErr.Rime.SessionInvalid.ToErr();
		var Session = Engine.rimeSessionId;
		if(Session == 0){
			throw KeysErr.Rime.SessionInvalid.ToErr();
		}
		var Buffer = stackalloc byte[OptionNameBufferBytes];
		WriteOptionName(OptionName, new Span<byte>(Buffer, OptionNameBufferBytes));
		var Current = Engine.apiFn.get_option(Session, Buffer);
		Engine.apiFn.set_option(Session, Buffer, Current == RimeUtil.False ? RimeUtil.True : RimeUtil.False);
	}

	[Doc("把 option 名寫成 NUL 結尾的 UTF-8；超長屬程式錯誤，直接拋參數異常。")]
	static void WriteOptionName(str OptionName, Span<byte> Buffer){
		Buffer.Clear();
		var Count = System.Text.Encoding.UTF8.GetByteCount(OptionName);
		if(Count + 1 > Buffer.Length){
			throw KeysErr.Common.ArgErr__.ToErr(nameof(OptionName));
		}
		System.Text.Encoding.UTF8.GetBytes(OptionName, Buffer);
	}

	[Doc("librime 通知的托管端：只認兩個 option，其餘忽略（狀態每次輸入後都會重抓）。")]
	void OnOptionChanged(str OptionName, bool IsEnabled){
		try{
			switch(OptionName){
				case "ascii_mode":
					IsAsciiMode = IsEnabled;
					break;
				case "simplification":
					IsSimplification = IsEnabled;
					break;
			}
		}catch(Exception Ex){
			AppLog.Error(Ex, "[Rime] option 狀態回寫失敗");
		}
	}

	[Doc("從引擎抓一次完整快照；未連接時先按需初始化，會話失效則清空狀態。")]
	unsafe void SyncStateFromRime(){
		// 按需初始化：服務端可能沒呼叫 ConnectAsy 就直接送鍵（TSF 外殼就是這樣，
		// 按鍵由 RimeKeyProcessor 自己觸發引擎初始化）。若這裡堅持要求已連接，
		// 狀態會永遠停在空的快照上——症狀是「按鍵有效、但 preedit 與候選永遠不出現」。
		var Engine = Setup ??= GetRimeSetup();
		if(Engine is null){
			ClearState();
			return;
		}
		lock(StateLock){
			var Rime = Engine.apiFn;
			var Session = Engine.rimeSessionId;
			if(Session == 0){
				ClearState();
				return;
			}
			// 1: status：組字／英文／簡繁。
			var Status = new RimeStatus{
				data_size = RimeUtil.DataSize<RimeStatus>(),
			};
			if(Rime.get_status(Session, &Status) != RimeUtil.False){
				try{
					IsAsciiMode = Status.is_ascii_mode != RimeUtil.False;
					IsSimplification = Status.is_simplified != RimeUtil.False;
					IsComposing = Status.is_composing != RimeUtil.False;
				}finally{
					Rime.free_status(&Status);
				}
			}
			// 2: context：preedit 與候選。原生 context 只在 free_context 前有效，故這裡複製成托管快照。
			var Context = new RimeContext{
				data_size = RimeUtil.DataSize<RimeContext>(),
			};
			if(Rime.get_context(Session, &Context) != RimeUtil.False){
				try{
					ReplacePreedit(MkDisplayedPreedit(Context.composition));
					ReplaceCandidates(ReadCandidates(&Context));
				}finally{
					Rime.free_context(&Context);
				}
			}else{
				ReplacePreedit("");
				ReplaceCandidates(new());
			}
		}
	}

	[Doc("清空所有狀態；用於未連接或會話失效。")]
	void ClearState(){
		IsConnected = false;
		ReplacePreedit("");
		ReplaceCandidates(new());
		IsComposing = false;
	}

	[Doc("把 composition.preedit 與 cursor_pos 組成要顯示的 preedit；游標用 U+2038 `‸` 表示。")]
	static unsafe str MkDisplayedPreedit(RimeComposition Composition){
		var Preedit = ToolCStr.ToCsStr(Composition.preedit) ?? "";
		if(Preedit.Length == 0){
			return Preedit;
		}
		var Cursor = Composition.cursor_pos;
		if(Cursor < 0){
			Cursor = 0;
		}
		if(Cursor > Preedit.Length){
			Cursor = Preedit.Length;
		}
		return Preedit.Insert(Cursor, "\u2038");
	}

	[Doc("在原生 context 有效期間把候選複製成托管快照（含分頁與高亮）。")]
	static unsafe CandidatePage ReadCandidates(RimeContext* Context){
		var Items = new List<ICandidate>();
		var Menu = Context->menu;
		for(i32 Index = 0; Index < Menu.num_candidates; Index++){
			var One = Menu.candidates[Index];
			Items.Add(new Candidate{
				Text = ToolCStr.ToCsStr(One.text) ?? "",
				Comment = ToolCStr.ToCsStr(One.comment) ?? "",
			});
		}
		return new CandidatePage{
			Data = Items,
			PageIdx = (u64)Menu.page_no,
			PageSize = (u64)Menu.page_size,
			IsLastPage = Menu.is_last_page != RimeUtil.False,
			HighlightedIndex = Menu.highlighted_candidate_index,
		};
	}

	[Doc("釋放時解除訂閱（自身事件與單例的靜態事件），避免已死的服務仍被回調。")]
	public override async ValueTask DisposeAsync(){
		AfterInput -= OnAfterInputSync;
		RimeSetup.OnOptionChanged -= OnOptionChanged;
		await base.DisposeAsync();
	}
}
