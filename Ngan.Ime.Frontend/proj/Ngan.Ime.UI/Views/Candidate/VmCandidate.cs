using System.Collections.Generic;
using Ngan.Ime.ViewModels;
using Avalonia.Media;

namespace Ngan.Ime.UI.Views.Candidate;
using Ctx = VmCandidate;

public class VmCandidate : ViewModelBase
{
	protected VmCandidate(){}

	/// <summary>
	/// 候選欄預建 VM 池大小。
	/// 先保留 16 個槽位，避免從「無候選」切到「有候選」時才臨時 new 出整排 VM / View。
	/// </summary>
	public const i32 PoolSize = 16;

	public static Ctx Mk(){return new Ctx();}

	public int Index{
		get => field;
		set => SetProperty(ref field, value);
	}

	public Action? Click{get;set;}

	public str Text{
		get => field;
		set => SetProperty(ref field, value);
	} = "";

	public str? Comment{
		get => field;
		set => SetProperty(ref field, value);
	}

	public IBrush Background{
		get => field;
		set => SetProperty(ref field, value);
	} = UiCfg.Inst.CandidateBgColor;

	public IBrush Foreground{
		get => field;
		set => SetProperty(ref field, value);
	} = Brushes.White;

	public double MinWidth{
		get => field;
		set => SetProperty(ref field, value);
	}

	/// <summary>
	/// 候選槽位是否當前可見。
	/// 預建池中的空槽位保持隱藏，真正有候選時再打開，避免首顯時批量創建控件。
	/// </summary>
	public bool IsVisible{
		get => field;
		set => SetProperty(ref field, value);
	}
}

