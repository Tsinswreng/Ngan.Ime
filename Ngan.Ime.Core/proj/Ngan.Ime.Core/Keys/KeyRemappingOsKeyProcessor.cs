namespace Ngan.Ime.Core.Keys;

/// <summary>
/// 包裝真正的 OS 按鍵發送器，在轉發前先套用未處理按鍵映射。
/// 這樣 Rime 的收鍵語義保持原樣，而宿主層仍可按需修正最終發給系統的鍵值。
/// </summary>
public class KeyRemappingOsKeyProcessor
	: IOsKeyProcessor
{
	readonly IOsKeyProcessor _Inner;
	readonly IKeyEventRemapper _Remapper;

	public KeyRemappingOsKeyProcessor(IOsKeyProcessor Inner, IKeyEventRemapper Remapper)
	{
		_Inner = Inner;
		_Remapper = Remapper;
	}

	public event ErrHandler? OnErr {
		add{_Inner.OnErr += value;}
		remove{_Inner.OnErr -= value;}
	}

	/// <summary>
	/// 只對“即將交給 OS 的未處理按鍵”做映射，映射完成後立刻交回內層宿主發送器。
	/// </summary>
	public Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> KeyEvents, CT Ct)
	{
		var remappedEvents = _Remapper.Remap(KeyEvents);
		return _Inner.OnKeyEvents(remappedEvents, Ct);
	}
}
