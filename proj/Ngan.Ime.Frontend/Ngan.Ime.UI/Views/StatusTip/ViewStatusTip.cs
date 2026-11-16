namespace Ngan.Ime.UI.Views.StatusTip;

using Avalonia;
using Avalonia.Controls;
using Tsinswreng.CsCore;

[Doc($$"""
狀態提示的視窗：無邊框、置頂、不搶焦點，貼着插入點顯示單字。

*為何是 {{nameof(Window)}}*：它必須浮在別的應用上面，而我們沒有自己的頂層視窗可依附；
`UserControl` 只能在我們自己的視窗裡顯示。
""")]
public partial class ViewStatusTip : Window{

	[Doc("顯示文字的控件。")]
	public TextBlock? _Text;

	[Doc("建立視窗與控件樹；此時還不顯示。")]
	public partial ViewStatusTip();

	[Doc("換掉顯示的字。")]
	public partial void SetText(str Text);

	[Doc("移到插入點下方（間距與候選浮窗相同）；超出工作區就往內收。")]
	public partial void MoveTo(PixelPoint Anchor);
}
