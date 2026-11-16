namespace Ngan.Ime.Core.Keys;


[Doc($$"""
最基本的單鍵對單鍵映射規則。

*現行用途*：手機輸入法的退格 → 右 Alt（部分宿主對退格與 Alt 的處理不同）。
規則保留原本的按下/擡起狀態與鍵盤上下文，只換鍵值本身。

#Eg[
	```cs
	var Rule = new KeyRemapRule(KeyChars.Backspace, KeyChars.Alt_R);
	```
]
""")]
public class KeyRemapRule : IKeyEventRemapRule {

	[Doc("來源鍵。")]
	public IKeyChar SourceKey { get; }

	[Doc("目標鍵。")]
	public IKeyChar TargetKey { get; }

	[Doc("來源鍵的鍵名；映射器用它建索引。")]
	public str SourceKeyName => SourceKey.Name;

	[Doc($$"""
	建一條映射規則。
	#Prm[{{nameof(SourceKey)}}][來源鍵。]
	#Prm[{{nameof(TargetKey)}}][目標鍵。]
	""")]
	public KeyRemapRule(IKeyChar SourceKey, IKeyChar TargetKey){
		this.SourceKey = SourceKey;
		this.TargetKey = TargetKey;
	}

	[Doc("命中來源鍵時回一份只換了鍵值的新事件；其餘欄位原樣保留。")]
	public bool TryRemap(IKeyEvent SourceEvent, out IEnumerable<IKeyEvent> RemappedEvents){
		if(!SourceKey.Equals(SourceEvent.KeyChar)){
			RemappedEvents = [];
			return false;
		}
		RemappedEvents = [
			new KeyEvent{
				KeyChar = TargetKey,
				KeyState = SourceEvent.KeyState,
				KeyBoardState = SourceEvent.KeyBoardState,
			},
		];
		return true;
	}
}
