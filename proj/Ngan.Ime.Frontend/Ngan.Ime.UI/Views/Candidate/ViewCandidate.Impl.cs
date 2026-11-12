namespace Ngan.Ime.UI.Views.Candidate;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Ngan.Ime.UI.Infra;

[Doc($$"""
{{nameof(ViewCandidate)}} 的實現：兩行文字 ＋ 點擊判定。
""")]
public partial class ViewCandidate{

	[Doc("組出「上註釋、下候選詞」並綁定高度、寬度、顏色。")]
	protected override partial object Build(VmCandidate vm){
		var Font = UiCfg.Inst.FontFamily;
		_Border = new Border()
			.With(B => {
				B.BorderBrush = UiCfg.Inst.CandidateGapBrush;
				B.BorderThickness = new Thickness(0.5, 0, 0, 0);
				B.Background = UiCfg.Inst.CandidateBgColor;
				B.Padding = new Thickness(0);
				B.ClipToBounds = true;
				B.PointerPressed += OnPointerPressed;
				B.PointerReleased += OnPointerReleased;
				B.PointerCaptureLost += OnPointerCaptureLost;
			})
			.Background(vm, X => X.Background)
			.MinWidth(vm, X => X.MinWidth)
			.IsVisible(vm, X => X.IsVisible)
			.Height(vm, X => X.Height)
			.Child(new Grid()
				.Rows("Auto,*")
				.Children(
					new TextBlock()
						.With(T => {
							_Comment = T;
							T.HorizontalAlignment = HAlign.Center;
							T.VerticalAlignment = VAlign.Top;
							T.TextAlignment = TxtAlign.Center;
							T.Margin = new Thickness(0);
							T.Padding = new Thickness(0);
							T.TextTrimming = TextTrimming.None;
							T.FontSize = UiCfg.Inst.CandidateCommentFontSize;
							if(Font is not null){
								T.FontFamily = Font;
							}
						})
						.Text(vm, X => X.Comment)
						.Foreground(vm, X => X.Foreground)
						.IsVisible(vm, X => X.ShowComment)
					,new TextBlock()
						.With(T => {
							_Text = T;
							T.HorizontalAlignment = HAlign.Center;
							T.VerticalAlignment = VAlign.Center;
							T.TextAlignment = TxtAlign.Center;
							T.Margin = new Thickness(0);
							T.Padding = new Thickness(0);
							T.FontSize = UiCfg.Inst.CandidateFontSize;
							if(Font is not null){
								T.FontFamily = Font;
							}
						})
						.Text(vm, X => X.Text)
						.Foreground(vm, X => X.Foreground)
				));
		return _Border;
	}

	void OnPointerPressed(obj? Sender, PointerPressedEventArgs Args){
		PressedInside = true;
		Args.Pointer.Capture(_Border);
		if(_Border is not null){
			_Border.Opacity = 0.7;
		}
	}

	void OnPointerReleased(obj? Sender, PointerReleasedEventArgs Args){
		if(_Border is null){
			return;
		}
		var Pos = Args.GetPosition(_Border);
		var Inside = Pos.X >= 0 && Pos.X <= _Border.Bounds.Width
			&& Pos.Y >= 0 && Pos.Y <= _Border.Bounds.Height;
		var ShouldClick = PressedInside && Inside;
		PressedInside = false;
		if(Args.Pointer.Captured == _Border){
			Args.Pointer.Capture(null);
		}
		_Border.Opacity = 1;
		if(ShouldClick && vm.IsClickable){
			vm.Click?.Invoke();
		}
	}

	void OnPointerCaptureLost(obj? Sender, PointerCaptureLostEventArgs Args){
		PressedInside = false;
		if(_Border is not null){
			_Border.Opacity = 1;
		}
	}

	[Doc("本視圖沒有額外訂閱，直接釋放 ViewModel。")]
	public partial void Dispose(){
		GC.SuppressFinalize(this);
	}
}
