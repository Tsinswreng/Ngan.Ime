namespace Ngan.Ime.Core.Keys;

/// <summary>
/// 將一批即將交給 OS 的按鍵事件做二次映射。
/// 這一層只處理 Rime 已明確標記為未處理的按鍵，不介入輸入法引擎本身的收鍵。
/// </summary>
public interface IKeyEventRemapper
{
	/// <summary>
	/// 依照當前規則把輸入事件映射成另一批事件。
	/// 允許 1 對 1，也允許未來擴展成 1 對多。
	/// </summary>
	public IEnumerable<IKeyEvent> Remap(IEnumerable<IKeyEvent> KeyEvents);
}
