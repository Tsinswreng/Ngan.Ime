namespace Ngan.Ime.Core.Keys;


[Doc($$"""
單條按鍵映射規則。

*規則自身只回答「這個事件要不要換」*，總開關與後續轉發由 {{nameof(IKeyEventRemapper)}} 與
{{nameof(KeyRemappingOsKeyProcessor)}} 負責；這樣一條規則可以獨立單測。

#Eg[
	```cs
	if(Rule.TryRemap(Event, out var Mapped)){ /* 用 Mapped 取代 Event */ }
	```
]
""")]
public interface IKeyEventRemapRule{
	[Doc("本規則關注的來源鍵名；映射器用它建索引，避免每次按鍵線性掃規則表。")]
	public str SourceKeyName{get;}

	[Doc($$"""
	嘗試把一個事件映射成新的事件序列。
	#Prm[{{nameof(SourceEvent)}}][原始事件。]
	#Prm[{{nameof(RemappedEvents)}}][映射結果；回傳 false 時為空序列。]
	#Rtn[命中並改寫時 true。]
	""")]
	public bool TryRemap(IKeyEvent SourceEvent, out IEnumerable<IKeyEvent> RemappedEvents);
}
