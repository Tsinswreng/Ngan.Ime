namespace Ngan.Ime.UI.Views.KeyBoard;

using Avalonia.Controls;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.Key;

[Doc($$"""
{{nameof(ViewKeyBoard)}} 的實現：資料驅動的兩層布局。
""")]
public partial class ViewKeyBoard{

	[Doc("建立字母層與數字層，並把它們疊在同一個 Grid 裡。")]
	protected override partial object Build(VmKeyBoard vm){
		var Main = MkLayer(vm, KeyLayout.MainRows).IsVisible(vm, X => X.ShowMain);
		var Num = MkLayer(vm, KeyLayout.NumRows).IsVisible(vm, X => X.ShowNum);
		_Root = new Grid().Children(Main, Num);
		return _Root;
	}

	[Doc("依一組排定義建立一層鍵盤；每一排是一個自動落號的 StackGrid。")]
	StackGrid MkLayer(VmKeyBoard vm, IReadOnlyList<KeyRow> Rows){
		var Layer = new StackGrid(IsRow: true)
			.Rows(KeyLayout.RowHeights)
			.With(Z => Z.Background = UiCfg.Inst.GapLineBrush);
		foreach(var Row in Rows){
			var RowGrid = new StackGrid(IsRow: false)
				.Cols(Row.Cols)
				.With(Z => Z.Background = UiCfg.Inst.GapLineBrush);
			foreach(var Def in Row.Keys){
				var KeyVm = vm.MkKey(Def);
				var KeyView = new ViewKey{ ViewModel = KeyVm };
				KeyViews.Add(KeyView);
				RowGrid.Children.Add(KeyView);
			}
			Layer.Children.Add(RowGrid);
		}
		return Layer;
	}

	[Doc("釋放每顆鍵的計時器，再釋放鍵盤 ViewModel。")]
	public partial void Dispose(){
		foreach(var One in KeyViews){
			One.Dispose();
		}
		KeyViews.Clear();
		vm?.Dispose();
		GC.SuppressFinalize(this);
	}
}
