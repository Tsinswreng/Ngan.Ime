namespace Ngan.Ime.UI.Views.Candidate;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.UI.Infra;
using System.ComponentModel;
using Ctx = VmCandidate;
using Ngan.Ime.Core.Infra;

public class ViewCandidate : AppViewBase<Ctx>
	, IDisposable
{
	Border _border = default!;
	bool _isPressedInside;
	readonly ImeUiState _uiState;
	RowDefinition? _commentRow;
	RowDefinition? _textRow;
	TextBlock? _commentTextBlock;
	TextBlock? _textTextBlock;
	PropertyChangedEventHandler? _uiStatePropertyChangedHandler;

	public ViewCandidate(){
		Ctx = Ctx.Mk();
		_uiState = Di.DiOrMk<ImeUiState>();
		Style();
		Render();
	}

	public class Cls{
		public const str Text = nameof(Text);
		public const str Comment = nameof(Comment);
	}

	GridStack Root = new(IsRow: true);

	void Style(){
		Styles.A(
			Sty.Is<Button>()
			.Set(MarginProperty, new Thickness(0))
			.Set(PaddingProperty, new Thickness(0))
			.Set(VerticalAlignmentProperty, VAlign.Stretch)
			.Set(HorizontalAlignmentProperty, HAlign.Stretch)
			.Set(CornerRadiusProperty, new CornerRadius(0))
		).A(
			Sty.Is<Control>()
			.Set(MarginProperty, new Thickness(0))
			.Set(PaddingProperty, new Thickness(0))
			.Set(VerticalAlignmentProperty, VAlign.Stretch)
			.Set(HorizontalAlignmentProperty, HAlign.Stretch)
			.Set(CornerRadiusProperty, new CornerRadius(0))
		).A(
			Sty.Is<Control>(x=>x.Class(Cls.Text))
			.Set(FontSizeProperty, UiCfg.Inst.CandidateFontSize)
		).A(
			Sty.Is<Control>(x=>x.Class(Cls.Comment))
			.Set(FontSizeProperty, UiCfg.Inst.CandidateCommentFontSize)
		);
	}

	void Render(){
		var keyboardFont = UiCfg.Inst.KeyboardFontFamily;
		var border = new Border{
			Child = Root.Grid,
			Background = UiCfg.Inst.CandidateBgColor,
			BorderThickness = new Thickness(right: 0.5, left:0, top:0,bottom:0),
			BorderBrush = UiCfg.Inst.CandidateGapBrush,
		};
		_border = border;
		Ctx.Bind(border, Border.BackgroundProperty, x=>x.Background);
		// 候選首顯時會批量建多個 ViewCandidate，這裡只保留必要綁定，避免每個控件額外掛觀察與打高頻日誌。
		Ctx.Bind(this, x=>x.MinWidth, x=>x.MinWidth);
		Ctx.Bind(this, x=>x.IsVisible, x=>x.IsVisible);
		Ctx.Bind(this, x=>x.IsHitTestVisible, x=>x.IsVisible);
		border.PointerPressed += (_, e) => {
			_isPressedInside = true;
			e.Pointer.Capture(border);
		};
		border.PointerReleased += (_, e) => {
			var shouldClick = _isPressedInside && IsPointerInside(e.GetPosition(_border));
			_isPressedInside = false;
			if(e.Pointer.Captured == border){
				e.Pointer.Capture(null);
			}
			if(shouldClick){
				Ctx?.Click?.Invoke();
			}
		};
		border.PointerCaptureLost += (_, _) => {
			_isPressedInside = false;
		};

		this.SetContent(border);
		Root.Grid.Height = GetTopBarHeight();
		_commentRow = new(UiCfg.Inst.CandidateCommentHeight, GUT.Pixel);
		_textRow = new(UiCfg.Inst.CandidateTextHeight, GUT.Pixel);
		Root.SetRowDefs([
			_commentRow,
			_textRow,
		]);

		Root
		.A(new TextBlock(), o=>{
			_commentTextBlock = o;
			o.Classes.Add(Cls.Comment);
			if(keyboardFont is not null) o.FontFamily = keyboardFont;
			o.VerticalAlignment = VAlign.Top;
			o.HorizontalAlignment = HAlign.Center;
			o.TextAlignment = TxtAlign.Center;
			o.Margin = new(0);
			o.Padding = new Thickness(0);
			o.TextTrimming = TextTrimming.None;
			Ctx.Bind(o, x=>x.Text, x=>x.Comment);
			Ctx.Bind(o, x=>x.Foreground, x=>x.Foreground);
		})
		.A(new Grid(), row=>{
			row.VerticalAlignment = VAlign.Stretch;
			row.HorizontalAlignment = HAlign.Stretch;
			row.Margin = new Thickness(0);
			row.A(new TextBlock(), o=>{
				_textTextBlock = o;
				o.Classes.Add(Cls.Text);
				if(keyboardFont is not null) o.FontFamily = keyboardFont;
				o.VerticalAlignment = VAlign.Center;
				o.HorizontalAlignment = HAlign.Center;
				o.TextAlignment = TxtAlign.Center;
				o.Margin = new(0);
				o.Padding = new Thickness(0);
				Ctx.Bind(o, x=>x.Text, x=>x.Text);
				Ctx.Bind(o, x=>x.Foreground, x=>x.Foreground);
			});
		})
		;
		_uiStatePropertyChangedHandler = (_, e) => {
			if(e.PropertyName == nameof(ImeUiState.IsCandidateCommentVisible)){
				SyncCommentState();
			}
		};
		_uiState.PropertyChanged += _uiStatePropertyChangedHandler;
		SyncCommentState();
	}

	bool IsPointerInside(Point p)
	{
		return p.X >= 0 && p.X <= _border.Bounds.Width
			&& p.Y >= 0 && p.Y <= _border.Bounds.Height;
	}

	double GetTopBarHeight() => _uiState.IsCandidateCommentVisible
		? UiCfg.Inst.TopBarHeight
		: UiCfg.Inst.TopBarHeightNoComment;

	void SyncCommentState()
	{
		var isVisible = _uiState.IsCandidateCommentVisible;
		Root.Grid.Height = GetTopBarHeight();
		if(_commentRow is not null){
			_commentRow.Height = new GridLength(isVisible ? UiCfg.Inst.CandidateCommentHeight : 0, GridUnitType.Pixel);
		}
		if(_textRow is not null){
			_textRow.Height = new GridLength(UiCfg.Inst.CandidateTextHeight, GridUnitType.Pixel);
		}
		if(_commentTextBlock is not null){
			_commentTextBlock.IsVisible = isVisible;
		}
		if(_textTextBlock is not null){
			_textTextBlock.VerticalAlignment = isVisible ? VAlign.Bottom : VAlign.Center;
		}
	}

	public void Dispose()
	{
		if(_uiStatePropertyChangedHandler is not null){
			_uiState.PropertyChanged -= _uiStatePropertyChangedHandler;
		}
	}
}
