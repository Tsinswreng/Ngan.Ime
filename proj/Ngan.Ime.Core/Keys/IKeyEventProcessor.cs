namespace Ngan.Ime.Core.Keys;


[Doc($$"""
一次按鍵處理的結果。

*為何要回傳而非直接上屏*：管線的多個階段（引擎、未處理鍵轉發）需要知道「這批鍵被吃掉了沒有」，
而「上屏文本」與「該轉發給誰」是兩個獨立事實，故分成兩個集合。
""")]
public interface IRespOnKeyEvent{
	[Doc("本批按鍵產生的上屏文本，依產生順序排列；沒有上屏時為空集合。")]
	public IList<str> Commits{get;set;}

	[Doc("引擎未處理、應轉發給作業系統的按鍵；沒有時為空集合。")]
	public IList<IKeyEvent> UnhandledKeys{get;set;}
}

[Doc($$"""
{{nameof(IRespOnKeyEvent)}} 的默認實現：兩個集合都已初始化，故呼叫方可直接 Add。
""")]
public class RespOnKeyEvent : IRespOnKeyEvent{
	[Doc("上屏文本；默認空集合。")]
	public IList<str> Commits{get;set;} = [];

	[Doc("未處理按鍵；默認空集合。")]
	public IList<IKeyEvent> UnhandledKeys{get;set;} = [];
}

[Doc($$"""
按鍵事件處理器：吃一批按鍵，回報「上屏了什麼」與「哪些要交還給作業系統」。
異步是因為引擎可能真的需要等待（未來的跨進程引擎）；現行 Rime 實現是同步完成的。

#Eg[
	```cs
	IRespOnKeyEvent Resp = await Processor.OnKeyEvents(Events, Ct);
	```
]
""")]
public interface IKeyEventProcessor{
	[Doc($$"""
	處理一批按鍵。
	#Prm[{{nameof(KeyEvents)}}][本批按鍵，依發生順序；只允許消費一次。]
	#Prm[{{nameof(Ct)}}][取消令牌。]
	#Rtn[本批的處理結果。]
	""")]
	public Task<IRespOnKeyEvent> OnKeyEvents(
		IEnumerable<IKeyEvent> KeyEvents, CT Ct
	);
}
