namespace Ngan.Ime.UI.Views.StatusTip;

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

[Doc($$"""
{{nameof(ViewStatusTip)}} 的實現：一個黑底白字的小方塊。
""")]
public partial class ViewStatusTip{

	[Doc("與候選浮窗相同的 6px 間距（照 Weasel 的 `OffsetRect(0, 6)`）。")]
	const i32 AnchorGapPx = 6;

	[Doc("建立視窗屬性與控件樹；此時還不顯示。")]
	public partial ViewStatusTip(){
		Topmost = true;
		ShowInTaskbar = false;
		ShowActivated = false;
		CanResize = false;
		WindowDecorations = Avalonia.Controls.WindowDecorations.None;
		SizeToContent = SizeToContent.WidthAndHeight;
		Background = UiCfg.Inst.PopupBgColor;
		TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
		_Text = new TextBlock{
			FontFamily = UiCfg.Inst.FontFamily,
			FontSize = UiCfg.Inst.PopupFontSize,
			Foreground = UiCfg.Inst.PopupForeColor,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		Content = new Border{
			Background = UiCfg.Inst.PopupBgColor,
			Padding = new Thickness(UiCfg.Inst.PopupFontSize * 0.25, 0),
			Child = _Text,
		};
	}

	[Doc("換掉顯示的字。")]
	public partial void SetText(str Text){
		if(_Text is null){
			return;
		}
		_Text.Text = Text;
	}

	[Doc("移到插入點下方；超出工作區就往內收。")]
	public partial void MoveTo(PixelPoint Anchor){
		var size = Bounds.Width > 0 ? Bounds.Size : DesiredSize;
		var width = (i32)Math.Ceiling(size.Width);
		var height = (i32)Math.Ceiling(size.Height);
		var screens = Screens;
		var screen = screens?.ScreenFromPoint(Anchor) ?? screens?.Primary;
		var work = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
		var x = Anchor.X;
		var y = Anchor.Y + AnchorGapPx;
		if(x + width > work.Right){
			x = work.Right - width;
		}
		if(x < work.X){
			x = work.X;
		}
		if(y + height > work.Bottom){
			// 下方放不下就翻到插入點上方（與候選浮窗同一條規則）。
			y = Anchor.Y - height;
		}
		if(y < work.Y){
			y = work.Y;
		}
		Position = new PixelPoint(x, y);
	}
}
