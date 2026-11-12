namespace Ngan.Ime.UI.Views.RimeLog;

using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ngan.Ime.UI.Infra;

[Doc($$"""
{{nameof(ViewRimeLog)}} 的實現：滾動容器 ＋ 逐行文字。
""")]
public partial class ViewRimeLog{

	[Doc("組出滾動容器與行面板。")]
	protected override partial object Build(VmRimeLog vm){
		_Items = new StackPanel{
			Orientation = Orientation.Vertical,
			Spacing = 2,
		};
		_Scroll = new ScrollViewer()
			.Background(Brushes.Black)
			.With(S => {
				S.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
				S.Content = _Items;
			});
		return _Scroll;
	}

	[Doc("訂閱集合變更並先建一次。")]
	protected override partial void OnAfterInitialized(){
		vm.Lines.CollectionChanged += OnLinesChanged;
		Rebuild();
	}

	void OnLinesChanged(obj? Sender, NotifyCollectionChangedEventArgs Args){
		Rebuild();
	}

	[Doc("清空並重建；完成後滾到底。")]
	partial void Rebuild(){
		if(_Items is null){
			return;
		}
		_Items.Children.Clear();
		foreach(var Line in vm.Lines){
			_Items.Children.Add(new TextBlock()
				.With(T => {
					T.Text = Line;
					T.Foreground = Brushes.White;
					T.TextWrapping = TextWrapping.Wrap;
					T.Margin = new Avalonia.Thickness(10, 6);
					T.FontSize = UiCfg.Inst.BaseFontSize * 0.5;
				}));
		}
		_Scroll?.ScrollToEnd();
	}

	[Doc("解除集合訂閱。")]
	public partial void Dispose(){
		if(vm is not null){
			vm.Lines.CollectionChanged -= OnLinesChanged;
		}
		GC.SuppressFinalize(this);
	}
}
