using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Runtime;
using Android.Views;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.UI;
using Ngan.Ime.UI.Views.SplitKeyboard;

namespace Ngan.Ime.Android;

/// <summary>
/// 管理 Android 端分體鍵盤的三個 overlay window：
/// 左半鍵盤、右半鍵盤，以及左右兩側頂條。
/// IME 輸入鏈仍由 InputMethodService 持有；overlay 只承擔顯示與觸摸輸入。
/// </summary>
public class SplitKeyboardOverlayManager
	: IDisposable
{
	readonly Context _context;
	readonly IViewManager? _viewManager;
	LoggingAvaloniaView? _leftView;
	LoggingAvaloniaView? _rightView;
	LoggingAvaloniaView? _leftTopView;
	LoggingAvaloniaView? _rightTopView;
	bool _isShown;
	bool _isAttached;

	public SplitKeyboardOverlayManager(Context Context){
		_context = Context;
		_viewManager = Context.GetSystemService(Context.WindowService)?.JavaCast<IViewManager>();
	}

	public bool Show()
	{
		if(_viewManager is null){
			AppLog.Warn("[SplitOverlay] ViewManager unavailable");
			return false;
		}
		if(!AvalimeOverlayPermission.CanDraw(_context)){
			AppLog.Warn("[SplitOverlay] missing overlay permission");
			return false;
		}
		if(_isShown){
			UpdateLayout();
			return true;
		}

		try{
			EnsureOverlayViews();
			if(!_isAttached){
				var leftLp = BuildSideLayoutParams(isLeft: true);
				var rightLp = BuildSideLayoutParams(isLeft: false);
				var leftTopLp = BuildTopLayoutParams(isLeft: true);
				var rightTopLp = BuildTopLayoutParams(isLeft: false);
				LogLayout("Add-left", leftLp);
				LogLayout("Add-right", rightLp);
				LogLayout("Add-top-left", leftTopLp);
				LogLayout("Add-top-right", rightTopLp);
				_viewManager.AddView(_leftView, leftLp);
				_viewManager.AddView(_rightView, rightLp);
				_viewManager.AddView(_leftTopView, leftTopLp);
				_viewManager.AddView(_rightTopView, rightTopLp);
				_isAttached = true;
			}
			_isShown = true;
			AppLog.Info("[SplitOverlay] shown");
			UpdateLayout();
			return true;
		}catch(Exception Ex){
			AppLog.Error(Ex, "[SplitOverlay] Show failed");
			Hide();
			return false;
		}
	}

	/// <summary>
	/// 預先構造 overlay 內容樹，把首次分體切換的 Avalonia 視圖創建成本前移。
	/// 不立即 add 到 WindowManager，因此不會提前攔截畫面。
	/// </summary>
	public void EnsureCreated()
	{
		EnsureOverlayViews();
	}

	public void UpdateLayout()
	{
		if(!_isShown || _viewManager is null){
			return;
		}
		try{
			if(_leftView is not null){
				var lp = BuildSideLayoutParams(isLeft: true);
				LogLayout("Update-left", lp);
				_viewManager.UpdateViewLayout(_leftView, lp);
			}
			if(_rightView is not null){
				var lp = BuildSideLayoutParams(isLeft: false);
				LogLayout("Update-right", lp);
				_viewManager.UpdateViewLayout(_rightView, lp);
			}
			if(_leftTopView is not null){
				var lp = BuildTopLayoutParams(isLeft: true);
				LogLayout("Update-top-left", lp);
				_viewManager.UpdateViewLayout(_leftTopView, lp);
			}
			if(_rightTopView is not null){
				var lp = BuildTopLayoutParams(isLeft: false);
				LogLayout("Update-top-right", lp);
				_viewManager.UpdateViewLayout(_rightTopView, lp);
			}
		}catch(Exception Ex){
			AppLog.Error(Ex, "[SplitOverlay] UpdateLayout failed");
		}
	}

	public void Hide()
	{
		if(!_isAttached){
			_isShown = false;
			return;
		}
		RemoveAttachedOverlays();
		_isShown = false;
	}

	void EnsureOverlayViews()
	{
		_leftView ??= CreateKeyboardOverlayView(SplitKeyboardSide.Left);
		_rightView ??= CreateKeyboardOverlayView(SplitKeyboardSide.Right);
		_leftTopView ??= CreateTopOverlayView(SplitKeyboardSide.Left);
		_rightTopView ??= CreateTopOverlayView(SplitKeyboardSide.Right);
	}

	LoggingAvaloniaView CreateKeyboardOverlayView(SplitKeyboardSide Side)
	{
		var view = new LoggingAvaloniaView(_context);
		view.Tag = $"SplitKeyboard:{Side}";
		view.LayoutParameters = new ViewGroup.LayoutParams(
			ViewGroup.LayoutParams.MatchParent,
			ViewGroup.LayoutParams.MatchParent
		);
		view.Content = new ViewSplitKeyboardHalf(Side);
		return view;
	}

	LoggingAvaloniaView CreateTopOverlayView(SplitKeyboardSide Side)
	{
		var view = new LoggingAvaloniaView(_context);
		view.Tag = $"SplitTop:{Side}";
		view.LayoutParameters = new ViewGroup.LayoutParams(
			ViewGroup.LayoutParams.MatchParent,
			ViewGroup.LayoutParams.MatchParent
		);
		view.Content = new ViewSplitTopHalf(Side);
		return view;
	}

	WindowManagerLayoutParams BuildSideLayoutParams(bool isLeft)
	{
		var metrics = _context.Resources?.DisplayMetrics;
		var screenWidth = metrics?.WidthPixels ?? 0;
		var screenHeight = metrics?.HeightPixels ?? 0;
		var width = Math.Max(1, screenWidth / 4);
		var height = Math.Max(1, (i32)(screenHeight * 0.8));
		var gravity = isLeft
			? GravityFlags.Left | GravityFlags.Bottom
			: GravityFlags.Right | GravityFlags.Bottom;
		return new WindowManagerLayoutParams(
			width,
			height,
			GetOverlayWindowType(),
			GetCommonFlags(),
			Format.Translucent
		){
			Gravity = gravity,
			X = 0,
			Y = 0
		};
	}

	WindowManagerLayoutParams BuildTopLayoutParams(bool isLeft)
	{
		var metrics = _context.Resources?.DisplayMetrics;
		var screenWidth = metrics?.WidthPixels ?? 0;
		var screenHeight = metrics?.HeightPixels ?? 0;
		var keyboardHeight = Math.Max(1, (i32)(screenHeight * 0.8));
		var topHeight = GetTopOverlayHeightPx(metrics);
		var gravity = isLeft
			? GravityFlags.Left | GravityFlags.Bottom
			: GravityFlags.Right | GravityFlags.Bottom;
		return new WindowManagerLayoutParams(
			Math.Max(1, screenWidth / 4),
			topHeight,
			GetOverlayWindowType(),
			GetCommonFlags(),
			Format.Translucent
		){
			Gravity = gravity,
			X = 0,
			// 留出極小安全邊界，避免頂欄下沿和鍵盤上沿在不同 round/scale 下互相壓住 1~2px。
			Y = keyboardHeight + GetTopKeyboardSeamPx(metrics)
		};
	}

	/// <summary>
	/// split 頂條只應承擔「預編輯 + 工具欄/候選欄」的高度。
	/// 不能讓 Android 以 WrapContent 讓整棵 Avalonia 樹自由膨脹，
	/// 否則 overlay 會把中間應留給底下 App 的區域整塊蓋黑。
	/// </summary>
	static i32 GetTopOverlayHeightPx(DisplayMetrics? Metrics)
	{
		var density = Metrics?.Density ?? 1f;
		var logicalHeight = UiCfg.Inst.PreeditHeight + UiCfg.Inst.TopBarHeight;
		return Math.Max(1, (i32)Math.Ceiling(logicalHeight * density));
	}

	static i32 GetTopKeyboardSeamPx(DisplayMetrics? Metrics)
	{
		var density = Metrics?.Density ?? 1f;
		// 之前只留約 1dp 的縫，在部分機型上仍會出現頂欄下沿壓住數字行上沿 1~幾 px。
		// 這裡在原基礎上再抬高 1 個物理像素，優先消除覆蓋。
		return Math.Max(2, (i32)Math.Ceiling(density + 1f));
	}

	static WindowManagerTypes GetOverlayWindowType()
	{
		if(global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O){
			return WindowManagerTypes.ApplicationOverlay;
		}
		return WindowManagerTypes.Phone;
	}

	static WindowManagerFlags GetCommonFlags()
	{
		return WindowManagerFlags.NotFocusable
			| WindowManagerFlags.NotTouchModal
			| WindowManagerFlags.LayoutInScreen;
	}

	void RemoveOverlayView(View? View)
	{
		if(_viewManager is null || View is null){
			return;
		}
		try{
			_viewManager.RemoveView(View);
		}catch(Exception Ex){
			AppLog.Error(Ex, "[SplitOverlay] RemoveView failed");
		}
	}

	void RemoveAttachedOverlays()
	{
		RemoveOverlayView(_leftView);
		RemoveOverlayView(_rightView);
		RemoveOverlayView(_leftTopView);
		RemoveOverlayView(_rightTopView);
		_isAttached = false;
	}

	static void LogLayout(str Name, WindowManagerLayoutParams Lp)
	{
		AppLog.Info(
			$"[SplitOverlay] {Name} width={Lp.Width} height={Lp.Height} x={Lp.X} y={Lp.Y} gravity={Lp.Gravity} flags={Lp.Flags} type={Lp.Type}"
		);
	}

	static void DisposeOverlayView(ref LoggingAvaloniaView? View)
	{
		if(View?.Content is IDisposable disposableContent){
			disposableContent.Dispose();
		}
		if(View is not null){
			View.Content = null;
			View.Dispose();
			View = null;
		}
	}

	public void Dispose()
	{
		Hide();
		DisposeOverlayView(ref _leftView);
		DisposeOverlayView(ref _rightView);
		DisposeOverlayView(ref _leftTopView);
		DisposeOverlayView(ref _rightTopView);
	}
}
