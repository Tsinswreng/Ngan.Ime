namespace Ngan.Ime.UI.Views.KeyBoard;

using Avalonia.Controls;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.Key;

[Doc($$"""
虛擬鍵盤視圖：把 {{nameof(KeyLayout)}} 的資料畫成兩層（字母層／數字層），並切換顯示。

*為何 View 這麼短*：鍵位、手勢、欄寬都在布局資料裡；本類只做兩個迴圈。
新增一種布局只需要在 {{nameof(KeyLayout)}} 加一組 {{nameof(KeyRow)}}，不必改本檔。

*關鍵控件都提為 public 成員*：`KeyViews` 供測試與設計時檢查「這層有幾顆鍵、鍵帽是什麼」。
""")]
public partial class ViewKeyBoard : AppViewBase<VmKeyBoard>, IDisposable{

	[Doc("本視圖建立的每顆鍵；順序與布局資料一致。")]
	public readonly List<ViewKey> KeyViews = [];

	[Doc("根容器：兩層布局疊在同一個 Grid 裡，靠 IsVisible 切換。")]
	public Grid? _Root;

	[Doc("組出兩層鍵盤。")]
	protected override partial object Build(VmKeyBoard vm);

	[Doc("釋放 ViewModel 的事件訂閱與每顆鍵的計時器。")]
	public partial void Dispose();
}
