using Ngan.Ime.Core.Infra;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Ngan.Ime.UI.Views.ToolBar;

/// 輸入法頂部工具欄視圖的既有實現。
/// 剪貼板與日誌圖標的前景色綁定將在聲明審查通過後補充。
public partial class ViewToolBar{
	public partial ViewToolBar(){
		Ctx = Di.DiOrMk<VmToolBar>();
		_uiState = Di.DiOrMk<ImeUiState>();
		Render();
	}

	private partial void Render(){
		this.SetContent(Root.Grid);
		Root.Grid.Background = Brushes.Black;
		Root.Grid.Height = GetTopBarHeight();
		Root.Grid.ClipToBounds = true;
		ClipToBounds = true;
		Root.SetColDefs([
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
			new(1, GUT.Star),
		]);

		Root
		.A(MkBtn(), B=>{
			B.PointerPressed += (_, E)=>{
				E.Handled = true;
				Ctx?.ToggleSimplification();
			};
			B.SetChild(new TextBlock(), O=>{
				O.Foreground = Brushes.White;
				O.VerticalAlignment = VAlign.Center;
				O.HorizontalAlignment = HAlign.Center;
				O.FontSize = UiCfg.Inst.TopBarFontSize;
				Ctx.Bind(O, TextBlock.TextProperty, X=>X.HanLabel);
			});
		})
		.A(MkBtn(), B=>{
			B.PointerPressed += (_, E)=>{
				E.Handled = true;
				Ctx?.ToggleCandidateComment();
			};
			B.SetChild(new TextBlock(), O=>{
				O.Foreground = Brushes.White;
				O.VerticalAlignment = VAlign.Center;
				O.HorizontalAlignment = HAlign.Center;
				O.FontSize = UiCfg.Inst.TopBarFontSize;
				O.Text = "註";
				Ctx.Bind(O, TextBlock.ForegroundProperty, X=>X.CandidateCommentForeground);
			});
		})
		.A(MkBtn(), B=>{
			B.PointerPressed += (_, E)=>{
				E.Handled = true;
				Ctx?.ToggleClipboard();
			};
			B.SetChild(Ngan.Ime.UI.Icons.Icons.Clipboard(), O=>{
				ClipboardIcon = O;
				O.Width = UiCfg.Inst.TopBarFontSize;
				O.Height = UiCfg.Inst.TopBarFontSize;
				Ctx.Bind(O, Avalonia.Controls.Shapes.Shape.FillProperty, X=>X.ClipboardForeground);
			});
		})
		.A(MkBtn(), B=>{
			B.PointerPressed += (_, E)=>{
				E.Handled = true;
				Ctx?.ToggleRimeLog();
			};
			B.SetChild(Ngan.Ime.UI.Icons.Icons.ScrollText(), O=>{
				RimeLogIcon = O;
				O.Width = UiCfg.Inst.TopBarFontSize;
				O.Height = UiCfg.Inst.TopBarFontSize;
				Ctx.Bind(O, Avalonia.Controls.Shapes.Shape.FillProperty, X=>X.RimeLogForeground);
			});
		})
		.A(MkBtn(), B=>{
			B.PointerPressed += (_, E)=>{
				E.Handled = true;
				Ctx?.ToggleVoiceInput();
			};
			B.SetChild(Ngan.Ime.UI.Icons.Icons.Microphone(), O=>{
				VoiceInputIcon = O;
				O.Width = UiCfg.Inst.TopBarFontSize;
				O.Height = UiCfg.Inst.TopBarFontSize;
				Ctx.Bind(O, Avalonia.Controls.Shapes.Shape.FillProperty, X=>X.VoiceInputForeground);
			});
		})
		.A(MkBtn(), B=>{
			B.PointerPressed += (_, E)=>{
				E.Handled = true;
				Ctx?.ToggleSplitKeyboard();
			};
			B.SetChild(new TextBlock(), O=>{
				O.Foreground = Brushes.White;
				O.VerticalAlignment = VAlign.Center;
				O.HorizontalAlignment = HAlign.Center;
				O.FontSize = UiCfg.Inst.TopBarFontSize;
				O.Text = "分";
				Ctx.Bind(O, TextBlock.ForegroundProperty, X=>X.SplitKeyboardForeground);
			});
		})
		.A(MkBtn(), B=>{
			B.PointerPressed += (_, E)=>{
				E.Handled = true;
				Ctx?.ToggleSystemKeyRemapping();
			};
			B.SetChild(new TextBlock(), O=>{
				O.Foreground = Brushes.White;
				O.VerticalAlignment = VAlign.Center;
				O.HorizontalAlignment = HAlign.Center;
				O.FontSize = UiCfg.Inst.TopBarFontSize;
				O.Text = "映";
				Ctx.Bind(O, TextBlock.ForegroundProperty, X=>X.SystemKeyRemappingForeground);
			});
		});

		_uiStatePropertyChangedHandler = (_, E)=>{
			if(E.PropertyName == nameof(ImeUiState.IsCandidateCommentVisible)){
				SyncHeight();
			}
		};
		_uiState.PropertyChanged += _uiStatePropertyChangedHandler;
	}

	private partial f64 GetTopBarHeight(){
		return _uiState.IsCandidateCommentVisible
			? UiCfg.Inst.TopBarHeight
			: UiCfg.Inst.TopBarHeightNoComment;
	}

	private partial void SyncHeight(){
		var Height = GetTopBarHeight();
		Root.Grid.Height = Height;
		this.Height = Height;
	}

	private static partial Border MkBtn(){
		return new Border{
			Background = Brushes.Black,
			BorderBrush = UiCfg.Inst.GapLineBrush,
			BorderThickness = new(0.5),
			CornerRadius = new(0),
			Padding = new(0),
			HorizontalAlignment = HAlign.Stretch,
			VerticalAlignment = VAlign.Stretch,
			ClipToBounds = true,
		};
	}

	public partial void Dispose(){
		if(_uiStatePropertyChangedHandler is not null){
			_uiState.PropertyChanged -= _uiStatePropertyChangedHandler;
		}
		(Ctx as IDisposable)?.Dispose();
	}
}
