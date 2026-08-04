using Ngan.Ime.Core.Infra;
using Ngan.Ime.UI.Infra;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ngan.Ime.UI.Views.Clipboard;
using Ctx = VmClipboard;

public class ViewClipboard : AppViewBase<Ctx>
	, IDisposable
{
	public ViewClipboard(){
		Ctx = Di.DiOrMk<Ctx>();
		Render();
	}

	void Render(){
		var items = new ItemsControl();
		items.SetItemTemplate<VmClipboardItem>((vm, _) => {
			var btn = new Button{
				Background = Brushes.Black,
				Foreground = Brushes.White,
				CornerRadius = new(0),
				BorderBrush = UiCfg.Inst.GapLineBrush,
				BorderThickness = new(0.5),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Left,
				Padding = new(12, 10),
			};
			btn.Bind(Button.ContentProperty, new Avalonia.Data.Binding(nameof(VmClipboardItem.Text)));
			btn.Click += (_, _) => vm.Click?.Invoke();
			return btn;
		}).SetItemsPanel(() => new StackPanel{
			Orientation = Orientation.Vertical,
			Spacing = 2,
		});
		Ctx.Bind(items, ItemsControl.ItemsSourceProperty, x => x.Items);

		this.SetContent(new ScrollViewer(), o=>{
			o.Background = Brushes.Black;
			o.SetContent(items);
		});
	}

	public void Dispose()
	{
		(Ctx as IDisposable)?.Dispose();
	}
}
