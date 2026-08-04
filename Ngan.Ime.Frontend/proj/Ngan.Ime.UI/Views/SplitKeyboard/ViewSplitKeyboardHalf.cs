using Avalonia.Controls;
using Avalonia.Layout;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.UI.Views.KeyBoard;

namespace Ngan.Ime.UI.Views.SplitKeyboard;

/// <summary>
/// 分體懸浮窗中的單側鍵盤。
/// 這裡不重新實現半套鍵盤，而是放入一個“兩倍寬”的完整鍵盤，
/// 再用容器裁剪只露出左半或右半。
/// 這樣現有按鍵、長按、滑動與數字層邏輯都可直接復用。
/// </summary>
public class ViewSplitKeyboardHalf : UserControl
	, IDisposable
{
	readonly ViewKeyBoard _keyboard;
	readonly SplitKeyboardSide _side;
	readonly Grid _root = new();
	double _lastSyncedWidth;
	double _lastSyncedHeight;

	public ViewSplitKeyboardHalf(SplitKeyboardSide Side){
		_side = Side;
		_keyboard = new ViewKeyBoard();
		AppLog.Info($"[SplitHalf:{_side}] ctor keyboardView#{_keyboard.GetHashCode()}");
		Render();
	}

	void Render()
	{
		ClipToBounds = true;
		HorizontalAlignment = HorizontalAlignment.Stretch;
		VerticalAlignment = VerticalAlignment.Stretch;
		_root.ClipToBounds = true;
		_root.HorizontalAlignment = HorizontalAlignment.Stretch;
		_root.VerticalAlignment = VerticalAlignment.Stretch;
		Content = _root;
		_root.Children.Add(_keyboard);
		_keyboard.HorizontalAlignment = _side == SplitKeyboardSide.Left
			? HorizontalAlignment.Left
			: HorizontalAlignment.Right;
		_keyboard.VerticalAlignment = VerticalAlignment.Stretch;
		SizeChanged += OnSizeChanged;
		LayoutUpdated += OnLayoutUpdated;
	}

	/// <summary>
	/// overlay 的寬度是目標半邊寬度，而內部完整鍵盤需要拉成兩倍寬。
	/// 左側貼左對齊即可露出前半；右側貼右對齊即可露出後半。
	/// </summary>
	void OnSizeChanged(object? Sender, SizeChangedEventArgs E)
	{
		var width = Bounds.Width;
		var height = Bounds.Height;
		if(width <= 0){
			AppLog.Warn($"[SplitHalf:{_side}] SizeChanged width<=0 bounds={Bounds}");
			return;
		}
		if(Math.Abs(width - _lastSyncedWidth) < 0.5 && Math.Abs(height - _lastSyncedHeight) < 0.5){
			return;
		}
		_lastSyncedWidth = width;
		_lastSyncedHeight = height;
		_keyboard.Width = width * 2;
		if(height > 0){
			// `ViewKeyBoard` 內部使用星號行高；
			// overlay 場景下若外層不給明確高度，Avalonia 量測時可能只留下最後一排可見區域。
			_keyboard.Height = height;
		}
		AppLog.Info($"[SplitHalf:{_side}] SizeChanged bounds={Bounds} keyboardWidth={_keyboard.Width} keyboardHeight={_keyboard.Height} rootBounds={_root.Bounds}");
	}

	void OnLayoutUpdated(object? Sender, EventArgs E)
	{
		AppLog.Debug($"[SplitHalf:{_side}] LayoutUpdated self={Bounds} root={_root.Bounds} keyboard={_keyboard.Bounds} desired={_keyboard.DesiredSize}");
	}

	public void Dispose()
	{
		SizeChanged -= OnSizeChanged;
		LayoutUpdated -= OnLayoutUpdated;
		_keyboard.Dispose();
	}
}
