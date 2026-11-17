using Ngan.Ime.Tsf.Ipc;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Tsf;

[Doc($$"""
被測對象：{{nameof(TsfCaretPolicy)}}，即「量到的矩形能不能用」「怎麼換成螢幕座標錨點」。

這些判斷對應 2026-11-15「候選窗黏在螢幕左上角」與 2026-11-16「只大致跟隨光標」兩件事：
矩形的有效性、客戶區座標的平移修正、量不到時的兜底。純函數，不必開視窗就能驗。
""")]
public class TestTsfCaretPolicy : Tester{

	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(Adopt_Should_RejectAllZeroRect), Adopt_Should_RejectAllZeroRect!);
		this.AddTestCase(nameof(Adopt_Should_ReturnRectTop), Adopt_Should_ReturnRectTop!);
		this.AddTestCase(nameof(Adopt_Should_TakeBottomLeft), Adopt_Should_TakeBottomLeft!);
		this.AddTestCase(nameof(Adopt_Should_ShiftClientRectIntoForeground), Adopt_Should_ShiftClientRectIntoForeground!);
		this.AddTestCase(nameof(Adopt_Should_KeepRectWhenForegroundUnknown), Adopt_Should_KeepRectWhenForegroundUnknown!);
		this.AddTestCase(nameof(Adopt_Should_RejectZeroHeightRectWhenPreviousExists), Adopt_Should_RejectZeroHeightRectWhenPreviousExists!);
		this.AddTestCase(nameof(Adopt_Should_TakeZeroHeightRectWhenNoPrevious), Adopt_Should_TakeZeroHeightRectWhenNoPrevious!);
		this.AddTestCase(nameof(Anchor_Should_PreferMeasuredCaret), Anchor_Should_PreferMeasuredCaret!);
		this.AddTestCase(nameof(Anchor_Should_FallbackToForeground), Anchor_Should_FallbackToForeground!);
		this.AddTestCase(nameof(Anchor_Should_BeZeroWhenUnknown), Anchor_Should_BeZeroWhenUnknown!);
	}

	[Doc("""
	四邊全零（`left == 0 && top == 0`）代表控件沒有版面，不能用（照 Weasel 的有效性判斷）。
	""")]
	public async Task<nil> Adopt_Should_RejectAllZeroRect(obj? O){
		var T = Assert.IsTrue;
		T(!TsfCaretPolicy.TryAdopt(0, 0, 0, 0, 0, 0, 1000, 1000, true, false, 0, 0, false, out _, out _, out _), "全零矩形不可採用");
		return NIL;
	}

	[Doc("""
	上緣要原樣吐出來：浮窗翻到文字行上方時靠它定位，只用下緣會蓋住整個字。
	""")]
	public async Task<nil> Adopt_Should_ReturnRectTop(obj? O){
		var T = Assert.IsTrue;
		var Ok = TsfCaretPolicy.TryAdopt(200, 300, 210, 320, 100, 100, 900, 700, true, false, 0, 0, false, out _, out _, out var Top);
		T(Ok, "視窗內的矩形應被採用");
		T(Top == 300, $"上緣應為 300，實際 {Top}");
		return NIL;
	}

	[Doc("""
	落在前景視窗內的矩形直接用；錨點取左下角（左邊、下緣）。
	""")]
	public async Task<nil> Adopt_Should_TakeBottomLeft(obj? O){
		var T = Assert.IsTrue;
		var Ok = TsfCaretPolicy.TryAdopt(200, 300, 210, 320, 100, 100, 900, 700, true, false, 0, 0, false, out var X, out var Y, out _);
		T(Ok, "視窗內的矩形應被採用");
		T(X == 200, $"錨點 X 應取左邊 200，實際 {X}");
		T(Y == 320, $"錨點 Y 應取下面 320，實際 {Y}");
		return NIL;
	}

	[Doc("""
	落在前景視窗外的矩形不是丟掉，而是當成客戶區座標平移回螢幕座標
	（照 Weasel `CGetTextExtentEditSession` 的 `enhanced_position`）。
	""")]
	public async Task<nil> Adopt_Should_ShiftClientRectIntoForeground(obj? O){
		var T = Assert.IsTrue;
		// 客戶區 (10,20)-(30,40)，前景視窗原點 (100,100)，caret 客戶區 (5,6)。
		var Ok = TsfCaretPolicy.TryAdopt(10, 20, 30, 40, 100, 100, 900, 700, true, true, 5, 6, false, out var X, out var Y, out _);
		T(Ok, "客戶區座標應被平移而不是丟棄");
		T(X == 105, $"平移後左邊應為 105（100-10+5），實際 {X}");
		T(Y == 126, $"平移後下緣應為 126（100-20+6+40），實際 {Y}");
		// caret 未知時只補前景視窗原點。
		TsfCaretPolicy.TryAdopt(10, 20, 30, 40, 100, 100, 900, 700, true, false, 0, 0, false, out var X2, out var Y2, out _);
		T(X2 == 100, $"caret 未知時左邊應為 100，實際 {X2}");
		T(Y2 == 120, $"caret 未知時下緣應為 120，實際 {Y2}");
		return NIL;
	}

	[Doc("""
	沒有前景視窗資訊時不做平移（無從判斷座標系），仍照原值採用。
	""")]
	public async Task<nil> Adopt_Should_KeepRectWhenForegroundUnknown(obj? O){
		var T = Assert.IsTrue;
		var Ok = TsfCaretPolicy.TryAdopt(10, 20, 30, 40, 0, 0, 0, 0, false, true, 5, 6, false, out var X, out var Y, out _);
		T(Ok, "前景未知時仍可採用");
		T(X == 10 && Y == 40, $"應保持原值 (10,40)，實際 ({X},{Y})");
		return NIL;
	}

	[Doc("""
	零高矩形（`top == bottom`）在已經有上一次位置時**不採用**：照 Weasel 的 CUAS 分支，
	用它定位會讓面板下緣正好壓在文字行上（順排、倒排都遮）。
	""")]
	public async Task<nil> Adopt_Should_RejectZeroHeightRectWhenPreviousExists(obj? O){
		var T = Assert.IsTrue;
		// 零高：top == bottom == 320。
		var Ok = TsfCaretPolicy.TryAdopt(200, 320, 210, 320, 100, 100, 900, 700, true, false, 0, 0, true, out _, out _, out _);
		T(!Ok, "已有上一次位置時，零高矩形不可採用");
		return NIL;
	}

	[Doc("""
	從來沒量到過位置時只能採用零高矩形：不採用就等於整個候選窗永遠不出現。
	這是我們與 Weasel 的差異（它的面板本來就會顯示，只是不移動），故寫成明例釘住。
	""")]
	public async Task<nil> Adopt_Should_TakeZeroHeightRectWhenNoPrevious(obj? O){
		var T = Assert.IsTrue;
		var Ok = TsfCaretPolicy.TryAdopt(200, 320, 210, 320, 100, 100, 900, 700, true, false, 0, 0, false, out var X, out var Y, out var Top);
		T(Ok, "沒有上一次位置時，零高矩形仍要採用");
		T(X == 200 && Y == 320 && Top == 320, $"應採用 (200,320,320)，實際 ({X},{Y},{Top})");
		return NIL;
	}

	[Doc("""
	錨點第一級：量到過插入點就沿用它。
	""")]
	public async Task<nil> Anchor_Should_PreferMeasuredCaret(obj? O){
		var T = Assert.IsTrue;
		TsfCaretPolicy.ResolveAnchor(true, 123, 456, true, 10, 20, 24, 96, out var X, out var Y);
		T(X == 123, $"X 應為 123，實際 {X}");
		T(Y == 456, $"Y 應為 456，實際 {Y}");
		return NIL;
	}

	[Doc("""
	錨點第二級：從未量到插入點時，退回前景視窗左上角加位移（不要落在螢幕原點）。
	""")]
	public async Task<nil> Anchor_Should_FallbackToForeground(obj? O){
		var T = Assert.IsTrue;
		TsfCaretPolicy.ResolveAnchor(false, 0, 0, true, 100, 200, 24, 96, out var X, out var Y);
		T(X == 124, $"X 應為 124，實際 {X}");
		T(Y == 296, $"Y 應為 296，實際 {Y}");
		return NIL;
	}

	[Doc("""
	錨點第三級：連前景視窗都沒有就回 (0,0)，由接收端解釋成「不知道位置」。
	""")]
	public async Task<nil> Anchor_Should_BeZeroWhenUnknown(obj? O){
		var T = Assert.IsTrue;
		TsfCaretPolicy.ResolveAnchor(false, 0, 0, false, 0, 0, 24, 96, out var X, out var Y);
		T(X == 0 && Y == 0, $"應為 (0,0)，實際 ({X},{Y})");
		return NIL;
	}
}
