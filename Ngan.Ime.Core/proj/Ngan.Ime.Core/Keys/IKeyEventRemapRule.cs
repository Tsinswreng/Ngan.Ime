namespace Ngan.Ime.Core.Keys;

/// <summary>
/// 單條按鍵映射規則。
/// 規則自身只關心“某個事件是否需要替換”，不負責總體開關與後續轉發。
/// </summary>
public interface IKeyEventRemapRule
{
	/// <summary>
	/// 此規則關注的來源鍵名。
	/// 映射器會用它預先建立索引，避免每次按鍵都線性掃描所有規則。
	/// </summary>
	public str SourceKeyName{get;}

	/// <summary>
	/// 嘗試將一個按鍵事件映射成新的事件序列。
	/// </summary>
	public bool TryRemap(IKeyEvent SourceEvent, out IEnumerable<IKeyEvent> RemappedEvents);
}
