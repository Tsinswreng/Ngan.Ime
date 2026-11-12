namespace Ngan.Ime.Core.Ime;

using System.Threading.Channels;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
輸入法服務的抽象基類：狀態（{{nameof(IImeState)}}）＋按鍵入口（{{nameof(IImeInput)}}）＋選項（{{nameof(IImeOptions)}}）。

*為何是抽象類而不是介面*：這裡有可複用的**機制**（單讀者佇列、事件分發、狀態回填），
不只是契約；契約已由上面三個介面表達，具體引擎只需實現 {{nameof(ConnectAsy)}} 與
{{nameof(ToggleAsciiModeAsy)}}／{{nameof(ToggleSimplificationAsy)}}。這是「類繼承只作複用、不作抽象」的用法。

*為何不在建構子啟動消費迴圈*：建構子有副作用會讓「造一個對象」等於「啟動一條背景任務」，
測試與設計器預覽都會被拖下水。故改成第一次 {{nameof(InputSafely)}}／{{nameof(Input)}} 時惰性啟動
（也可由宿主顯式呼叫 {{nameof(Start)}}）。

*佇列背壓*：佇列有界（{{nameof(KeyQueueCapacity)}}）。滿了就丟最舊的一批並記 Warn，
而不是無限增長——按鍵是即時輸入，積壓幾百批之後再補處理沒有意義。
""")]
public abstract class SvcImeBase
	: ObservableObjectBase
	, IImeState
	, IImeInput
	, IImeOptions
	, IAsyncDisposable
{

	[Doc("按鍵佇列容量。256 批足以覆蓋一次長按連發（每 50ms 一批）而不至於積壓成延遲。")]
	public const i32 KeyQueueCapacity = 256;

	[Doc("單讀者佇列：保證按鍵按順序、且同時只有一個引擎調用。")]
	readonly Channel<IEnumerable<IKeyEvent>> KeyQueue =
		Channel.CreateBounded<IEnumerable<IKeyEvent>>(new BoundedChannelOptions(KeyQueueCapacity){
			SingleReader = true,
			SingleWriter = false,
			FullMode = BoundedChannelFullMode.DropOldest,
		});

	[Doc("取消源；Dispose 時取消消費迴圈。")]
	readonly CancellationTokenSource LoopCts = new();

	[Doc("消費迴圈任務；惰性啟動，Dispose 時等待結束。")]
	Task? LoopTask;

	[Doc("啟動鎖：多個宿主線程同時首次送鍵時只啟動一次。")]
	readonly Lock StartLock = new();

	[Doc("溢位告警節流：一秒內最多記一條，避免刷屏。")]
	long LastOverflowWarnTick;

	[Doc("引擎按鍵處理器；null 表示引擎尚未接入（骨架宿主會用 null 搭配空狀態）。")]
	public IImeKeyProcessor? ImeKeyProcessor{get;protected set;}

	[Doc("未處理按鍵的作業系統轉發器；null 表示本宿主不轉發（例如純預覽宿主）。")]
	public IOsKeyProcessor? OsKeyProcessor{get;protected set;}

	[Doc("建立帶引擎處理器的實例。")]
	protected SvcImeBase(IOsKeyProcessor? OsKeyProcessor, IImeKeyProcessor? ImeKeyProcessor){
		this.OsKeyProcessor = OsKeyProcessor;
		this.ImeKeyProcessor = ImeKeyProcessor;
	}

	[Doc("建立沒有引擎處理器的實例；供預覽／測試宿主使用。")]
	protected SvcImeBase(){}

	#region IImeState

	[Doc("引擎是否已連接。")]
	public bool IsConnected{
		get;
		protected set{ SetProperty(ref field, value); }
	}

	[Doc("是否正在連接。")]
	public bool IsConnecting{
		get;
		protected set{ SetProperty(ref field, value); }
	}

	[Doc("是否英文模式。")]
	public bool IsAsciiMode{
		get;
		protected set{ SetProperty(ref field, value); }
	}

	[Doc("是否簡體模式。")]
	public bool IsSimplification{
		get;
		protected set{ SetProperty(ref field, value); }
	}

	[Doc("是否正在組字。")]
	public bool IsComposing{
		get;
		protected set{ SetProperty(ref field, value); }
	}

	[Doc("預編輯字串。")]
	public str Preedit{
		get;
		protected set{ SetProperty(ref field, value); }
	} = "";

	[Doc("候選頁快照；永不為 null。")]
	public CandidatePage Candidates{
		get;
		protected set{ SetProperty(ref field, value); }
	} = new();

	#endregion

	#region IImeInput

	[Doc("一批按鍵即將處理。")]
	public event EventHandler<IEnumerable<IKeyEvent>>? BeforeInput;

	[Doc("一批按鍵處理完畢。")]
	public event EventHandler<IEnumerable<IKeyEvent>>? AfterInput;

	[Doc("引擎產生上屏文本。")]
	public event EventHandler<str>? OnCommit;

	[Doc("一次連接流程結束。")]
	public event EventHandler<bool>? ConnectCompleted;

	[Doc("確保消費迴圈已啟動；可重複呼叫。")]
	public void Start(){
		lock(StartLock){
			LoopTask ??= Task.Run(ConsumeLoopAsy);
		}
	}

	[Doc("把按鍵批次排進佇列並立刻返回；佇列關閉或溢位時回報錯誤。")]
	public void InputSafely(IEnumerable<IKeyEvent> KeyEvents, Action<Exception>? OnError = null){
		Start();
		// 溢位偵測：DropOldest 模式不會讓 TryWrite 失敗，故先看佇列長度。讀到的長度可能已過時，
		// 但這個值只用來決定「要不要記一條 Warn」，不需要精確。
		if(KeyQueue.Reader.Count >= KeyQueueCapacity){
			WarnOverflow();
		}
		if(!KeyQueue.Writer.TryWrite(KeyEvents)){
			var Ex = KeysErr.Ime.KeyQueueClosed.ToErr();
			AppLog.Error(Ex, "[IME] 按鍵佇列已關閉，該批按鍵被丟棄");
			OnError?.Invoke(Ex);
		}
	}

	[Doc("同步處理一批按鍵（不經佇列）並回傳引擎回應；失敗時拋。")]
	public async Task<IRespOnKeyEvent> Input(IEnumerable<IKeyEvent> KeyEvents, CT Ct){
		return await InputCoreAsy(KeyEvents, Ct);
	}

	[Doc($$"""
	一次按鍵處理的完整流程；{{nameof(InputSafely)}} 的消費迴圈與 {{nameof(Input)}} 都走這裡。
	#Prm[{{nameof(KeyEvents)}}][本批按鍵，依序。]
	#Prm[{{nameof(Ct)}}][取消令牌。]
	""")]
	protected async Task<IRespOnKeyEvent> InputCoreAsy(IEnumerable<IKeyEvent> KeyEvents, CT Ct){
		RaiseSafely(BeforeInput, KeyEvents, nameof(BeforeInput));
		IRespOnKeyEvent? Resp = null;
		if(ImeKeyProcessor is not null){
			Resp = await ImeKeyProcessor.OnKeyEvents(KeyEvents, Ct);
		}
		// 先分發 commit：宿主上屏不該被「回填狀態」拖後，否則體感上是上屏與新候選同時發生。
		if(Resp is not null){
			foreach(var Text in Resp.Commits){
				RaiseSafely(OnCommit, Text, nameof(OnCommit));
			}
		}
		RaiseSafely(AfterInput, KeyEvents, nameof(AfterInput));
		if(Resp is not null && Resp.UnhandledKeys.Count > 0 && OsKeyProcessor is not null){
			await OsKeyProcessor.OnKeyEvents(Resp.UnhandledKeys, Ct);
		}
		return Resp ?? new RespOnKeyEvent();
	}

	[Doc("連接引擎；默認實現只是把「有處理器」當成已連接，具體引擎覆寫它做真正的初始化。")]
	public virtual async Task ConnectAsy(CT Ct = default){
		IsConnected = ImeKeyProcessor is not null;
		await Task.CompletedTask;
		RaiseConnectCompleted(IsConnected);
	}

	#endregion

	#region IImeOptions

	[Doc("切換英文模式；默認只翻狀態位，具體引擎覆寫它去驅動引擎。")]
	public virtual async Task<nil> ToggleAsciiModeAsy(CT Ct = default){
		IsAsciiMode = !IsAsciiMode;
		await Task.CompletedTask;
		return NIL;
	}

	[Doc("切換簡繁；默認只翻狀態位。")]
	public virtual async Task<nil> ToggleSimplificationAsy(CT Ct = default){
		IsSimplification = !IsSimplification;
		await Task.CompletedTask;
		return NIL;
	}

	#endregion

	[Doc("子類在完成一次連接流程後呼叫；只負責發事件，不摻 UI 邏輯。")]
	protected void RaiseConnectCompleted(bool IsSuccess){
		RaiseSafely(ConnectCompleted, IsSuccess, nameof(ConnectCompleted));
	}

	[Doc("替換候選頁並發通知；只能在引擎消費迴圈或 UI 線程呼叫。")]
	protected void ReplaceCandidates(CandidatePage Page){
		Candidates = Page;
	}

	[Doc("替換預編輯文本並發通知；null 視為空串。")]
	protected void ReplacePreedit(str? Text){
		Preedit = Text ?? "";
	}

	[Doc("消費迴圈：逐批取出並串行處理；單批失敗只記日誌，不讓迴圈退出。")]
	async Task ConsumeLoopAsy(){
		try{
			await foreach(var KeyEvents in KeyQueue.Reader.ReadAllAsync(LoopCts.Token)){
				try{
					await InputCoreAsy(KeyEvents, LoopCts.Token);
				}catch(OperationCanceledException){
					break;
				}catch(Exception Ex){
					AppLog.Error(Ex, "[IME] 按鍵處理失敗，該批被跳過");
				}
			}
		}catch(OperationCanceledException){
			// 正常關閉。
		}
	}

	[Doc($$"""
	安全地觸發事件：任一訂閱者拋例外只記日誌，不影響其他訂閱者與後續管線。
	#Prm[{{nameof(Handler)}}][事件。]
	#Prm[{{nameof(Arg)}}][參數。]
	#Prm[{{nameof(EventName)}}][事件名，只為日誌。]
	""")]
	void RaiseSafely<T>(EventHandler<T>? Handler, T Arg, str EventName){
		if(Handler is null){
			return;
		}
		foreach(var One in Handler.GetInvocationList()){
			try{
				((EventHandler<T>)One)(this, Arg);
			}catch(Exception Ex){
				AppLog.Error(Ex, $"[IME] {EventName} 的訂閱者拋出例外");
			}
		}
	}

	[Doc("佇列溢位告警；一秒內最多一條。")]
	void WarnOverflow(){
		var Now = Environment.TickCount64;
		if(Now - LastOverflowWarnTick < 1000){
			return;
		}
		LastOverflowWarnTick = Now;
		AppLog.Warn($"[IME] 按鍵佇列已滿（容量 {KeyQueueCapacity}），丟棄最舊的批次");
	}

	[Doc("停止消費迴圈並釋放資源；可重複呼叫。")]
	public virtual async ValueTask DisposeAsync(){
		LoopCts.Cancel();
		KeyQueue.Writer.TryComplete();
		var Loop = LoopTask;
		if(Loop is not null){
			try{
				await Loop;
			}catch(OperationCanceledException){
				// 正常關閉。
			}
		}
		LoopCts.Dispose();
		GC.SuppressFinalize(this);
	}
}
