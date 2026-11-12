namespace Ngan.Ime.UI.Views.KeyBoard;

using Avalonia.Media;
using Ngan.Ime.UI.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
單顆虛擬鍵的 ViewModel。

*為何這麼薄*：鍵的內容（文字、提示、手勢）屬於布局資料 {{nameof(KeyDef)}}；
本類只承載「當前要顯示什麼」與「被操作時通知誰」，不持有計時器、不碰輸入管線細節。
舊版每個 VmKey 自己訂閱引擎狀態（120 顆鍵 = 120 個訂閱），現在由 {{nameof(VmKeyBoard)}} 集中刷新。

#Eg[
	```cs
	var Vm = VmKey.Mk();
	Vm.Text = "Q";
	Vm.Invoke(EGestureSlot.Click);
	```
]
""")]
public partial class VmKey : AppVmBase, IMk<VmKey>{

	[Doc("本鍵在布局中的定義。")]
	public KeyDef Def{get;set;} = default!;

	[Doc("鍵帽主文字。")]
	public str Text{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("右上角提示文字。")]
	public str Hint{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("左下角提示文字。")]
	public str BottomHint{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("主文字字級。")]
	public f64 FontSize{
		get;
		set{ SetProperty(ref field, value); }
	} = 24;

	[Doc("提示文字字級。")]
	public f64 HintFontSize{
		get;
		set{ SetProperty(ref field, value); }
	} = 12;

	[Doc("背景刷；Shift 鎖定時由 {{nameof(VmKeyBoard)}} 換成主色。")]
	public IBrush Background{
		get;
		set{ SetProperty(ref field, value); }
	} = Brushes.Black;

	[Doc("主文字前景色。")]
	public IBrush Foreground{
		get;
		set{ SetProperty(ref field, value); }
	} = Brushes.White;

	[Doc("本鍵的最小寬度；候選欄與鍵盤共用同一套欄寬對齊。")]
	public f64 MinWidth{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("手勢入口；View 按下、滑動、長按時呼叫。")]
	public Action<EGestureSlot>? Invoke{get;set;}

	[Doc("無依賴實例；供布局產生器使用。")]
	protected VmKey(){
		base.Init();
	}

	[Doc("兜底建立。")]
	public static VmKey Mk(){
		return new VmKey();
	}
}
