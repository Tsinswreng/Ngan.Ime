namespace Ngan.Ime.UI.Views.ToolBar;

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Ngan.Ime.UI.Icons;
using Ngan.Ime.UI.Infra;

[Doc($$"""
{{nameof(ViewToolBar)}} 的實現：十欄版面與七個入口。
""")]
public partial class ViewToolBar{

	[Doc("組出工具欄：文字入口用 TextBlock，圖標入口用 Icons 產生的 Shape。")]
	protected override partial object Build(VmToolBar vm){
		_Root = new Grid()
			.Cols(string.Join(",", Enumerable.Repeat("*", ColumnCount)))
			.Height(vm, X => X.TopBarHeight)
			.With(G => {
				G.Background = Brushes.Black;
				G.ClipToBounds = true;
			});

		// 1: 簡繁。
		_Root.Children.Add(MkCell(new TextBlock()
			.With(T => {
				T.FontSize = UiCfg.Inst.TopBarFontSize;
				T.Foreground = Brushes.White;
				T.HorizontalAlignment = HAlign.Center;
				T.VerticalAlignment = VAlign.Center;
			})
			.Text(vm, X => X.HanLabel), vm.ToggleSimplification));

		// 2: 候選註釋。
		_Root.Children.Add(MkCell(new TextBlock()
			.With(T => {
				T.Text = Todo.I18n("註");
				T.FontSize = UiCfg.Inst.TopBarFontSize;
				T.HorizontalAlignment = HAlign.Center;
				T.VerticalAlignment = VAlign.Center;
			})
			.Foreground(vm, X => X.CandidateCommentForeground), vm.ToggleCandidateComment));

		// 3: 剪貼板。
		var ClipboardIcon = Icons.Clipboard();
		_ClipboardIcon = ClipboardIcon;
		_Root.Children.Add(MkCell(MkIcon(ClipboardIcon)
			.Fill(vm, X => X.ClipboardForeground), vm.ToggleClipboard));

		// 4: 引擎日誌。
		var RimeLogIcon = Icons.ScrollText();
		_RimeLogIcon = RimeLogIcon;
		_Root.Children.Add(MkCell(MkIcon(RimeLogIcon)
			.Fill(vm, X => X.RimeLogForeground), vm.ToggleRimeLog));

		// 5: 語音輸入。
		var VoiceIcon = Icons.Microphone();
		_VoiceInputIcon = VoiceIcon;
		_Root.Children.Add(MkCell(MkIcon(VoiceIcon)
			.Fill(vm, X => X.VoiceInputForeground), vm.ToggleVoiceInput));

		// 6: 分體鍵盤。
		_Root.Children.Add(MkCell(new TextBlock()
			.With(T => {
				T.Text = Todo.I18n("分");
				T.FontSize = UiCfg.Inst.TopBarFontSize;
				T.HorizontalAlignment = HAlign.Center;
				T.VerticalAlignment = VAlign.Center;
			})
			.Foreground(vm, X => X.SplitKeyboardForeground), vm.ToggleSplitKeyboard));

		// 7: 系統按鍵映射。
		_Root.Children.Add(MkCell(new TextBlock()
			.With(T => {
				T.Text = Todo.I18n("映");
				T.FontSize = UiCfg.Inst.TopBarFontSize;
				T.HorizontalAlignment = HAlign.Center;
				T.VerticalAlignment = VAlign.Center;
			})
			.Foreground(vm, X => X.SystemKeyRemappingForeground), vm.ToggleSystemKeyRemapping));

		return _Root;
	}

	[Doc("設定圖標的統一尺寸（取工具欄字級）。")]
	static Shape MkIcon(Shape Icon){
		Icon.Width = UiCfg.Inst.TopBarFontSize;
		Icon.Height = UiCfg.Inst.TopBarFontSize;
		Icon.HorizontalAlignment = HAlign.Center;
		Icon.VerticalAlignment = VAlign.Center;
		return Icon;
	}

	[Doc("把內容包成一個可點擊的等寬格；點擊委派給 ViewModel 的命令。")]
	static Control MkCell(Control Content, Action OnClick){
		return new Button()
			.With(B => {
				B.Content = Content;
				B.Background = Brushes.Black;
				B.BorderBrush = UiCfg.Inst.GapLineBrush;
				B.BorderThickness = new Avalonia.Thickness(0.5);
				B.CornerRadius = new Avalonia.CornerRadius(0);
				B.Padding = new Avalonia.Thickness(0);
				B.HorizontalContentAlignment = HAlign.Center;
				B.VerticalContentAlignment = VAlign.Center;
				B.Click += (_, _) => OnClick();
			});
	}

	[Doc("釋放 ViewModel。")]
	public partial void Dispose(){
		vm?.Dispose();
		GC.SuppressFinalize(this);
	}
}
