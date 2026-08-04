using System.ComponentModel;
using Avalonia.Controls;
namespace Ngan.Ime.UI.Infra;

/// 模仿 Ngan.Dict 的 AppViewBase<TCtx>、提供統一的 Ctx 屬性與 View 基底
public class AppViewBase<TCtx>
	:UserControl
	where TCtx:class
{
	public TCtx? Ctx{
		get{return DataContext as TCtx;}
		set{DataContext = value;}
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	public event PropertyChangingEventHandler? PropertyChanging;

	protected nil RaisePropertyChanged(str PropertyName){
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
		return NIL;
	}

	protected nil RaisePropertyChanging(str PropertyName){
		PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(PropertyName));
		return NIL;
	}
}
