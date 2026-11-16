namespace Ngan.Ime.UI.Views.SplitKeyboard;

using Avalonia.Controls;
using Avalonia.Layout;
using Ngan.Ime.UI.Views.KeyBoard;

[Doc($$"""
分體鍵盤的一側：內嵌一份「兩倍寬」的完整鍵盤，再由容器裁出左半或右半。

*為何不另寫一套半鍵盤*：按鍵、手勢、數字層全部已在 {{nameof(ViewKeyBoard)}}；
用裁切複用同一份視圖，行為自然一致。代價是記憶體裡有一份完整鍵盤的控件樹，
但分體模式本來就要顯示其中一半，這比維護兩套鍵盤邏輯便宜。

*狀態一致*：布局狀態（數字層、Shift 鎖定）在共享的 `ImeUiState` 裡，
故左右半邊與主鍵盤永遠同步（舊版各自持有一份，切層時互不知道）。

#Eg[
	```cs
	var Half = new ViewSplitKeyboardHalf(SplitKeyboardSide.Left);
	```
]
""")]
public partial class ViewSplitKeyboardHalf : UserControl, IDisposable{

	[Doc("本側鍵盤。")]
	public ViewKeyBoard? _Keyboard;

	[Doc("裁切容器。")]
	public Grid? _Root;

	[Doc("本側標識。")]
	readonly SplitKeyboardSide Side;

	[Doc("上次套用的寬度；避免每幀重設尺寸。")]
	f64 LastWidth;

	[Doc("上次套用的高度。")]
	f64 LastHeight;

	[Doc("建立並放入完整鍵盤。")]
	public ViewSplitKeyboardHalf(SplitKeyboardSide Side){
		this.Side = Side;
		_Keyboard = new ViewKeyBoard();
		_Root = new Grid();
		ClipToBounds = true;
		HorizontalAlignment = HorizontalAlignment.Stretch;
		VerticalAlignment = VerticalAlignment.Stretch;
		_Root.ClipToBounds = true;
		_Root.Children.Add(_Keyboard);
		_Keyboard.HorizontalAlignment = Side == SplitKeyboardSide.Left
			? HorizontalAlignment.Left
			: HorizontalAlignment.Right;
		_Keyboard.VerticalAlignment = VerticalAlignment.Stretch;
		Content = _Root;
		SizeChanged += OnSizeChanged;
	}

	[Doc("把內層鍵盤拉成兩倍寬，讓裁切後剛好露出一半。")]
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
		if(_Keyboard is null){
			return;
		}
		_Keyboard.Width = Width * 2;
		// 分體 overlay 的高度由宿主給定；星號行高需要明確高度才不會只剩最後一排。
		if(Height > 0){
			_Keyboard.Height = Height;
		}
	}

	[Doc("解除事件並釋放鍵盤視圖。")]
	public void Dispose(){
		SizeChanged -= OnSizeChanged;
		_Keyboard?.Dispose();
		_Keyboard = null;
		GC.SuppressFinalize(this);
	}
}
