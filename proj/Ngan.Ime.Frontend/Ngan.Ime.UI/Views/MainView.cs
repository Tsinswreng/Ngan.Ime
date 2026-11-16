namespace Ngan.Ime.UI.Views;

using Avalonia.Controls;
using Ngan.Ime.UI.Views.Ime;

[Doc($$"""
界面根視圖：只放一個 {{nameof(ViewIme)}}。

*為何要這一層*：宿主（桌面主視窗、Android Activity、AvaloniaView）只認一個根控件；
把「宿主掛載點」與「輸入法界面」分開，Android 強制恢復時才能整棵樹丟棄重建。

#Eg[
	```cs
	Content = new MainView();
	```
]
""")]
public class MainView : UserControl, IDisposable{

	[Doc("輸入法主視圖。")]
	public ViewIme? ImeView{get; private set;}

	[Doc("建立並放入輸入法主視圖。")]
	public MainView(){
		ImeView = new ViewIme();
		Content = ImeView;
	}

	[Doc("釋放子視圖（取消其事件訂閱）。")]
	public void Dispose(){
		ImeView?.Dispose();
		ImeView = null;
		GC.SuppressFinalize(this);
	}
}
