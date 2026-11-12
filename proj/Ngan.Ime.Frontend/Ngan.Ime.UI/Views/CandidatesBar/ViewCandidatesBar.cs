namespace Ngan.Ime.UI.Views.CandidatesBar;

using Avalonia.Controls;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.Candidate;
using Tsinswreng.CsCore;

[Doc($$"""
候選欄視圖：一條水平排列的 16 個候選槽位。

*欄寬對齊*：鍵盤一排放 10 顆鍵，候選欄也按「欄寬 / 10」給每個槽位最小寬度，
故候選與鍵帽對齊；這在 `SizeChanged` 時算一次，不在每幀算。

#Eg[
	```cs
	var Bar = new ViewCandidatesBar();
	```
]
""")]
public partial class ViewCandidatesBar : AppViewBase<VmCandidatesBar>, IDisposable{

	[Doc("承載所有候選槽位的水平面板。")]
	public StackPanel? _Items;

	[Doc("槽位視圖；供設計時檢查。")]
	public readonly List<ViewCandidate> SlotViews = [];

	[Doc("建立控件樹。")]
	protected override partial object Build(VmCandidatesBar vm);

	[Doc("控製樹完成後掛尺寸變更。")]
	protected override partial void OnAfterInitialized();

	[Doc("釋放 ViewModel。")]
	public partial void Dispose();
}
