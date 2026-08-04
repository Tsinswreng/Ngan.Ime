namespace Ngan.Ime.Core.Keys;

/// <summary>
/// OS 按鍵映射器。
/// 它只在開關打開時套用規則，否則原樣透傳，避免把 UI 狀態耦合進具體宿主發送器。
/// </summary>
public class KeyEventRemapper
	: IKeyEventRemapper
{
	readonly Func<bool> _IsEnabled;
	readonly IReadOnlyDictionary<str, IKeyEventRemapRule> _RulesBySourceKeyName;

	public KeyEventRemapper(Func<bool> IsEnabled, IEnumerable<IKeyEventRemapRule> Rules)
	{
		_IsEnabled = IsEnabled;
		_RulesBySourceKeyName = Rules.ToDictionary(x => x.SourceKeyName);
	}

	/// <summary>
	/// 開關打開後，先按來源鍵名做 O(1) 索引，再交給對應規則完成映射。
	/// 因此單個事件不再線性掃描整張規則表。
	/// </summary>
	public IEnumerable<IKeyEvent> Remap(IEnumerable<IKeyEvent> KeyEvents)
	{
		if(!_IsEnabled()){
			return KeyEvents;
		}

		return RemapCore(KeyEvents);
	}

	IEnumerable<IKeyEvent> RemapCore(IEnumerable<IKeyEvent> KeyEvents)
	{
		foreach(var keyEvent in KeyEvents){
			if(_RulesBySourceKeyName.TryGetValue(keyEvent.KeyChar.Name, out var rule)
				&& rule.TryRemap(keyEvent, out var remappedEvents)){
				foreach(var remappedEvent in remappedEvents){
					yield return remappedEvent;
				}
				continue;
			}

			yield return keyEvent;
		}
	}
}
