namespace Ngan.Ime.UI.Views.Ime;

using Avalonia.Controls;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.CandidatesBar;
using Ngan.Ime.UI.Views.Clipboard;
using Ngan.Ime.UI.Views.KeyBoard;
using Ngan.Ime.UI.Views.Preedit;
using Ngan.Ime.UI.Views.RimeLog;
using Ngan.Ime.UI.Views.ToolBar;
using Ngan.Ime.UI.Views.VoiceInput;
using Tsinswreng.CsCore;

[Doc($$"""
輸入法主視圖：上「預編輯列」、中「頂欄（工具欄／候選欄）」、下「body 區（四頁互斥）」。

*為何可見性全部走綁定*：舊版訂閱 7 個 `Show*` 屬性後手動設 `IsVisible` 與 `IsHitTestVisible`，
等於把「綁定能做的事」用一份手工屬性名清單重做一次，漏一個就是幽靈 bug。
現在每個子視圖各自 `.IsVisible(vm, X => X.ShowXxx)`，加一頁只改 ViewModel 與這裡各一行。

*為何頂欄用疊層 Grid*：工具欄與候選欄共用同一塊高度、互斥顯示，疊在一起可以避免佈局跳動。

#Eg[
	```cs
	Content = new ViewIme();
	```
]
""")]
public partial class ViewIme : AppViewBase<VmIme>, IDisposable{

	[Doc("預編輯列。")]
	public ViewPreedit? _Preedit;

	[Doc("工具欄。")]
	public ViewToolBar? _Toolbar;

	[Doc("候選欄。")]
	public ViewCandidatesBar? _Candidates;

	[Doc("鍵盤。")]
	public ViewKeyBoard? _Keyboard;

	[Doc("剪貼板頁。")]
	public ViewClipboard? _Clipboard;

	[Doc("語音頁。")]
	public ViewVoiceInput? _VoiceInput;

	[Doc("日誌頁。")]
	public ViewRimeLog? _RimeLog;

	[Doc("建立控件樹。")]
	protected override partial object Build(VmIme vm);

	[Doc("首次建立後啟動引擎連接（只生效一次）。")]
	protected override partial void OnAfterInitialized();

	[Doc("釋放所有子視圖與 ViewModel。")]
	public partial void Dispose();
}
