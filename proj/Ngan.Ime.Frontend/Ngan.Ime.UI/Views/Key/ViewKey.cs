namespace Ngan.Ime.UI.Views.Key;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.KeyBoard;
using Tsinswreng.CsCore;

[Doc($$"""
單顆虛擬鍵的視圖。

*交互*：按下 → 計時（400ms 長按）；移動超過閾值 → 取消長按、放開時判方向；
放開 → 沒觸發長按就當點擊。長按若 `IsRepeat` 為真，進入每 50ms 連發（退格）。
閾值與時長都是常量（{nameof(SwipeThreshold)}／{nameof(LongPressMs)}／{nameof(RepeatIntervalMs)}），
不在事件處理器裡寫裸數字。

*樣式*：鍵帽底色與文字色都綁定 ViewModel，Shift 鎖定高亮由 ViewModel 改色；
本視圖不直接讀 `UiCfg` 的顏色（只讀字體與字級）。

#Eg[
	```cs
	var View = new ViewKey{ ViewModel = Vm };
	Panel.Children.Add(View);
	```
]
""")]
public partial class ViewKey : AppViewBase<VmKey>, IDisposable{

	[Doc("樣式類名常量；禁止用字符串字面量硬編碼類名。")]
	public static partial class Cls{
		[Doc("鍵外框。")]
		public const str KeyBorder = nameof(KeyBorder);
		[Doc("鍵帽主文字。")]
		public const str Label = nameof(Label);
		[Doc("鍵帽角落提示。")]
		public const str Hint = nameof(Hint);
	}

	[Doc("滑動判定閾值（px）；超過就視為滑動手勢而不是點擊。")]
	public const f64 SwipeThreshold = 20;

	[Doc("長按判定時長（ms）。")]
	public const i32 LongPressMs = 400;

	[Doc("連發間隔（ms）。")]
	public const i32 RepeatIntervalMs = 50;

	[Doc("鍵外框；事件都掛在它上面。")]
	public Border? _Border;

	[Doc("主文字控件。")]
	public TextBlock? _Label;

	[Doc("右上角提示控件。")]
	public TextBlock? _Hint;

	[Doc("左下角提示控件。")]
	public TextBlock? _BottomHint;

	[Doc("按下位置；用來算滑動方向。")]
	Point PressPoint;

	[Doc("本次按下是否已觸發長按。")]
	bool LongPressFired;

	[Doc("長按計時器；第一次按下時建立，之後複用。")]
	DispatcherTimer? LongPressTimer;

	[Doc("連發計時器；長按且 `IsRepeat` 為真時啟動。")]
	DispatcherTimer? RepeatTimer;

	[Doc("建立控件樹。")]
	protected override partial object Build(VmKey vm);

	[Doc("控件樹建好後建立計時器（UI 線程）。")]
	protected override partial void OnAfterInitialized();

	[Doc("釋放計時器並解除事件訂閱。")]
	public partial void Dispose();
}
