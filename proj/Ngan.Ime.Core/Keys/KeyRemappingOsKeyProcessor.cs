namespace Ngan.Ime.Core.Keys;


[Doc($$"""
裝飾器：在「把未處理按鍵交給作業系統」之前先套一層映射。

*為何用裝飾器而不是改各平臺實現*：映射是使用者偏好，和平臺發鍵機制無關；
放在這一層，Android／Windows／TSF 三個實現都不必知道映射的存在，
也不必各自維護一份「要不要映射」的分支。

#Eg[
	```cs
	IOsKeyProcessor Wrapped = new KeyRemappingOsKeyProcessor(
		Inner: new AndroidOsKeyProcessor(Runtime)
		,Remapper: Remapper
	);
	```
]
""")]
public class KeyRemappingOsKeyProcessor : IOsKeyProcessor {

	[Doc("真正發鍵的內層實現。")]
	readonly IOsKeyProcessor Inner;

	[Doc("映射器；關閉時原樣放行。")]
	readonly IKeyEventRemapper Remapper;

	[Doc($$"""
	包一層映射。
	#Prm[{{nameof(Inner)}}][真正的發鍵器。]
	#Prm[{{nameof(Remapper)}}][映射器。]
	""")]
	public KeyRemappingOsKeyProcessor(IOsKeyProcessor Inner, IKeyEventRemapper Remapper){
		this.Inner = Inner;
		this.Remapper = Remapper;
	}

	[Doc("錯誤事件原樣轉發給內層；本層不自行產生錯誤。")]
	public event ErrHandler? OnErr {
		add{ Inner.OnErr += value; }
		remove{ Inner.OnErr -= value; }
	}

	[Doc("先映射再轉發；兩者都是惰性序列，不會把整批按鍵載入內存。")]
	public Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> KeyEvents, CT Ct){
		return Inner.OnKeyEvents(Remapper.Remap(KeyEvents), Ct);
	}
}
