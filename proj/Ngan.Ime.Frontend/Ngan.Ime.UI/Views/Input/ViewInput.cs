namespace Ngan.Ime.UI.Views.Input;

using Avalonia.Controls;
using Avalonia.Media;
using Ngan.Ime.UI.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
預編輯列：一行可折行的文字，讀取 {{nameof(VmInput.Text)}}。

*為何用 WrapPanel*：預編輯可能很長（整句拼音），需要在窄寬度下自動折行而不是被裁掉；
`TextBlock` 本身支援 `TextWrapping`，包一層面板是為了留邊距與垂直居中。

#Eg[
	```cs
	var View = new ViewInput();
	```
]
""")]
public partial class ViewInput : AppViewBase<VmInput>, IDisposable{

	[Doc("預編輯文字控件；公開以便設計時檢查。")]
	public TextBlock? _Text;

	[Doc("建立控件樹。")]
	protected override partial object Build(VmInput vm);

	[Doc("釋放 ViewModel 的事件訂閱。")]
	public partial void Dispose();
}
