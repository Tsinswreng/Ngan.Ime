namespace Ngan.Ime.Core.Keys;

/// <summary>
/// 最基礎的單鍵對單鍵映射規則。
/// 目前先覆蓋“手機輸入法退格映射為右 Alt”這類場景，後續可繼續擴展更多規則實現。
/// </summary>
public class KeyRemapRule
	: IKeyEventRemapRule
{
	public IKeyChar SourceKey { get; }
	public IKeyChar TargetKey { get; }
	public str SourceKeyName => SourceKey.Name;

	public KeyRemapRule(IKeyChar SourceKey, IKeyChar TargetKey)
	{
		this.SourceKey = SourceKey;
		this.TargetKey = TargetKey;
	}

	/// <summary>
	/// 命中來源鍵時，保留原本的 down/up 狀態與鍵盤上下文，只替換鍵值本身。
	/// </summary>
	public bool TryRemap(IKeyEvent SourceEvent, out IEnumerable<IKeyEvent> RemappedEvents)
	{
		if(SourceEvent.KeyChar.Name != SourceKeyName){
			RemappedEvents = [];
			return false;
		}

		RemappedEvents = [
			new KeyEvent{
				KeyChar = TargetKey,
				KeyState = SourceEvent.KeyState,
				KeyBoardState = SourceEvent.KeyBoardState,
			}
		];
		return true;
	}
}
