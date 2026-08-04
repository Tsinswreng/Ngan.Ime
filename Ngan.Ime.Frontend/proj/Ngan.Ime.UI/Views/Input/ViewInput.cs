using Ngan.Ime.Core.Infra;
using Ngan.Ime.UI.Infra;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Ngan.Ime.Core.Infra.Log;

namespace Ngan.Ime.UI.Views.Input;
using Ctx = VmInput;

public class ViewInput : AppViewBase<Ctx>
	, IDisposable
{
	public ViewInput(){
		Ctx = Di.DiOrMk<Ctx>();
		AppLog.Info($"[Life] ViewInput ctor view#{GetHashCode()} vm#{Ctx?.GetHashCode()}");
		Render();
	}

	GridStack Root = new(IsRow: true);

	void Render(){
		var keyboardFont = UiCfg.Inst.KeyboardFontFamily;
		this.SetContent(Root.Grid);
		Root.Grid.Background = Brushes.Black;
		Root
		.A(new WrapPanel(), wp=>{
			wp.Background = Brushes.Black;
			wp.VerticalAlignment = VAlign.Center;
			wp.Margin = new (0);
			wp.A(new TextBlock(), o=>{
				if(keyboardFont is not null) o.FontFamily = keyboardFont;
				o.Foreground = Brushes.White;
				o.FontSize = UiCfg.Inst.PreeditFontSize;
				o.Margin = new(4, 1, 0, 1);
				o.VerticalAlignment = VAlign.Center;
				Ctx.Bind(o, x=>x.Text, x=>x.Text, Mode: BindingMode.TwoWay);
			});
		});
	}

	public void Dispose()
	{
		AppLog.Info($"[Life] ViewInput dispose view#{GetHashCode()} vm#{Ctx?.GetHashCode()}");
		(Ctx as IDisposable)?.Dispose();
	}
}
