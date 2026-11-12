namespace Ngan.Ime.UI.Views.Key;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.KeyBoard;

[Doc($$"""
{{nameof(ViewKey)}} 的實現：控件樹、指針手勢、計時器。
""")]
public partial class ViewKey{

	[Doc("組出「外框 ＋ 三層文字」，並掛上指針事件。")]
	protected override partial object Build(VmKey vm){
		var Font = UiCfg.Inst.FontFamily;
		_Border = new Border()
			.With(B => {
				B.Classes.Add(Cls.KeyBorder);
				B.Background = UiCfg.Inst.KeyBgColor;
				B.BorderBrush = UiCfg.Inst.GapLineBrush;
				B.BorderThickness = new Thickness(0.5);
				B.CornerRadius = new CornerRadius(0);
				B.Padding = new Thickness(0);
				B.ClipToBounds = true;
				B.PointerPressed += OnPointerPressed;
				B.PointerMoved += OnPointerMoved;
				B.PointerReleased += OnPointerReleased;
				B.PointerCaptureLost += OnPointerCaptureLost;
			})
			.Background(vm, X => X.Background)
			.Child(new Grid().Children(
				new TextBlock()
					.With(T => {
						_Label = T;
						T.Classes.Add(Cls.Label);
						T.HorizontalAlignment = HAlign.Center;
						T.VerticalAlignment = VAlign.Center;
						T.TextAlignment = TxtAlign.Center;
					})
					.Text(vm, X => X.Text)
					.FontSize(vm, X => X.FontSize)
					.With(T => {
						if(Font is not null){
							T.FontFamily = Font;
						}
					})
				,new TextBlock()
					.With(T => {
						_Hint = T;
						T.Classes.Add(Cls.Hint);
						T.HorizontalAlignment = HAlign.Right;
						T.VerticalAlignment = VAlign.Top;
						T.Margin = new Thickness(0, 1, 3, 0);
						T.Foreground = Brushes.Gray;
						T.FontSize = vm.HintFontSize;
						if(Font is not null){
							T.FontFamily = Font;
						}
					})
					.Text(vm, X => X.Hint)
				,new TextBlock()
					.With(T => {
						_BottomHint = T;
						T.Classes.Add(Cls.Hint);
						T.HorizontalAlignment = HAlign.Left;
						T.VerticalAlignment = VAlign.Bottom;
						T.Margin = new Thickness(2, 0, 0, 2);
						T.Foreground = Brushes.Gray;
						T.FontSize = vm.HintFontSize;
						if(Font is not null){
							T.FontFamily = Font;
						}
					})
					.Text(vm, X => X.BottomHint)
			));
		// 主文字顏色也要跟 ViewModel（目前固定白色，但保留綁定以便未來分層配色）。
		_Label?.Foreground = Brushes.White;
		return _Border;
	}

	[Doc("計時器在控件樹完成後建立；只在 UI 線程用，故不在建構子建立。")]
	protected override partial void OnAfterInitialized(){
		LongPressTimer ??= new DispatcherTimer{ Interval = TimeSpan.FromMilliseconds(LongPressMs) };
		LongPressTimer.Tick += OnLongPressTick;
		RepeatTimer ??= new DispatcherTimer{ Interval = TimeSpan.FromMilliseconds(RepeatIntervalMs) };
		RepeatTimer.Tick += OnRepeatTick;
	}

	void OnLongPressTick(obj? Sender, EventArgs Args){
		StopLongPressTimer();
		LongPressFired = true;
		vm.Invoke?.Invoke(EGestureSlot.LongClick);
		if(vm.Def?.IsRepeat == true){
			RepeatTimer?.Start();
		}
	}

	void OnRepeatTick(obj? Sender, EventArgs Args){
		vm.Invoke?.Invoke(EGestureSlot.Click);
	}

	void OnPointerPressed(obj? Sender, PointerPressedEventArgs Args){
		PressPoint = Args.GetPosition(_Border);
		LongPressFired = false;
		Args.Pointer.Capture(_Border);
		// 按下回饋用透明度，避免覆寫 Background 綁定（覆寫後綁定就永久失效）。
		if(_Border is not null){
			_Border.Opacity = 0.7;
		}
		LongPressTimer?.Start();
	}

	void OnPointerMoved(obj? Sender, PointerEventArgs Args){
		if(_Border is null){
			return;
		}
		var Pos = Args.GetPosition(_Border);
		if(Math.Abs(Pos.X - PressPoint.X) > SwipeThreshold || Math.Abs(Pos.Y - PressPoint.Y) > SwipeThreshold){
			StopLongPressTimer();
		}
	}

	void OnPointerReleased(obj? Sender, PointerReleasedEventArgs Args){
		if(_Border is null){
			return;
		}
		if(Args.Pointer.Captured == _Border){
			Args.Pointer.Capture(null);
		}
		StopLongPressTimer();
		_Border.Opacity = 1;
		if(LongPressFired){
			return;
		}
		var Pos = Args.GetPosition(_Border);
		var Dx = Pos.X - PressPoint.X;
		var Dy = Pos.Y - PressPoint.Y;
		if(Math.Abs(Dx) > SwipeThreshold || Math.Abs(Dy) > SwipeThreshold){
			vm.Invoke?.Invoke(PickSwipeSlot(Dx, Dy));
			return;
		}
		vm.Invoke?.Invoke(EGestureSlot.Click);
	}

	[Doc("依位移挑一個滑動手勢：水平位移大就判左右，否則判上下。")]
	static EGestureSlot PickSwipeSlot(f64 Dx, f64 Dy){
		if(Math.Abs(Dx) > Math.Abs(Dy)){
			return Dx > 0 ? EGestureSlot.SwipeRight : EGestureSlot.SwipeLeft;
		}
		return Dy > 0 ? EGestureSlot.SwipeDown : EGestureSlot.SwipeUp;
	}

	void OnPointerCaptureLost(obj? Sender, PointerCaptureLostEventArgs Args){
		StopLongPressTimer();
		if(_Border is not null){
			_Border.Opacity = 1;
		}
	}

	void StopLongPressTimer(){
		LongPressTimer?.Stop();
		RepeatTimer?.Stop();
	}

	[Doc("停止計時器並解除訂閱；控件本身由父視圖釋放。")]
	public partial void Dispose(){
		StopLongPressTimer();
		if(LongPressTimer is not null){
			LongPressTimer.Tick -= OnLongPressTick;
			LongPressTimer = null;
		}
		if(RepeatTimer is not null){
			RepeatTimer.Tick -= OnRepeatTick;
			RepeatTimer = null;
		}
		GC.SuppressFinalize(this);
	}
}
