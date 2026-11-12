namespace Ngan.Ime.UI.Views.CandidatesBar;

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.Candidate;

[Doc($$"""
{{nameof(ViewCandidatesBar)}} 的實現：水平排列的槽位 ＋ 欄寬同步。
""")]
public partial class ViewCandidatesBar{

	[Doc("把 16 個槽位放進水平面板，高度綁定 ViewModel。")]
	protected override partial object Build(VmCandidatesBar vm){
		_Items = new StackPanel{
			Orientation = Orientation.Horizontal,
			Spacing = 0,
			VerticalAlignment = VAlign.Stretch,
		};
		foreach(var SlotVm in vm.CandVms){
			var Slot = new ViewCandidate{ ViewModel = SlotVm };
			SlotViews.Add(Slot);
			_Items.Children.Add(Slot);
		}
		return new Border()
			.Background(Brushes.Black)
			.Height(vm, X => X.TopBarHeight)
			.Child(_Items);
	}

	[Doc("尺寸變更時把槽位寬度同步成「欄寬 / 10」。")]
	protected override partial void OnAfterInitialized(){
		SizeChanged += (_, _) => vm.SetSlotMinWidth(Bounds.Width);
		// 首幀還沒有 SizeChanged 時，先按當前寬度算一次（可能為 0，會被 SetSlotMinWidth 忽略）。
		vm.SetSlotMinWidth(Bounds.Width);
	}

	[Doc("釋放 ViewModel。")]
	public partial void Dispose(){
		foreach(var One in SlotViews){
			One.Dispose();
		}
		SlotViews.Clear();
		vm?.Dispose();
		GC.SuppressFinalize(this);
	}
}
