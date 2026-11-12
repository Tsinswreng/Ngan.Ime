namespace Ngan.Ime.UI.Views.Clipboard;

using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ngan.Ime.UI.Infra;

[Doc($$"""
{{nameof(ViewClipboard)}} 的實現：滾動容器 ＋ 逐項重建。
""")]
public partial class ViewClipboard{

	[Doc("組出滾動列表。")]
	protected override partial object Build(VmClipboard vm){
		_Items = new StackPanel{
			Orientation = Orientation.Vertical,
			Spacing = 2,
		};
		return new ScrollViewer()
			.Background(Brushes.Black)
			.With(S => {
				S.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
				S.Content = _Items;
			});
	}

	[Doc("訂閱集合變更並先建一次。")]
	protected override partial void OnAfterInitialized(){
		vm.Items.CollectionChanged += OnItemsChanged;
		Rebuild();
	}

	void OnItemsChanged(obj? Sender, NotifyCollectionChangedEventArgs Args){
		Rebuild();
	}

	[Doc("清空並重建所有列表項。")]
	partial void Rebuild(){
		if(_Items is null){
			return;
		}
		_Items.Children.Clear();
		foreach(var Item in vm.Items){
			var Button = new Button()
				.With(B => {
					B.Content = Item.Preview;
					B.Background = Brushes.Black;
					B.Foreground = Brushes.White;
					B.BorderBrush = UiCfg.Inst.GapLineBrush;
					B.BorderThickness = new Avalonia.Thickness(0.5);
					B.CornerRadius = new Avalonia.CornerRadius(0);
					B.HorizontalAlignment = HAlign.Stretch;
					B.HorizontalContentAlignment = HAlign.Left;
					B.Padding = new Avalonia.Thickness(12, 10);
					B.Click += (_, _) => Item.Click?.Invoke();
				});
			_Items.Children.Add(Button);
		}
	}

	[Doc("解除集合訂閱並釋放 ViewModel。")]
	public partial void Dispose(){
		if(vm is not null){
			vm.Items.CollectionChanged -= OnItemsChanged;
			vm.Dispose();
		}
		GC.SuppressFinalize(this);
	}
}
