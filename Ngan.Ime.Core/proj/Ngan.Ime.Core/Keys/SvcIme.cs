namespace Ngan.Ime.Core.Keys;
using System.Threading.Channels;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.Core.Ime;
using CommunityToolkit.Mvvm.ComponentModel;
using Tsinswreng.CsPage;

[Doc(@$"一頁候選詞，包含候選列表與分頁訊息。
為引擎單頁候選的快照，含頁號、是否末頁、頁大小、高亮索引。")]
public class CandidatePage:IPage<ICandidate>{

	/// 當前頁內高亮候選的索引
	public int HighlightedIndex{get;set;}
	public bool IsLastPage{get;set;}
	#region ICandidate
	public IList<ICandidate>? Data{get;set;} = [];
	public u64 PageSize{get;set;}
	public u64 TotCnt { get;set; }
	/// from 0
	public u64 PageIdx { get;set; }
	public bool HasTotCnt { get;set; }
	#endregion ICandidate
}


[Doc(@$"輸入法引擎抽象與適配。
外部訪問其API當由{nameof(ISvcIme)}引用。
提供按鍵管線、狀態屬性與事件；具體的 Rime 引擎連接與狀態同步由 Ngan.Ime.Rime.SvcIme 實現。
")]
public abstract class ISvcIme
	:ObservableObject
{
	public IDictionary<object, object?> Cfg{
		get;
		set => SetProperty(ref field, value);
	} = new Dictionary<object, object?>();

	public IOsKeyProcessor? OsKeyProcessor{
		get;
		set => SetProperty(ref field, value);
	}

	public IImeKeyProcessor? ImeKeyProcessor{
		get;
		set => SetProperty(ref field, value);
	}

	public bool IsConnected{
		get;
		set => SetProperty(ref field, value);
	}

	public bool IsConnecting{
		get;
		set => SetProperty(ref field, value);
	}

	public bool IsAsciiMode{
		get;
		set => SetProperty(ref field, value);
	}

	public bool IsSimplification{
		get;
		set => SetProperty(ref field, value);
	}

	public bool IsComposing{
		get;
		set => SetProperty(ref field, value);
	}

	[Obsolete("不應出現在此層")]
	public str StatusText{
		get;
		set => SetProperty(ref field, value);
	} = "Ime 未連接";

	public str Preedit{
		get;
		set => SetProperty(ref field, value);
	} = "";

	public CandidatePage Candidates{
		get;
		set => SetProperty(ref field, value);
	} = new();

	readonly Channel<IEnumerable<IKeyEvent>> _KeyChannel = Channel.CreateUnbounded<IEnumerable<IKeyEvent>>();
	readonly CancellationTokenSource _ChannelCts = new();

	protected ISvcIme(IOsKeyProcessor osKeyProcessor, IImeKeyProcessor imeKeyProcessor) {
		this.OsKeyProcessor = osKeyProcessor;
		this.ImeKeyProcessor = imeKeyProcessor;
		_InitChannelConsumer();
	}

	protected ISvcIme(IOsKeyProcessor osKeyProcessor) {
		this.OsKeyProcessor = osKeyProcessor;
		_InitChannelConsumer();
	}

	protected ISvcIme() {
		_InitChannelConsumer();
	}

	public void InputSafely(IEnumerable<IKeyEvent> keyEvents, Action<Exception>? onError = null){
		if(!_KeyChannel.Writer.TryWrite(keyEvents)){
			var ex = new InvalidOperationException("[ImeState] Key channel is closed");
			AppLog.Error(ex, "");
			onError?.Invoke(ex);
		}
	}

	void _InitChannelConsumer(){
		_ = ConsumeKeysLoop();
	}

	async Task ConsumeKeysLoop(){
		try{
			await foreach(var keyEvents in _KeyChannel.Reader.ReadAllAsync(_ChannelCts.Token)){
				try{
					await Input(keyEvents, _ChannelCts.Token);
				}catch(OperationCanceledException){
					break;
				}catch(Exception ex){
					AppLog.Error(ex, "[ImeState] Channel consumer Input error");
				}
			}
		}catch(OperationCanceledException){
			// 通道取消，正常退出
		}
	}

	public async Task<RespInput> Input(IEnumerable<IKeyEvent> keyEvents, CT Ct){
		var sw = System.Diagnostics.Stopwatch.StartNew();
		AppLog.Debug($"[Perf] ImeState.Input start: {sw.ElapsedMilliseconds}ms");
		BeforeInput?.Invoke(this, keyEvents);
		IRespOnKeyEvent? resp = null;
		if(ImeKeyProcessor is not null){
			var swProc = System.Diagnostics.Stopwatch.StartNew();
			resp = await ImeKeyProcessor.OnKeyEvents(keyEvents, Ct);
			AppLog.Debug($"[Perf] ImeState.Input OnKeyEventsAsy done: {swProc.ElapsedMilliseconds}ms");
		}
		// 先把 commit 文字交給宿主上屏，再回填 Ngan.Ime 自己的狀態與候選。
		// 否則空格選詞後，宿主上屏會被 AfterInput 里的同步狀態抓取拖後，
		// 體感上就會變成「上屏」和「新的聯想候選彈出」幾乎同時發生。
		if(resp?.Commits is not null){
			var swCommitDispatch = System.Diagnostics.Stopwatch.StartNew();
			foreach(var commitText in resp.Commits){
				var swOneCommit = System.Diagnostics.Stopwatch.StartNew();
				OnCommit?.Invoke(this, commitText);
				AppLog.Debug($"[Perf] ImeState.Input OnCommit dispatched: {swOneCommit.ElapsedMilliseconds}ms, text: {commitText}");
			}
			AppLog.Debug($"[Perf] ImeState.Input OnCommit total dispatched: {swCommitDispatch.ElapsedMilliseconds}ms, count: {resp.Commits.Count}");
		}

		var swAfter = System.Diagnostics.Stopwatch.StartNew();
		AfterInput?.Invoke(this, keyEvents);
		AppLog.Debug($"[Perf] ImeState.Input AfterInput done: {swAfter.ElapsedMilliseconds}ms");

		// 未處理按鍵轉發給 OS
		if(resp?.UnhandledKeys is not null && resp.UnhandledKeys.Count > 0 && OsKeyProcessor is not null){
			var swOs = System.Diagnostics.Stopwatch.StartNew();
			await OsKeyProcessor.OnKeyEvents(resp.UnhandledKeys, Ct);
			AppLog.Debug($"[Perf] ImeState.Input OsKeyProcessor done: {swOs.ElapsedMilliseconds}ms, unhandled: {resp.UnhandledKeys.Count}");
		}

		AppLog.Debug($"[Perf] ImeState.Input total done: {sw.ElapsedMilliseconds}ms");
		return new();
	}

	public virtual Task ConnectAsy(CT ct = default){
		IsConnected = ImeKeyProcessor is not null;
		if(IsConnected && string.IsNullOrWhiteSpace(StatusText)){
			StatusText = "Ime 已連接";
		}
		return Task.CompletedTask;
	}

	public virtual Task ToggleAsciiModeAsy(CT ct = default){
		IsAsciiMode = !IsAsciiMode;
		return Task.CompletedTask;
	}

	public virtual Task ToggleSimplificationAsy(CT ct = default){
		IsSimplification = !IsSimplification;
		return Task.CompletedTask;
	}

	public void ReplaceCandidates(CandidatePage page){
		Candidates = page;
	}

	public void ReplacePreedit(str? preedit){
		Preedit = preedit ?? "";
	}

	public event EventHandler<IEnumerable<IKeyEvent>>? BeforeInput;
	public event EventHandler<IEnumerable<IKeyEvent>>? AfterInput;

	/// 當 Rime 引擎 commit 文字時觸發。參數為 commit 的文字內容。
	public event EventHandler<string>? OnCommit;

	/// <summary>
	/// 當後端初始化/連接流程結束時觸發。
	/// 不論成功或失敗都會發出，供 UI 在“初始化完成”這個時機做視圖切換。
	/// 參數為本次連接是否成功。
	/// </summary>
	public event EventHandler<bool>? ConnectCompleted;

	/// <summary>
	/// 子類在完成一次初始化/連接流程後調用。
	/// 這裡只負責發事件，不摻雜具體 UI 邏輯，避免 Core 反向依賴前端。
	/// </summary>
	protected void RaiseConnectCompleted(bool IsSuccess)
	{
		ConnectCompleted?.Invoke(this, IsSuccess);
	}

}
