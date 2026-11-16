namespace Ngan.Ime.Core.Ime;

using Ngan.Ime.Core.Keys;

[Doc($$"""
輸入法的「按鍵入口與連接生命週期」。

*為何要 {{nameof(InputSafely)}} 與 {{nameof(Input)}} 兩個*：
宿主的事件回調（Android 的 `OnKeyDown`、TSF 的原生回調）多數是同步、不能等待、也不該拋；
故 {{nameof(InputSafely)}} 只把批次排進佇列，真正的處理在單一消費迴圈裡串行完成。
需要知道處理結果的場合（測試、宿主主動驅動）才用 {{nameof(Input)}}。

*串行為何重要*：Rime 的 session 不是線程安全的；兩顆鍵並行處理會讓候選與 commit 交錯。
故佇列是「單讀者」，順序即按鍵順序。

#Eg[
	```cs
	Svc.InputSafely([KeyEvent.Mk(KeyChars.Enter, KeyStates.Down)]);
	await Svc.ConnectAsy(Ct);
	```
]
""")]
public interface IImeInput{
	[Doc($$"""
	把一批按鍵排進處理佇列後立刻返回；永不拋（引擎錯誤經 {{nameof(OnError)}} 回報）。
	#Prm[{{nameof(KeyEvents)}}][本批按鍵；只在方法返回前被讀取一次，故可傳惰性序列。]
	#Prm[{{nameof(OnError)}}][本批被拒絕或排隊失敗時的回調；省略則只記日誌。]
	""")]
	public void InputSafely(IEnumerable<IKeyEvent> KeyEvents, Action<Exception>? OnError = null);

	[Doc($$"""
	同步處理一批按鍵並等待完成；失敗時拋出。
	#Prm[{{nameof(KeyEvents)}}][本批按鍵，依發生順序。]
	#Prm[{{nameof(Ct)}}][取消令牌。]
	#Rtn[引擎的回應：上屏文本與「哪些鍵沒被吃掉」。呼叫方據此決定是否把按鍵放行給系統。]
	""")]
	public Task<IRespOnKeyEvent> Input(IEnumerable<IKeyEvent> KeyEvents, CT Ct);

	[Doc("連接／初始化引擎；可重複呼叫，失敗後再呼叫會重試。")]
	public Task ConnectAsy(CT Ct = default);

	[Doc("一批按鍵即將被處理；用於宿主層的除錯與狀態埋點。")]
	public event EventHandler<IEnumerable<IKeyEvent>>? BeforeInput;

	[Doc("一批按鍵處理完畢（commit 已分發、狀態已回填之前後由實現決定）；用於界面刷新。")]
	public event EventHandler<IEnumerable<IKeyEvent>>? AfterInput;

	[Doc("引擎產生上屏文本時觸發；參數即要提交給宿主的文字。")]
	public event EventHandler<str>? OnCommit;

	[Doc("一次連接流程結束時觸發（不論成敗）；參數為是否成功。")]
	public event EventHandler<bool>? ConnectCompleted;
}
