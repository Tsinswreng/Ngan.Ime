using Ngan.Ime.Core.Infra;
using Ngan.Ime.UI.Infra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ngan.Ime.UI.Views.RimeLog;
using Ctx = VmRimeLog;

public class ViewRimeLog : AppViewBase<Ctx>
	, IDisposable
{
	public ViewRimeLog()
	{
		Ctx = Di.DiOrMk<Ctx>();
		Render();
	}

	void Render()
	{
		var items = new ItemsControl();
		items.SetItemTemplate<string>((line, _) => {
			return new TextBlock{
				Text = line,
				Foreground = Brushes.White,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(10, 6),
				FontSize = UiCfg.Inst.BaseFontSize,
			};
		}).SetItemsPanel(() => new StackPanel{
			Orientation = Orientation.Vertical,
			Spacing = 2,
		});
		Ctx.Bind(items, ItemsControl.ItemsSourceProperty, x => x.Lines);

		this.SetContent(new ScrollViewer(), o=>{
			o.Background = Brushes.Black;
			o.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			o.SetContent(items);
		});
	}

	public void Dispose()
	{
		(Ctx as IDisposable)?.Dispose();
	}
}
