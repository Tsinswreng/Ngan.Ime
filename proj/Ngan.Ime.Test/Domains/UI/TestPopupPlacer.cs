using Avalonia;
using Ngan.Ime.UI.Views.CandidatePopup;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.UI;

[Doc($$"""
被測對象：{{nameof(PopupPlacer)}}，候選浮窗的定位規則。

這一組用例直接對應「候選窗亂跳、遮住光標」：
翻轉的邊界、翻上去時面板下緣是否貼着文字行上緣、x 是否夾在工作區內、
翻過一次之後是否固定在上方、翻上去時是否倒排。全部是像素整數，不必開視窗。
""")]
public class TestPopupPlacer : Tester{

	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(Below_Should_SitUnderCaret), Below_Should_SitUnderCaret!);
		this.AddTestCase(nameof(Flip_Should_SitOnTextLineTop), Flip_Should_SitOnTextLineTop!);
		this.AddTestCase(nameof(Flip_Should_TriggerExactlyWhenNoRoom), Flip_Should_TriggerExactlyWhenNoRoom!);
		this.AddTestCase(nameof(Flip_Should_ReverseRows), Flip_Should_ReverseRows!);
		this.AddTestCase(nameof(X_Should_ClampIntoWorkArea), X_Should_ClampIntoWorkArea!);
		this.AddTestCase(nameof(Y_Should_NotGoAboveWorkArea), Y_Should_NotGoAboveWorkArea!);
	}

	[Doc("""
	下方放得下時：上緣 = 插入點下緣（Weasel 0.15.0 不加間距），左邊 = 插入點左邊，不倒排。
	""")]
	public async Task<nil> Below_Should_SitUnderCaret(obj? O){
		var T = Assert.IsTrue;
		var R = PopupPlacer.Compute(Pt(x: 100, y: 500, top: 470), WidthPx: 120, HeightPx: 200, Work: Work());
		T(R.Y == 500, $"上緣應等於插入點下緣 500，實際 {R.Y}");
		T(R.X == 100, $"左邊應為 100，實際 {R.X}");
		T(!R.Flip, "下方放得下時不該翻");
		T(!R.Reversed, "不翻時不倒排");
		return NIL;
	}

	[Doc("""
	翻上去時：面板下緣必須剛好落在文字行上緣，一個像素都不能蓋進去。
	這正是「倒排也遮」的驗收點。
	""")]
	public async Task<nil> Flip_Should_SitOnTextLineTop(obj? O){
		var T = Assert.IsTrue;
		// 文字行 (470..500)，面板高 200，下方只剩 42px，放不下。
		var R = PopupPlacer.Compute(Pt(x: 100, y: 1000, top: 970), WidthPx: 120, HeightPx: 200, Work: Work());
		T(R.Flip, "下方放不下時應翻上去");
		T(R.Y + 200 == 970, $"面板下緣應等於文字行上緣 970，實際 {R.Y + 200}");
		T(R.Y == 770, $"上緣應為 770，實際 {R.Y}");
		return NIL;
	}

	[Doc("""
	翻轉的邊界：剛好放得下就不翻，少 1px 就翻。
	""")]
	public async Task<nil> Flip_Should_TriggerExactlyWhenNoRoom(obj? O){
		var T = Assert.IsTrue;
		// 工作區下緣 1000，插入點下緣 y，面板高 200（0.15.0 不加間距）：需要 y + 200 <= 1000。
		var Fits = PopupPlacer.Compute(Pt(x: 0, y: 800, top: 760), WidthPx: 100, HeightPx: 200, Work: Work());
		T(!Fits.Flip, $"800 + 200 = 1000，剛好放得下，不該翻（實際 y={Fits.Y}）");
		var Tight = PopupPlacer.Compute(Pt(x: 0, y: 801, top: 760), WidthPx: 100, HeightPx: 200, Work: Work());
		T(Tight.Flip, "801 + 200 = 1001，超出 1px 就該翻");
		return NIL;
	}

	[Doc("""
	翻上去時候選行倒排（照 Weasel 的 `m_istorepos`），第一候選貼着光標。
	""")]
	public async Task<nil> Flip_Should_ReverseRows(obj? O){
		var T = Assert.IsTrue;
		var R = PopupPlacer.Compute(Pt(x: 0, y: 990, top: 960), WidthPx: 100, HeightPx: 200, Work: Work());
		T(R.Flip && R.Reversed, "翻上去必須同時倒排");
		return NIL;
	}

	[Doc("""
	x 夾在插入點所在螢幕的工作區內：靠右邊時不越界，靠左邊時不越界。
	""")]
	public async Task<nil> X_Should_ClampIntoWorkArea(obj? O){
		var T = Assert.IsTrue;
		var Right = PopupPlacer.Compute(Pt(x: 1900, y: 100, top: 80), WidthPx: 120, HeightPx: 100, Work: Work());
		T(Right.X == 880, $"右邊界應為 1000 - 120 = 880，實際 {Right.X}");
		var Left = PopupPlacer.Compute(Pt(x: -50, y: 100, top: 80), WidthPx: 120, HeightPx: 100, Work: Work());
		T(Left.X == 0, $"左邊界應為 0，實際 {Left.X}");
		return NIL;
	}

	[Doc("""
	上方空間也不夠時，y 不得高於工作區上緣（照 Weasel 的 `if (y < rcWorkArea.top)`）。
	""")]
	public async Task<nil> Y_Should_NotGoAboveWorkArea(obj? O){
		var T = Assert.IsTrue;
		// 文字行很靠近工作區底部、上緣又低：只能翻上去，而 top − height 會是負值。
		var R = PopupPlacer.Compute(Pt(x: 10, y: 990, top: 50), WidthPx: 100, HeightPx: 200, Work: Work());
		T(R.Flip, "下方放不下，應翻上去");
		T(R.Y == 0, $"y 應夾到工作區上緣 0，實際 {R.Y}");
		return NIL;
	}

	[Doc("""
	建一個插入點矩形。
	""")]
	static PopupAnchor Pt(i32 x, i32 y, i32 top){
		return new(){
			X = x,
			Y = y,
			Top = top,
		};
	}

	[Doc("""
	一個 1000x1000、原點在工作區左上角的螢幕。
	""")]
	static PixelRect Work(){
		return new(0, 0, 1000, 1000);
	}
}
