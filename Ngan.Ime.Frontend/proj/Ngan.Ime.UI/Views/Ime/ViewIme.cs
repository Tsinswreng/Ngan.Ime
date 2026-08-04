using Ngan.Ime.Core.Infra;
using Ngan.Ime.UI.Infra;
using ViewCandidatesBarControl = Ngan.Ime.UI.Views.CandidatesBar.ViewCandidatesBar;
using ViewClipboardControl = Ngan.Ime.UI.Views.Clipboard.ViewClipboard;
using ViewKeyBoardControl = Ngan.Ime.UI.Views.KeyBoard.ViewKeyBoard;
using ViewPreeditControl = Ngan.Ime.UI.Views.Preedit.ViewPreedit;
using ViewRimeLogControl = Ngan.Ime.UI.Views.RimeLog.ViewRimeLog;
using ViewToolBarControl = Ngan.Ime.UI.Views.ToolBar.ViewToolBar;
using ViewVoiceInputControl = Ngan.Ime.UI.Views.VoiceInput.ViewVoiceInput;
using Avalonia.Controls;
using System.ComponentModel;
using Tsinswreng.Avln.Grid;
using Ngan.Ime.Core.Infra.Log;

namespace Ngan.Ime.UI.Views.Ime;
using Ctx = VmIme;

public partial class ViewIme : AppViewBase<Ctx>
	, IDisposable
{
	ViewPreeditControl? _preedit;
	ViewToolBarControl? _toolbar;
	ViewCandidatesBarControl? _candidates;
	ViewKeyBoardControl? _keyboard;
	ViewClipboardControl? _clipboard;
	ViewVoiceInputControl? _voiceInput;
	ViewRimeLogControl? _rimeLog;
	PropertyChangedEventHandler? _ctxPropertyChangedHandler;
	PropertyChangedEventHandler? _uiStatePropertyChangedHandler;
	GridStack Root = new(IsRow: true);
	Grid? _topBarOverlay;
	Grid? _bodyOverlay;
	RowDefinition? _topBarRow;

	public ViewIme(){
		Ctx = Di.DiOrMk<Ctx>();
		AppLog.Info($"[Life] ViewIme ctor view#{GetHashCode()} vm#{Ctx?.GetHashCode()}");
		Render();
	}

	void Render(){
		var topBarHeight = GetTopBarHeight();
		this.SetContent(Root.Grid);
		_topBarRow = new(topBarHeight, GUT.Pixel);
		Root.SetRowDefs([
			new(1, GUT.Auto),
			_topBarRow,
			new(1, GUT.Star),
		]);

		var preedit = new ViewPreeditControl();
		_preedit = preedit;

		var toolbar = new ViewToolBarControl();
		_toolbar = toolbar;
		toolbar.Height = topBarHeight;

		var candidates = new ViewCandidatesBarControl();
		_candidates = candidates;
		candidates.Height = topBarHeight;

		var keyboard = new ViewKeyBoardControl();
		_keyboard = keyboard;

		var clipboard = new ViewClipboardControl();
		_clipboard = clipboard;

		var voiceInput = new ViewVoiceInputControl();
		_voiceInput = voiceInput;

		var rimeLog = new ViewRimeLogControl();
		_rimeLog = rimeLog;

		void SyncVisible(){
			preedit.IsVisible = Ctx!.ShowPreedit;
			preedit.IsHitTestVisible = Ctx.ShowPreedit;

			toolbar.IsVisible = Ctx.ShowToolbar;
			toolbar.IsHitTestVisible = Ctx.ShowToolbar;

			candidates.IsVisible = Ctx.ShowCandidates;
			candidates.IsHitTestVisible = Ctx.ShowCandidates;

			keyboard.IsVisible = Ctx.ShowKeyboard;
			keyboard.IsHitTestVisible = Ctx.ShowKeyboard;

			clipboard.IsVisible = Ctx.ShowClipboard;
			clipboard.IsHitTestVisible = Ctx.ShowClipboard;

			voiceInput.IsVisible = Ctx.ShowVoiceInput;
			voiceInput.IsHitTestVisible = Ctx.ShowVoiceInput;

			rimeLog.IsVisible = Ctx.ShowRimeLog;
			rimeLog.IsHitTestVisible = Ctx.ShowRimeLog;
		}

		_ctxPropertyChangedHandler = (_, e) => {
			if(
				e.PropertyName is nameof(Ctx.ShowPreedit)
				or nameof(Ctx.ShowToolbar)
				or nameof(Ctx.ShowCandidates)
				or nameof(Ctx.ShowKeyboard)
				or nameof(Ctx.ShowClipboard)
				or nameof(Ctx.ShowVoiceInput)
				or nameof(Ctx.ShowRimeLog)
			){
				SyncVisible();
			}
		};
		Ctx.PropertyChanged += _ctxPropertyChangedHandler;
		_uiStatePropertyChangedHandler = (_, e) => {
			if(e.PropertyName == nameof(ImeUiState.IsCandidateCommentVisible)){
				SyncTopBarHeight();
			}
		};
		Ctx.UiState.PropertyChanged += _uiStatePropertyChangedHandler;
		SyncVisible();

		Root
		.A(preedit)
		.A(new Grid(), o=>{
			_topBarOverlay = o;
			o.Height = topBarHeight;
			o.A(toolbar)
			.A(candidates);
		})
		.A(new Grid(), o=>{
			_bodyOverlay = o;
			o.A(keyboard)
			.A(clipboard)
			.A(voiceInput)
			.A(rimeLog);
		});
	}

	double GetTopBarHeight() => Ctx!.UiState.IsCandidateCommentVisible
		? UiCfg.Inst.TopBarHeight
		: UiCfg.Inst.TopBarHeightNoComment;

	void SyncTopBarHeight()
	{
		var height = GetTopBarHeight();
		if(_topBarRow is not null){
			_topBarRow.Height = new GridLength(height, GridUnitType.Pixel);
		}
		if(_topBarOverlay is not null){
			_topBarOverlay.Height = height;
		}
		if(_toolbar is not null){
			_toolbar.Height = height;
		}
		if(_candidates is not null){
			_candidates.Height = height;
		}
	}

	public void Dispose()
	{
		AppLog.Info($"[Life] ViewIme dispose view#{GetHashCode()} vm#{Ctx?.GetHashCode()}");
		if(_ctxPropertyChangedHandler is not null && Ctx is not null){
			Ctx.PropertyChanged -= _ctxPropertyChangedHandler;
		}
		if(_uiStatePropertyChangedHandler is not null && Ctx is not null){
			Ctx.UiState.PropertyChanged -= _uiStatePropertyChangedHandler;
		}
		(_preedit as IDisposable)?.Dispose();
		(_toolbar as IDisposable)?.Dispose();
		(_candidates as IDisposable)?.Dispose();
		_keyboard?.Dispose();
		(_clipboard as IDisposable)?.Dispose();
		(_voiceInput as IDisposable)?.Dispose();
		(_rimeLog as IDisposable)?.Dispose();
		(Ctx as IDisposable)?.Dispose();
	}
}

