namespace Ngan.Ime.UI.Views.SplitKeyboard;

using Avalonia.Controls;
using Avalonia.Layout;

[Doc($$"""
分體模式的一側頂條：內嵌一份「兩倍寬」的完整頂條，再裁出左半或右半。

*與鍵盤半邊同一個手法*：候選欄、工具欄、預編輯列都按全寬排好，裁半後中央留空，
左右各顯示自己那一半——與正式的分體輸入法視覺一致。

#Eg[
	```cs
	var Half = new ViewSplitTopHalf(SplitKeyboardSide.Left);
	```
]
""")]
public partial class ViewSplitTopHalf : UserControl, IDisposable{

	[Doc("本側頂條。")]
	public ViewSplitTopOverlay? _Strip;

	[Doc("裁切容器。")]
	public Grid? _Root;

	[Doc("本側標識。")]
	readonly SplitKeyboardSide Side;

	[Doc("上次套用的寬度。")]
	f64 LastWidth;

	[Doc("上次套用的高度。")]
	f64 LastHeight;

	[Doc("建立並放入完整頂條。")]
	public ViewSplitTopHalf(SplitKeyboardSide Side){
		this.Side = Side;
		_Strip = new ViewSplitTopOverlay();
		_Root = new Grid();
		ClipToBounds = true;
		HorizontalAlignment = HorizontalAlignment.Stretch;
		VerticalAlignment = VerticalAlignment.Stretch;
		_Root.ClipToBounds = true;
		_Root.Children.Add(_Strip);
		_Strip.HorizontalAlignment = Side == SplitKeyboardSide.Left
			? HorizontalAlignment.Left
			: HorizontalAlignment.Right;
		// 頂欄 overlay 是固定高度的窗口，內容要貼近鍵盤上沿而不是撐滿。
		_Strip.VerticalAlignment = VerticalAlignment.Bottom;
		Content = _Root;
		SizeChanged += OnSizeChanged;
	}

	[Doc("把內層頂條拉成兩倍寬。")]
	void OnSizeChanged(obj? Sender, SizeChangedEventArgs Args){
		var Width = Bounds.Width;
		var Height = Bounds.Height;
		if(Width <= 0){
			return;
		}
		if(Math.Abs(Width - LastWidth) < 0.5 && Math.Abs(Height - LastHeight) < 0.5){
			return;
		}
		LastWidth = Width;
		LastHeight = Height;
		if(_Strip is not null){
			_Strip.Width = Width * 2;
		}
	}

	[Doc("解除事件並釋放頂條。")]
	public void Dispose(){
		SizeChanged -= OnSizeChanged;
		_Strip?.Dispose();
		_Strip = null;
		GC.SuppressFinalize(this);
	}
}
