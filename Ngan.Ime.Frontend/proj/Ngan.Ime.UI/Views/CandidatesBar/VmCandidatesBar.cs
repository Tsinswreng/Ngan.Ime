using System.Collections.Generic;
using System.Collections.ObjectModel;
using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI.Views.Candidate;
using Ngan.Ime.ViewModels;
using Avalonia.Media;
using Avalonia.Threading;
using static Ngan.Ime.Core.Keys.KeyChars;
using KS = Ngan.Ime.Core.Keys.KeyStates;

namespace Ngan.Ime.UI.Views.CandidatesBar;
using Ctx = VmCandidatesBar;

public partial class VmCandidatesBar : ViewModelBase
	, IMk<VmCandidatesBar>
	, IDisposable
{
	/// <summary>
	/// 候選欄固定預建 16 個槽位。
	/// 正常頁大小遠小於這個值，首顯時直接復用既有 VM / View，避免從空集合臨時長出整排控件。
	/// </summary>
	const i32 CandidatePoolSize = VmCandidate.PoolSize;

	public ISvcIme ImeState{get;}

	readonly EventHandler<IEnumerable<IKeyEvent>> _afterInputHandler;
	bool _HasShownCandidates;

	public VmCandidatesBar(ISvcIme ImeState){
		this.ImeState = ImeState;
		AppLog.Info($"[Life] VmCandidatesBar ctor vm#{GetHashCode()}");
		WarmCandidatePool();
		_afterInputHandler = (s, e)=>{
			var sw = System.Diagnostics.Stopwatch.StartNew();
			AppLog.Debug($"[Perf] VmCandidatesBar.AfterInput start: {sw.ElapsedMilliseconds}ms");
			var candidates = ImeState.Candidates.Data;
			if(candidates is null){
				Dispatcher.UIThread.Post(HideAllCandidates);
				return;
			}
			var highlightedIndex = ImeState.Candidates.HighlightedIndex;
			// 候選欄在 Android hide/show 後若每次整批替換 CandVms，
			// Avalonia 會重建整排候選控件並重新跑一次布局/綁定。
			// 這裡改成原地覆寫 VM，讓候選視圖盡量被復用，避免聯想詞上屏後的額外卡頓。
			Dispatcher.UIThread.Post(() => ApplyCandidates(candidates, highlightedIndex));
			AppLog.Debug($"[Perf] VmCandidatesBar.AfterInput done: {sw.ElapsedMilliseconds}ms, candidates: {candidates.Count}");
		};
		ImeState.AfterInput += _afterInputHandler;
	}

	/// <summary>
	/// 候選欄 VM 的兜底創建路徑。
	/// 依賴本身仍交由 DI 解析，只是不再依賴反射式自動構造。
	/// </summary>
	public static Ctx Mk()
	{
		return new Ctx(Di.GetRSvc<ISvcIme>());
	}

	/// <summary>
	/// 啟動時先把候選 VM 池建好。
	/// 之後候選首顯只切換資料與顯隱，不再批量 new VM。
	/// </summary>
	void WarmCandidatePool()
	{
		for(i32 Index = 0; Index < CandidatePoolSize; Index++){
			CandVms.Add(MkCandVm());
		}
	}

	void ApplyCandidates(IList<ICandidate> candidates, int highlightedIndex){
		// UI 體感卡頓主要發生在「首次從空列表變成有候選」時，這裡單獨量測 UI 線程的實際更新成本。
		var sw = System.Diagnostics.Stopwatch.StartNew();
		var oldVisibleCount = CountVisibleCandidates();
		var visibleCount = Math.Min(candidates.Count, CandVms.Count);
		if(candidates.Count > CandVms.Count){
			AppLog.Warn($"[Perf] VmCandidatesBar.ApplyCandidates candidates overflow: actual={candidates.Count}, pool={CandVms.Count}");
		}

		for(i32 index = 0; index < visibleCount; index++){
			var vm = CandVms[index];
			FillCandVm(vm, candidates[index], index, index == highlightedIndex);
		}
		for(i32 index = visibleCount; index < CandVms.Count; index++){
			HideCandVm(CandVms[index], index);
		}
		var isFirstShow = !_HasShownCandidates && oldVisibleCount == 0 && visibleCount > 0;
		if(isFirstShow){
			_HasShownCandidates = true;
		}
		AppLog.Info($"[Perf] VmCandidatesBar.ApplyCandidates ui={sw.ElapsedMilliseconds}ms oldVisible={oldVisibleCount} newVisible={visibleCount} source={candidates.Count} firstShow={isFirstShow}");
	}

	VmCandidate MkCandVm(){
		var ans = VmCandidate.Mk();
		ans.Background = UiCfg.Inst.CandidateBgColor;
		ans.IsVisible = false;
		return ans;
	}

	/// <summary>
	/// 復用既有槽位顯示當前候選。
	/// 單個槽位的最小寬度由 ViewCandidatesBar 提前灌入，這裡只覆寫文字、高亮與點擊行為。
	/// </summary>
	void FillCandVm(VmCandidate vm, ICandidate cand, int index, bool isHighlighted){
		// Click 也隨當前 index 一併覆寫，避免復用 VM 後還指向舊候選位次。
		vm.Text = cand.Text ?? "";
		vm.Comment = cand.Comment ?? "";
		vm.Index = index;
		vm.Foreground = isHighlighted ? UiCfg.Inst.MainColor : Brushes.White;
		vm.IsVisible = true;
		vm.Click = () => {
			var key = IndexToKey(index);
			ImeState.InputSafely([
				new KeyEvent{KeyChar = key, KeyState = KS.Down},
				new KeyEvent{KeyChar = key, KeyState = KS.Up}
			], ex => HandleErr(ex));
		};
	}

	/// <summary>
	/// 清空槽位內容並隱藏。
	/// 保留 VM / View 實例，只把本輪不用的候選槽位收起來。
	/// </summary>
	void HideCandVm(VmCandidate Vm, i32 Index)
	{
		Vm.Text = "";
		Vm.Comment = "";
		Vm.Index = Index;
		Vm.Foreground = Brushes.White;
		Vm.IsVisible = false;
		Vm.Click = null;
	}

	/// <summary>
	/// 本輪沒有候選時，整批隱藏候選槽位。
	/// 不清空集合，讓下次首顯直接復用已創建的控件樹。
	/// </summary>
	void HideAllCandidates()
	{
		for(i32 Index = 0; Index < CandVms.Count; Index++){
			HideCandVm(CandVms[Index], Index);
		}
	}

	i32 CountVisibleCandidates()
	{
		i32 Ans = 0;
		foreach(var Vm in CandVms){
			if(Vm.IsVisible){
				Ans++;
			}
		}
		return Ans;
	}

	static IKeyChar IndexToKey(int index) => index switch {
		0 => D1,
		1 => D2,
		2 => D3,
		3 => D4,
		4 => D5,
		5 => D6,
		6 => D7,
		7 => D8,
		8 => D9,
		9 => D0,
		_ => D1
	};

	public ObservableCollection<VmCandidate> CandVms{
		get => field;
		set => SetProperty(ref field, value);
	} = [];

	public void Dispose()
	{
		AppLog.Info($"[Life] VmCandidatesBar dispose vm#{GetHashCode()}");
		ImeState.AfterInput -= _afterInputHandler;
	}
}
