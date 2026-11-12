namespace Ngan.Ime.UI.Views.RimeLog;

using Avalonia.Controls;
using Ngan.Ime.UI.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
引擎日誌頁：可滾動的逐行文字。

*為何逐行建控件*：日誌最多 200 行，重建成本可接受；比 `ItemsControl` 模板更容易保證
「新行一定顯示、滾動條自動到底」的行為。

#Eg[
	```cs
	var Page = new ViewRimeLog();
	```
]
""")]
public partial class ViewRimeLog : AppViewBase<VmRimeLog>, IDisposable{

	[Doc("承載日誌行的縱向面板。")]
	public StackPanel? _Items;

	[Doc("外層滾動容器；新行進來時滾到底。")]
	public ScrollViewer? _Scroll;

	[Doc("建立控件樹。")]
	protected override partial object Build(VmRimeLog vm);

	[Doc("訂閱集合變更並先建一次。")]
	protected override partial void OnAfterInitialized();

	[Doc("重建全部行並滾到底。")]
	partial void Rebuild();

	[Doc("釋放（本視圖只解除集合訂閱）。")]
	public partial void Dispose();
}
