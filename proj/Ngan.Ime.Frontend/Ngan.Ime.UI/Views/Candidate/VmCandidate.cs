namespace Ngan.Ime.UI.Views.Candidate;

using Avalonia.Media;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI.Infra;

[Doc($$"""
單個候選詞的 ViewModel。

*為何池化*：候選欄固定顯示 16 個槽位，若每次刷新都重建 VM 與控件，
Avalonia 會重跑整排布局與綁定；故槽位只改內容、不改實例（見 `VmCandidatesBar`）。

*可點性*：候選的選詞鍵只覆蓋前 10 個（`1`～`9`、`0`）；第 11 個之後沒有對應鍵，
故 `Click` 為 null、`IsClickable` 為 false——先前誤用 `1` 兜底，會把第 1 個候選上屏。

#Eg[
	```cs
	Vm.Text = "你"; Vm.Comment = "ni3"; Vm.IsVisible = true;
	```
]
""")]
public partial class VmCandidate : AppVmBase, IMk<VmCandidate>{

	[Doc("候選欄預建槽位數。")]
	public const i32 PoolSize = 16;

	[Doc("候選詞本體。")]
	public str Text{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("候選詞註釋（編碼、詞性等）。")]
	public str Comment{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("是否顯示註釋列；由候選欄統一設定。")]
	public bool ShowComment{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("候選文字前景色；高亮項用主色。")]
	public IBrush Foreground{
		get;
		set{ SetProperty(ref field, value); }
	} = Brushes.White;

	[Doc("槽位背景色。")]
	public IBrush Background{
		get;
		set{ SetProperty(ref field, value); }
	} = Brushes.Black;

	[Doc("槽位最小寬度；由候選欄按欄寬 / 10 統一設定。")]
	public f64 MinWidth{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("槽位高度；由候選欄按 UiCfg 設定。")]
	public f64 Height{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("槽位是否可見；空槽位保持隱藏。")]
	public bool IsVisible{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("點擊行為；null 表示本槽位不可點（超出選詞鍵範圍或空槽）。")]
	public Action? Click{get;set;}

	[Doc("本槽位是否可點擊。")]
	public bool IsClickable{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("無依賴實例。")]
	protected VmCandidate(){
		base.Init();
	}

	[Doc("兜底建立。")]
	public static VmCandidate Mk(){
		return new VmCandidate();
	}
}
