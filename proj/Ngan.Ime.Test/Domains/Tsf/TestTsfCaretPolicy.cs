using Ngan.Ime.Tsf.Ipc;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Tsf;

[Doc($$"""
被測對象：{{nameof(TsfCaretPolicy)}}，即「量到的矩形能不能用」「錨點取哪裡」。

這些判斷是 2026-11-15「候選窗黏在螢幕左上角」那件事的第二層與第三層根因：
矩形可信度、以及量不到時的兜底。抽成純函數之後不必開視窗、不必有前景視窗就能驗。
""")]
public class TestTsfCaretPolicy : Tester{

	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(Adopt_Should_RejectAllZeroRect), Adopt_Should_RejectAllZeroRect!);
		this.AddTestCase(nameof(Adopt_Should_RejectFarAwayRect), Adopt_Should_RejectFarAwayRect!);
		this.AddTestCase(nameof(Adopt_Should_TakeBottomLeft), Adopt_Should_TakeBottomLeft!);
		this.AddTestCase(nameof(Adopt_Should_SkipForegroundCheckWhenUnknown), Adopt_Should_SkipForegroundCheckWhenUnknown!);
		this.AddTestCase(nameof(IsNear_Should_AllowSlack), IsNear_Should_AllowSlack!);
		this.AddTestCase(nameof(Anchor_Should_PreferMeasuredCaret), Anchor_Should_PreferMeasuredCaret!);
		this.AddTestCase(nameof(Anchor_Should_FallbackToForeground), Anchor_Should_FallbackToForeground!);
		this.AddTestCase(nameof(Anchor_Should_BeZeroWhenUnknown), Anchor_Should_BeZeroWhenUnknown!);
	}

	[Doc("""
	四邊全零代表「這個控件不回報插入點」，不能當座標用。
	""")]
	public async Task<nil> Adopt_Should_RejectAllZeroRect(obj? O){
		var T = Assert.IsTrue;
		T(!TsfCaretPolicy.TryAdopt(0, 0, 0, 0, 0, 0, 1000, 1000, true, out _, out _), "全零矩形不可採用");
		return NIL;
	}

	[Doc("""
	矩形落在前景視窗很遠的地方：那不是螢幕座標系，採用會讓浮窗跑到螢幕外。
	""")]
	public async Task<nil> Adopt_Should_RejectFarAwayRect(obj? O){
		var T = Assert.IsTrue;
		// 前景視窗在 (100,100)-(900,700)，矩形卻在 (5000,5000)。
		T(!TsfCaretPolicy.TryAdopt(5000, 5000, 5010, 5020, 100, 100, 900, 700, true, out _, out _),
			"遠離前景視窗的矩形不可採用");
		return NIL;
	}

	[Doc("""
	可用的矩形取左下角當錨點：插入點下方，候選窗從那裡往下排。
	""")]
	public async Task<nil> Adopt_Should_TakeBottomLeft(obj? O){
		var T = Assert.IsTrue;
		var Ok = TsfCaretPolicy.TryAdopt(200, 300, 210, 320, 100, 100, 900, 700, true, out var X, out var Y);
		T(Ok, "視窗內的矩形應被採用");
		T(X == 200, $"錨點 X 應取左邊 200，實際 {X}");
		T(Y == 320, $"錨點 Y 應取下面 320，實際 {Y}");
		return NIL;
	}

	[Doc("""
	沒有前景視窗（例如取不到）時跳過範圍檢查，只驗「不是全零」。
	""")]
	public async Task<nil> Adopt_Should_SkipForegroundCheckWhenUnknown(obj? O){
		var T = Assert.IsTrue;
		T(TsfCaretPolicy.TryAdopt(200, 300, 210, 320, 0, 0, 0, 0, false, out var X, out var Y),
			"前景未知時仍可採用");
		T(X == 200 && Y == 320, $"錨點應為 (200,320)，實際 ({X},{Y})");
		return NIL;
	}

	[Doc("""
	範圍判斷允許 ±SlackPx 的餘量：插入點貼在邊界上不該被丟掉。
	""")]
	public async Task<nil> IsNear_Should_AllowSlack(obj? O){
		var T = Assert.IsTrue;
		var S = TsfCaretPolicy.SlackPx;
		T(TsfCaretPolicy.IsNear(0, 0, 800, 600, 0, 0), "左上角上應在範圍內");
		T(TsfCaretPolicy.IsNear(0, 0, 800, 600, 800 + S, 600 + S), "右下角加餘量仍應在範圍內");
		T(!TsfCaretPolicy.IsNear(0, 0, 800, 600, 800 + S + 1, 0), "超出餘量就不在範圍內");
		T(!TsfCaretPolicy.IsNear(0, 0, 800, 600, 0, -S - 1), "上緣超出餘量也不在範圍內");
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
