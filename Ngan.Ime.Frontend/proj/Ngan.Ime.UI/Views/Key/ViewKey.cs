using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.UI.Infra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Ngan.Ime.UI.Views.Key;
using Ctx = VmKey;

public class ViewKey : AppViewBase<Ctx>
{
	public ViewKey(){
		Ctx = Di.DiOrMk<Ctx>();
		Style();
		Render();
	}

	public class Cls{
		public const str HintLabel = nameof(HintLabel);
		public const str Label = nameof(Label);
		public const str KeyBorder = nameof(KeyBorder);
	}

	static class TswG{
		public static readonly SolidColorBrush KeyText = Brush("#E0E0E0");
		public static SolidColorBrush GapLine => (SolidColorBrush)UiCfg.Inst.GapLineBrush;
		public static readonly SolidColorBrush HintText = Brush("#BDBDBD");
		public static readonly CornerRadius Round = new(0);
		public static readonly Thickness KeyMargin = new(0);
		public static readonly Thickness BorderThick = new(0.5);
	}

	const double SwipeThreshold = 20;
	const int LongPressMs = 400;
	const int RepeatIntervalMs = 50;

	static SolidColorBrush Brush(str Hex)=>SolidColorBrush.Parse(Hex);

	void Style(){
		Styles.A(
			Sty.Is<Control>()
			.Set(CornerRadiusProperty, TswG.Round)
		).A(
			Sty.Is<Border>(x=>x.Class(Cls.KeyBorder))
			.Set(MarginProperty, TswG.KeyMargin)
			.Set(PaddingProperty, new Thickness(0))
			.Set(BackgroundProperty, UiCfg.Inst.KeyBgColor)
			.Set(BorderBrushProperty, TswG.GapLine)
			.Set(BorderThicknessProperty, TswG.BorderThick)
		).A(
			Sty.Is<Control>(x=>x.Class(Cls.Label))
			.Set(MinHeightProperty, 0.0)
			.Set(MinWidthProperty, 0.0)
			.Set(HorizontalAlignmentProperty, HAlign.Center)
			.Set(VerticalAlignmentProperty, VAlign.Center)
			.Set(ForegroundProperty, TswG.KeyText)
		).A(
			Sty.Is<Control>(x=>x.Class(Cls.HintLabel))
			.Set(FontSizeProperty, UiCfg.Inst.HintFontSize)
			.Set(ForegroundProperty, TswG.HintText)
			.Set(HorizontalAlignmentProperty, HAlign.Center)
		);
	}

	Border _border = default!;
	Point _pressPoint;
	DispatcherTimer? _longPressTimer;
	DispatcherTimer? _repeatTimer;
	bool _longPressFired;

	void OnPointerPressed(object? s, PointerPressedEventArgs e){
		_pressPoint = e.GetPosition(_border);
		_longPressFired = false;
		e.Pointer.Capture(_border);
		_border.Background = UiCfg.Inst.MainColor;
		AppLog.Info($"[SplitTouch] Key PointerPressed label={Ctx?.Label} key={Ctx?.Key_Click?.Name}");
		AppLog.Debug($"[Key] Pressed, hasLongPress={Ctx?.LongPress is not null}, isRepeat={Ctx?.IsRepeat}");
		StartLongPressTimer();
	}

	void OnPointerMoved(object? s, PointerEventArgs e){
		var pos = e.GetPosition(_border);
		var dx = pos.X - _pressPoint.X;
		var dy = pos.Y - _pressPoint.Y;
		if(Math.Abs(dx) > SwipeThreshold || Math.Abs(dy) > SwipeThreshold){
			StopLongPressTimer();
		}
	}

