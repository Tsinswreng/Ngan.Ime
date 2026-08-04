using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.UI.Infra;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using System.ComponentModel;
using ViewCandidateControl = Ngan.Ime.UI.Views.Candidate.ViewCandidate;
using VmCandidateCtx = Ngan.Ime.UI.Views.Candidate.VmCandidate;

namespace Ngan.Ime.UI.Views.CandidatesBar;
using Ctx = VmCandidatesBar;

public class ViewCandidatesBar : AppViewBase<Ctx>
	, IDisposable
{
	readonly ImeUiState _uiState;
	PropertyChangedEventHandler? _uiStatePropertyChangedHandler;

	public ViewCandidatesBar(){
		Ctx = Di.DiOrMk<Ctx>();
		_uiState = Di.DiOrMk<ImeUiState>();
		AppLog.Info($"[Life] ViewCandidatesBar ctor view#{GetHashCode()} vm#{Ctx?.GetHashCode()}");
		Render();
	}

	GridStack Root = new(IsRow: true);
	double _cachedMinWidth;
	bool _hasLoggedPreparedWidth;

	void Render(){
		Root.Grid.Background = Brushes.Black;
		Root.Grid.Height = GetTopBarHeight();
		this.SetContent(Root.Grid);
		var items = MkItems();
		Root.A(items);
		_uiStatePropertyChangedHandler = (_, e) => {
			if(e.PropertyName == nameof(ImeUiState.IsCandidateCommentVisible)){
				SyncHeight();
			}
		};
		_uiState.PropertyChanged += _uiStatePropertyChangedHandler;
		this.LayoutUpdated += (_, _) => {
			var sw = System.Diagnostics.Stopwatch.StartNew();
			var width = this.Bounds.Width;
			if(width <= 0){
				return;
			}
			if(!TryApplyPreparedMinWidth(width)){
				return;
			}
			if(!_hasLoggedPreparedWidth){
				AppLog.Info($"[Perf] ViewCandidatesBar.PreparedMinWidth layout={sw.ElapsedMilliseconds}ms width={width:F1} minWidth={_cachedMinWidth:F1} slots={Ctx?.CandVms.Count ?? 0}");
				_hasLoggedPreparedWidth = true;
			}
		};
	}

	/// <summary>
	/// 候選出現前就先按當前欄寬算好所有槽位的最小寬度。
	/// 這樣從無候選切到有候選時，不需要再等首輪 item layout 才補 MinWidth。
	/// </summary>
	bool TryApplyPreparedMinWidth(double Width)
	{
		var candVms = Ctx?.CandVms;
		if(candVms is null || candVms.Count <= 0){
			return false;
		}
		// 間隔由 BorderThickness 提供(與鍵盤一致)，MinWidth = 容器寬度 / 10
		var minWidth = Math.Max(0, Width / 10.0);
		if(Math.Abs(minWidth - _cachedMinWidth) < 0.5
			&& Math.Abs(candVms[0].MinWidth - minWidth) < 0.5){
			return false;
		}
		_cachedMinWidth = minWidth;
		foreach(var vm in candVms){
			vm.MinWidth = minWidth;
		}
		return true;
	}

	double GetTopBarHeight() => _uiState.IsCandidateCommentVisible
		? UiCfg.Inst.TopBarHeight
		: UiCfg.Inst.TopBarHeightNoComment;

	void SyncHeight()
	{
		var height = GetTopBarHeight();
		Root.Grid.Height = height;
		this.Height = height;
	}

	ItemsControl MkItems(){
		var ans = new ItemsControl();
		ans.VerticalAlignment = VAlign.Stretch;
		ans.SetItemTemplate<VmCandidateCtx>((vm, ns)=>{
			var view = new ViewCandidateControl();
			view.CBind<VmCandidateCtx>(DataContextProperty, x=>x);
			return view;
		}).SetItemsPanel(()=>new StackPanel{
			Orientation = Orientation.Horizontal,
			Spacing = 0, // 間隔由 BorderThickness(0.5×2) 提供，與鍵盤一致
			VerticalAlignment = VAlign.Stretch,
		});
		Ctx.Bind(ans, ItemsControl.ItemsSourceProperty, x=>x.CandVms);
		return ans;
	}

	public void Dispose()
	{
		AppLog.Info($"[Life] ViewCandidatesBar dispose view#{GetHashCode()} vm#{Ctx?.GetHashCode()}");
		if(_uiStatePropertyChangedHandler is not null){
			_uiState.PropertyChanged -= _uiStatePropertyChangedHandler;
		}
		(Ctx as IDisposable)?.Dispose();
	}
}
