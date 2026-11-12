namespace Ngan.Ime.UI.Views.Clipboard;

using Avalonia.Controls;
using Ngan.Ime.UI.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
剪貼板頁：可滾動的文本列表。

*為何不用 ItemsControl 模板*：條目數少（一次剪貼板歷史），直接重建子控件更簡單，
也避免在模板裡做編譯期綁定時對 DataContext 型別的假設。

#Eg[
	```cs
	var Page = new ViewClipboard();
	```
]
""")]
public partial class ViewClipboard : AppViewBase<VmClipboard>, IDisposable{

	[Doc("承載列表項的縱向面板。")]
	public StackPanel? _Items;

	[Doc("建立控件樹。")]
	protected override partial object Build(VmClipboard vm);

	[Doc("建立後掛集合變更，重建列表。")]
	protected override partial void OnAfterInitialized();

	[Doc("重建列表項。")]
	partial void Rebuild();

	[Doc("釋放 ViewModel。")]
	public partial void Dispose();
}
