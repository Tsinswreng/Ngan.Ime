namespace Ngan.Ime.UI.Views.ToolBar;

using Avalonia.Controls;
using Avalonia.Media;
using Ngan.Ime.UI.Icons;
using Ngan.Ime.UI.Infra;

[Doc($$"""
頂部工具欄：七個功能入口，等寬排列。

*為何留三格空白*：工具欄按十欄排布，右側三欄刻意留空——分體模式會把頂欄裁成左右各半，
若把七個按鈕鋪滿，裁半後按鈕會落在被裁掉的一側。故佈局寬度先按整條頂欄算好。

*入口*：簡繁、候選註釋、剪貼板、日誌、語音、分體、按鍵映射；各自綁定
{{nameof(VmToolBar)}} 的 `XxxForeground`，開啟時變主色。

#Eg[
	```cs
	var Bar = new ViewToolBar();
	```
]
""")]
public partial class ViewToolBar : AppViewBase<VmToolBar>, IDisposable{

	[Doc("工具欄欄數；含右側三格留白（見類註釋）。")]
	public const i32 ColumnCount = 10;

	[Doc("根版面：十欄等寬。")]
	public Grid? _Root;

	[Doc("剪貼板入口圖標。")]
	public Icon? _ClipboardIcon;

	[Doc("日誌入口圖標。")]
	public Icon? _RimeLogIcon;

	[Doc("語音入口圖標。")]
	public Icon? _VoiceInputIcon;

	[Doc("建立控件樹。")]
	protected override partial object Build(VmToolBar vm);

	[Doc("釋放 ViewModel。")]
	public partial void Dispose();
}
