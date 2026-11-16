namespace Ngan.Ime.UI.Infra;

using Avalonia.Controls;

[Doc($$"""
Grid 的簡寫：用一個字符串宣告行／列定義。

*為何需要*：`new RowDefinitions("Auto,*,Auto")` 每次都要記住型別名，讀起來也蓋過了佈局本身；
`Root.Rows("Auto,*,Auto")` 更接近「這一頁有三段」的意圖。純語法糖，不改變任何行為。

#Eg[
	```cs
	return new StackGrid(IsRow: true).Rows("Auto,*,Auto").Children(Header, Body, Footer);
	```
]
""")]
public static class GridExtn{

	[Doc($$"""
	設定行定義並回傳同一個容器，便於串接。
	#Prm[{{nameof(Defs)}}][行定義字符串，語法同 Avalonia 的 `RowDefinitions`，例如 `"Auto,*,2*"`。]
	#Rtn[同一個容器。]
	""")]
	public static TSelf Rows<TSelf>(this TSelf z, str Defs) where TSelf : Grid{
		z.RowDefinitions = new RowDefinitions(Defs);
		return z;
	}

	[Doc("設定列定義並回傳同一個容器，便於串接；語法同 `ColumnDefinitions`。")]
	public static TSelf Cols<TSelf>(this TSelf z, str Defs) where TSelf : Grid{
		z.ColumnDefinitions = new ColumnDefinitions(Defs);
		return z;
	}
}