	void OnPointerReleased(object? s, PointerReleasedEventArgs e){
		var swPerf = System.Diagnostics.Stopwatch.StartNew();
		if(e.Pointer.Captured == _border){
			e.Pointer.Capture(null);
		}
		StopLongPressTimer();
		RestoreBg();

		if(_longPressFired){
			AppLog.Debug("[Key] Released after long press, skipping Click");
			return;
		}
		var pos = e.GetPosition(_border);
		var dx = pos.X - _pressPoint.X;
		var dy = pos.Y - _pressPoint.Y;

		if(Math.Abs(dx) > SwipeThreshold || Math.Abs(dy) > SwipeThreshold){
			if(Math.Abs(dx) > Math.Abs(dy)){
				if(dx > 0) Ctx?.SwipeRight?.Invoke();
				else Ctx?.SwipeLeft?.Invoke();
			}else{
				if(dy > 0) Ctx?.SwipeDown?.Invoke();
				else Ctx?.SwipeUP?.Invoke();
			}
		}else{
			AppLog.Debug($"[Perf] OnPointerReleased→Click start: {swPerf.ElapsedMilliseconds}ms");
			AppLog.Info($"[SplitTouch] Key Click label={Ctx?.Label} key={Ctx?.Key_Click?.Name}");
			Ctx?.Click?.Invoke();
			AppLog.Debug($"[Perf] OnPointerReleased→Click done: {swPerf.ElapsedMilliseconds}ms");
		}
	}

	void OnPointerCaptureLost(object? s, PointerCaptureLostEventArgs e){
		StopLongPressTimer();
		RestoreBg();
	}

	void RestoreBg(){
		_border.Background = Ctx?.Background ?? UiCfg.Inst.KeyBgColor;
	}

	void StartLongPressTimer(){
		_longPressTimer = new DispatcherTimer{Interval = TimeSpan.FromMilliseconds(LongPressMs)};
		_longPressTimer.Tick += (_, _) => {
			_longPressTimer.Stop();
			_longPressFired = true;
			AppLog.Debug($"[Key] LongPress fired, isRepeat={Ctx?.IsRepeat}");
			Ctx?.LongPress?.Invoke();
			if(Ctx?.IsRepeat == true){
				StartRepeatTimer();
			}
		};
		_longPressTimer.Start();
	}

	void StartRepeatTimer(){
		_repeatTimer = new DispatcherTimer{Interval = TimeSpan.FromMilliseconds(RepeatIntervalMs)};
		_repeatTimer.Tick += (_, _) => {
			Ctx?.Click?.Invoke();
		};
		_repeatTimer.Start();
	}

	void StopLongPressTimer(){
		_longPressTimer?.Stop();
		_longPressTimer = null;
		_repeatTimer?.Stop();
		_repeatTimer = null;
	}

	void Render(){
		var keyboardFont = UiCfg.Inst.KeyboardFontFamily;
		this.SetContent(new Border(), b=>{
			_border = b;
			b.Classes.Add(Cls.KeyBorder);

			b.PointerPressed += OnPointerPressed;
			b.PointerMoved += OnPointerMoved;
			b.PointerReleased += OnPointerReleased;
			b.PointerCaptureLost += OnPointerCaptureLost;
			Ctx.Bind(b, Border.BackgroundProperty, x=>x.Background);
			b.SetChild(new Grid(), grid=>{
				grid
				.A(new TextBlock(), o=>{
					o.Classes.Add(Cls.Label);
					if(keyboardFont is not null) o.FontFamily = keyboardFont;
					Ctx.Bind(o, x=>x.Text, x=>x.Label);
					Ctx.Bind(o, TextBlock.FontSizeProperty, x=>x.FontSize);
				})
				.A(new TextBlock(), o=>{
					o.Classes.Add(Cls.HintLabel);
					if(keyboardFont is not null) o.FontFamily = keyboardFont;
					o.VerticalAlignment = VAlign.Top;
					o.HorizontalAlignment = HAlign.Right;
					o.Margin = new(0, 1, 3, 0);
					Ctx.Bind(o, x=>x.Text, x=>x.Hint);
				})
				.A(new TextBlock(), o=>{
					o.Classes.Add(Cls.HintLabel);
					if(keyboardFont is not null) o.FontFamily = keyboardFont;
					o.VerticalAlignment = VAlign.Bottom;
					o.HorizontalAlignment = HAlign.Left;
					o.Margin = new(2, 0, 0, 2);
					Ctx.Bind(o, x=>x.Text, x=>x.BottomHint);
				})
				;
			});
		});
	}
}
