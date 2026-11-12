namespace Ngan.Ime.UI.Views;

using Avalonia.Controls;
using Avalonia.Media;
using Tsinswreng.CsCore;

[Doc($$"""
桌面宿主的主視窗：只有一個 {{nameof(MainView)}}。

*尺寸*：預覽用的小窗（480×270），真正的輸入法界面在 Android 與 TSF 候選窗裡；
桌面窗只是本機開發時看界面用，故不保存尺寸、不做多螢幕適配。

#Eg[
	```cs
	window.Content = new MainView();
	```
]
""")]
public class MainWindow : Window{

	[Doc("預覽視窗寬度（px）。")]
	public const i32 PreviewWidth = 480;

	[Doc("預覽視窗高度（px）。")]
	public const i32 PreviewHeight = 270;

	[Doc("建立視窗並放入根視圖。")]
	public MainWindow(){
		Title = "Ngan.Ime";
		Width = PreviewWidth;
		Height = PreviewHeight;
		MinWidth = 0;
		MinHeight = 0;
		WindowDecorations = WindowDecorations.Full;
		Content = new MainView();
		Background = Brushes.Black;
	}
}
