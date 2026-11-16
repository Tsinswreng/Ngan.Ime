namespace Ngan.Ime.Core.Keys;


[Doc($$"""
{{nameof(IKeyChar)}} 的默認實現。

*為何重寫 {{nameof(Equals)}}／{{nameof(GetHashCode)}}*：按鍵在專案裡被當成字典鍵
（`Dictionary<IKeyChar, i64>`）與集合元素（{{nameof(KeyBoardState.AllDownKeys)}}）。
若按引用比較，`new KeyChar("a")` 與 `KeyChars.LowerA` 就是兩個鍵，
跨宿主（TSF 管道、Android 輸入連接、Rime 轉碼）傳來的鍵名就無法命中映射表。
故這裡按 {{nameof(Name)}} 做值比較；這也是「鍵名即身份」這條約定的落點。

#Eg[
	```cs
	var K = new KeyChar("a");
	K.Equals(KeyChars.LowerA);        // true
	KeyChars.LowerA.Name;             // "a"
	```
]
""")]
public class KeyChar : IKeyChar {

	[Doc(@"鍵名；默認空串。大小寫敏感。")]
	public str Name{get;set;} = "";

	[Doc("建立一個未命名的鍵；只供反序列化與對象初始化器使用。")]
	public KeyChar(){}

	[Doc($$"""
	按名字建立一個鍵。
	#Prm[{{nameof(Name)}}][鍵名，見 {{nameof(KeyChars)}} 的全表。]
	""")]
	public KeyChar(str Name){
		this.Name = Name;
	}

	[Doc($$"""
	按名字建立一個鍵的工廠。等價於 `new KeyChar(Name)`，供舊呼叫點使用。
	#Prm[{{nameof(Name)}}][鍵名。]
	#Rtn[新的 {{nameof(IKeyChar)}} 實例；與同名的既有鍵相等。]
	""")]
	public static IKeyChar Mk(str Name){
		return new KeyChar(Name);
	}

	[Doc("按鍵名比較；兩個實現只要名字相同就是同一個鍵。")]
	public override bool Equals(obj? O){
		return O is IKeyChar K && string.Equals(K.Name, Name, StringComparison.Ordinal);
	}

	[Doc("與 Equals 一致的哈希：只用鍵名。")]
	public override i32 GetHashCode(){
		return StringComparer.Ordinal.GetHashCode(Name);
	}

	[Doc("日誌與測試訊息裡直接顯示鍵名。")]
	public override str ToString(){
		return Name;
	}
}
