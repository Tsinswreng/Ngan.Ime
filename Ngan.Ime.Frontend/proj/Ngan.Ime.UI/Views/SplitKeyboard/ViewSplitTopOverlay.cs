namespace Ngan.Ime.UI.Views.SplitKeyboard;

using Avalonia.Controls;
using Avalonia.Media;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.CandidatesBar;
using Ngan.Ime.UI.Views.Ime;
using Ngan.Ime.UI.Views.Preedit;
using Ngan.Ime.UI.Views.ToolBar;
using Tsinswreng.CsCore;

[Doc($$"""
分體模式的中間窄頂條：只有預編輯列與頂欄（工具欄／候選欄），不含鍵盤。

*為何與主界面共用 ViewModel*：`VmIme` 在容器裡是 singleton，故這裡拿到的是同一個根 VM，
不會再觸發一次「連引擎／重置首屏日誌」——舊版每個分體視圖各建一個 `VmIme`，
切到分體就把使用者剛選好的 body 頁搶回日誌頁。

*可見性*：全部走綁定，與主界面同一套規則。

#Eg[
	```cs
	var Strip = new ViewSplitTopOverlay();
	```
]
""")]
public partial class ViewSplitTopOverlay : AppViewBase<VmIme>, IDisposable{

	[Doc("預編輯列。")]
	public ViewPreedit? _Preedit;

	[Doc("工具欄。")]
	public ViewToolBar? _Toolbar;

	[Doc("候選欄。")]
	public ViewCandidatesBar? _Candidates;

	[Doc("建立控件樹。")]
	protected override partial object Build(VmIme vm);

	[Doc("釋放子視圖。")]
	public partial void Dispose();
}
