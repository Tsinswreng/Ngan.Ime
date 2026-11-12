//Impl: 聲明見 ViewSplitTopOverlay.cs。
namespace Ngan.Ime.UI.Views.SplitKeyboard;

using Avalonia.Controls;
using Avalonia.Media;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.CandidatesBar;
using Ngan.Ime.UI.Views.Ime;
using Ngan.Ime.UI.Views.Preedit;
using Ngan.Ime.UI.Views.ToolBar;

public partial class ViewSplitTopOverlay{

	[Doc("組出「預編輯 ＋ 頂欄」兩列，高度隨候選註釋狀態變化。")]
	protected override partial object Build(VmIme vm){
		_Preedit = new ViewPreedit();
		_Toolbar = new ViewToolBar();
		_Candidates = new ViewCandidatesBar();
		return new StackGrid(IsRow: true)
			.Rows("Auto,Auto")
			.With(Z => Z.Background = Brushes.Black)
			.Children(
				_Preedit.IsVisible(vm, X => X.ShowPreedit)
				,new Grid()
					.Height(vm, X => X.TopBarHeight)
					.Children(
						_Toolbar.IsVisible(vm, X => X.ShowToolbar)
						,_Candidates.IsVisible(vm, X => X.ShowCandidates)
					)
			);
	}

	[Doc("釋放子視圖。")]
	public partial void Dispose(){
		_Preedit?.Dispose();
		_Toolbar?.Dispose();
		_Candidates?.Dispose();
		GC.SuppressFinalize(this);
	}
}
