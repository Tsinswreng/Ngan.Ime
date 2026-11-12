namespace Ngan.Ime.UI.Views.Ime;

using Avalonia.Controls;
using Avalonia.Media;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.CandidatesBar;
using Ngan.Ime.UI.Views.Clipboard;
using Ngan.Ime.UI.Views.Input;
using Ngan.Ime.UI.Views.KeyBoard;
using Ngan.Ime.UI.Views.Preedit;
using Ngan.Ime.UI.Views.RimeLog;
using Ngan.Ime.UI.Views.ToolBar;
using Ngan.Ime.UI.Views.VoiceInput;

[Doc($$"""
{{nameof(ViewIme)}} 的實現：三段式版面 ＋ 各區域的可見性綁定。
""")]
public partial class ViewIme{

	[Doc("組出三段式版面；每個子區域的可見性直接綁到 ViewModel。")]
	protected override partial object Build(VmIme vm){
		_Preedit = new ViewPreedit();
		_Toolbar = new ViewToolBar();
		_Candidates = new ViewCandidatesBar();
		_Keyboard = new ViewKeyBoard();
		_Clipboard = new ViewClipboard();
		_VoiceInput = new ViewVoiceInput();
		_RimeLog = new ViewRimeLog();

		// 頂欄：工具欄與候選欄疊在同一塊高度上，互斥顯示。
		var TopBar = new Grid()
			.Height(vm, X => X.TopBarHeight)
			.Children(
				_Toolbar.IsVisible(vm, X => X.ShowToolbar).IsHitTestVisible(vm, X => X.ShowToolbar)
				,_Candidates.IsVisible(vm, X => X.ShowCandidates).IsHitTestVisible(vm, X => X.ShowCandidates)
			);

		// body：四頁互斥。
		var Body = new Grid().Children(
			_Keyboard.IsVisible(vm, X => X.ShowKeyboard).IsHitTestVisible(vm, X => X.ShowKeyboard)
			,_Clipboard.IsVisible(vm, X => X.ShowClipboard).IsHitTestVisible(vm, X => X.ShowClipboard)
			,_VoiceInput.IsVisible(vm, X => X.ShowVoiceInput).IsHitTestVisible(vm, X => X.ShowVoiceInput)
			,_RimeLog.IsVisible(vm, X => X.ShowRimeLog).IsHitTestVisible(vm, X => X.ShowRimeLog)
		);

		return new StackGrid(IsRow: true)
			.Rows("Auto,Auto,*")
			.With(Z => Z.Background = Brushes.Black)
			.Children(
				new Border()
					.Background(Brushes.Black)
					.IsVisible(vm, X => X.ShowPreedit)
					.IsHitTestVisible(vm, X => X.ShowPreedit)
					.Child(_Preedit)
				,TopBar
				,Body
			);
	}

	[Doc("控件樹完成後啟動連接。")]
	protected override partial void OnAfterInitialized(){
		vm.Start();
	}

	[Doc("釋放所有子視圖（各自解除訂閱），再釋放根 ViewModel。")]
	public partial void Dispose(){
		(_Preedit as IDisposable)?.Dispose();
		_Toolbar?.Dispose();
		_Candidates?.Dispose();
		_Keyboard?.Dispose();
		_Clipboard?.Dispose();
		_VoiceInput?.Dispose();
		_RimeLog?.Dispose();
		vm?.Dispose();
		GC.SuppressFinalize(this);
	}
}
