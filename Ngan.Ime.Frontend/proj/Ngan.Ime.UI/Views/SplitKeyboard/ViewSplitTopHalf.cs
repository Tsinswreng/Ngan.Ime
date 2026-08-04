using Avalonia.Controls;
using Avalonia.Layout;
using Ngan.Ime.Core.Infra.Log;

namespace Ngan.Ime.UI.Views.SplitKeyboard;

/// <summary>
/// 分體模式下的單側頂條。
/// 與鍵盤半邊相同，復用一個“兩倍寬”的完整頂條，再裁剪出左半或右半。
/// 這樣候選欄、工具欄、預編輯欄都沿同一條全寬佈局切開，中央保持空出。
/// </summary>
public class ViewSplitTopHalf : UserControl
	, IDisposable
{
	readonly ViewSplitTopOverlay _fullStrip;
	readonly SplitKeyboardSide _side;
	readonly Grid _root = new();
	double _lastSyncedWidth;
	double _lastSyncedHeight;

	public ViewSplitTopHalf(SplitKeyboardSide Side){
		_side = Side;
		_fullStrip = new ViewSplitTopOverlay();
		AppLog.Info($"[SplitTop:{_side}] ctor topView#{_fullStrip.GetHashCode()}");
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
		_root.Children.Add(_fullStrip);
		_fullStrip.HorizontalAlignment = _side == SplitKeyboardSide.Left
			? HorizontalAlignment.Left
			: HorizontalAlignment.Right;
		// split 頂欄 overlay 本身是固定高度窗口；
		// 內容需要貼近鍵盤頂邊，而不是撐滿整個窗口，否則工具欄和第一排按鍵之間會留下整塊黑縫。
		_fullStrip.VerticalAlignment = VerticalAlignment.Bottom;
		SizeChanged += OnSizeChanged;
		LayoutUpdated += OnLayoutUpdated;
	}

	void OnSizeChanged(object? Sender, SizeChangedEventArgs E)
	{
		var width = Bounds.Width;
		var height = Bounds.Height;
		if(width <= 0){
			AppLog.Warn($"[SplitTop:{_side}] SizeChanged width<=0 bounds={Bounds}");
			return;
		}
		if(Math.Abs(width - _lastSyncedWidth) < 0.5 && Math.Abs(height - _lastSyncedHeight) < 0.5){
			return;
		}
		_lastSyncedWidth = width;
		_lastSyncedHeight = height;
		_fullStrip.Width = width * 2;
		AppLog.Info($"[SplitTop:{_side}] SizeChanged bounds={Bounds} stripWidth={_fullStrip.Width} stripHeight={_fullStrip.Height} rootBounds={_root.Bounds}");
	}

	void OnLayoutUpdated(object? Sender, EventArgs E)
	{
		AppLog.Debug($"[SplitTop:{_side}] LayoutUpdated self={Bounds} root={_root.Bounds} strip={_fullStrip.Bounds} desired={_fullStrip.DesiredSize}");
	}

	public void Dispose()
	{
		SizeChanged -= OnSizeChanged;
		LayoutUpdated -= OnLayoutUpdated;
		_fullStrip.Dispose();
	}
}
