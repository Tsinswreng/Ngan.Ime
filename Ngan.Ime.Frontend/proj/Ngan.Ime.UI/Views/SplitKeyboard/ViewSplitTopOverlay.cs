using Ngan.Ime.Core.Infra;
using Ngan.Ime.UI.Infra;
using Avalonia.Controls;
using System.ComponentModel;
using ViewCandidatesBarControl = Ngan.Ime.UI.Views.CandidatesBar.ViewCandidatesBar;
using ViewPreeditControl = Ngan.Ime.UI.Views.Preedit.ViewPreedit;
using ViewToolBarControl = Ngan.Ime.UI.Views.ToolBar.ViewToolBar;

namespace Ngan.Ime.UI.Views.SplitKeyboard;
using Ctx = Views.Ime.VmIme;

/// <summary>
/// 分體模式下的中間窄頂條。
/// 只承擔預編輯、工具欄與候選欄，避免重新佔用整個 IME 視窗。
/// </summary>
public class ViewSplitTopOverlay : AppViewBase<Ctx>
	, IDisposable
{
	ViewPreeditControl? _preedit;
	ViewToolBarControl? _toolbar;
	ViewCandidatesBarControl? _candidates;
	PropertyChangedEventHandler? _ctxPropertyChangedHandler;
	PropertyChangedEventHandler? _uiStatePropertyChangedHandler;
	GridStack _root = new(IsRow: true);
	Grid? _topBarOverlay;
	RowDefinition? _topBarRow;

	public ViewSplitTopOverlay(){
		Ctx = Di.DiOrMk<Ctx>();
		Render();
	}

	void Render()
	{
		var topBarHeight = GetTopBarHeight();
		Content = _root.Grid;
		_topBarRow = new(topBarHeight, GUT.Pixel);
		_root.SetRowDefs([
			new(1, GUT.Auto),
			_topBarRow,
		]);

		var preedit = new ViewPreeditControl();
		_preedit = preedit;

		var toolbar = new ViewToolBarControl();
		_toolbar = toolbar;
		toolbar.Height = topBarHeight;

		var candidates = new ViewCandidatesBarControl();
		_candidates = candidates;
		candidates.Height = topBarHeight;

		void SyncVisible(){
			preedit.IsVisible = Ctx!.ShowPreedit;
			preedit.IsHitTestVisible = Ctx.ShowPreedit;

			toolbar.IsVisible = Ctx.ShowToolbar;
			toolbar.IsHitTestVisible = Ctx.ShowToolbar;

			candidates.IsVisible = Ctx.ShowCandidates;
			candidates.IsHitTestVisible = Ctx.ShowCandidates;
		}

		_ctxPropertyChangedHandler = (_, e) => {
			if(
				e.PropertyName is nameof(Ctx.ShowPreedit)
				or nameof(Ctx.ShowToolbar)
				or nameof(Ctx.ShowCandidates)
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

		_root
		.A(preedit)
		.A(new Grid(), o=>{
			_topBarOverlay = o;
			o.Height = topBarHeight;
			o.A(toolbar)
			.A(candidates);
		});
	}

	double GetTopBarHeight()
	{
		return Ctx!.UiState.IsCandidateCommentVisible
			? UiCfg.Inst.TopBarHeight
			: UiCfg.Inst.TopBarHeightNoComment;
	}

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
		if(_ctxPropertyChangedHandler is not null && Ctx is not null){
			Ctx.PropertyChanged -= _ctxPropertyChangedHandler;
		}
		if(_uiStatePropertyChangedHandler is not null && Ctx is not null){
			Ctx.UiState.PropertyChanged -= _uiStatePropertyChangedHandler;
		}
		(_preedit as IDisposable)?.Dispose();
		(_toolbar as IDisposable)?.Dispose();
		(_candidates as IDisposable)?.Dispose();
	}
}
