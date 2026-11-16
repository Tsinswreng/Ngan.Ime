namespace Ngan.Ime.Core.Keys;


[Doc($$"""
把一批「即將交給 OS」的按鍵事件做二次映射。

*職責邊界*：只處理引擎已明確標記為未處理的按鍵，不介入引擎本身的收鍵；
故它是 {{nameof(IOsKeyProcessor)}} 外面的一層包裝（見 {{nameof(KeyRemappingOsKeyProcessor)}}），
而不是引擎的裝飾器。

#Eg[
	```cs
	IEnumerable<IKeyEvent> Mapped = Remapper.Remap(Unhandled);
	```
]
""")]
public interface IKeyEventRemapper{
	[Doc($$"""
	依當前規則把輸入事件映射成另一批事件；允許 1 對 1，也允許 1 對多。
	#Prm[{{nameof(KeyEvents)}}][待映射的事件，依序；只允許消費一次。]
	#Rtn[映射後的事件序列（惰性）。]
	""")]
	public IEnumerable<IKeyEvent> Remap(IEnumerable<IKeyEvent> KeyEvents);
}
