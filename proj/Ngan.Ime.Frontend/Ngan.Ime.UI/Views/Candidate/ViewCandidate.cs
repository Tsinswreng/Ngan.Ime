namespace Ngan.Ime.UI.Views.Candidate;

using Avalonia;
using Avalonia.Controls;
using Ngan.Ime.UI.Infra;

[Doc($$"""
單個候選槽位：上註釋、下候選詞；點擊上屏由 ViewModel 的 {{nameof(VmCandidate.Click)}} 決定。

*為何點擊用 Pressed/Released 配對*：使用者按下滑動出去再放開時不應上屏，
故記錄「按下時在裡面」且「放開時仍在裡面」才觸發。

#Eg[
	```cs
	var View = new ViewCandidate{ ViewModel = Vm };
	```
]
""")]
public partial class ViewCandidate : AppViewBase<VmCandidate>, IDisposable{

	[Doc("本槽位的外框；點擊判定用它的邊界。")]
	public Border? _Border;

	[Doc("註釋文字控件。")]
	public TextBlock? _Comment;

	[Doc("候選詞文字控件。")]
	public TextBlock? _Text;

	[Doc("按下時指針是否在本槽位內。")]
	bool PressedInside;

	[Doc("建立控件樹。")]
	protected override partial object Build(VmCandidate vm);

	[Doc("釋放（本視圖無額外訂閱）。")]
	public partial void Dispose();
}
