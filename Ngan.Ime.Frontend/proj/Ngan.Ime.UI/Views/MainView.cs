using ViewImeControl = Ngan.Ime.UI.Views.Ime.ViewIme;
using Avalonia.Controls;
using Ngan.Ime.Core.Infra.Log;

namespace Ngan.Ime.UI.Views;

public class MainView : UserControl
	, IDisposable
{
	ViewImeControl? _viewIme;

	public MainView(){
		AppLog.Info($"[Life] MainView ctor #{GetHashCode()}");
		_viewIme = new ViewImeControl();
		Content = _viewIme;
	}

	public void Dispose()
	{
		AppLog.Info($"[Life] MainView dispose #{GetHashCode()}");
		_viewIme?.Dispose();
		_viewIme = null;
	}
}

