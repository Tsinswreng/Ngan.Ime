namespace Ngan.Ime.Tsf.Ipc;

using Tsinswreng.CsCore;

[Doc($$"""
插入點矩形的採用規則；純幾何，不碰 COM 也不碰 Win32。

*照 Weasel 的 `CGetTextExtentEditSession`（`WeaselTSF/Composition.cpp:161-207`）*：
+ `GetTextExt` 成功但 `left` 與 `top` 都是 0 → 這個控件沒有版面，不能用。
+ 矩形若落在前景視窗外，代表它可能是**客戶區座標**而不是螢幕座標；
	Weasel 不丟掉它，而是用「前景視窗原點 − 矩形原點 ＋ caret」把它平移回螢幕座標
	（那裡是 `enhanced_position` 分支）。先前我們直接 reject，退回「前景視窗左上角＋偏移」，
	於是候選窗只剩大致跟隨——這就是「漂」的來源。
+ 錨點取矩形的左下角（`Left`, `Bottom`）：候選窗從插入點下方開始排。

*為何要抽出來*：這些判斷原本埋在殼的 static 類裡，只能真機驗；抽成純函數可以餵數字測。
""")]
public static class TsfCaretPolicy{

	[Doc("把矩形平移回螢幕座標時，caret 未知就只補前景視窗原點，不加 caret 位移。")]
	public const i32 NoCaret = 0;

	[Doc($$"""
	這個矩形能不能當插入點用；能用就回螢幕座標的錨點。
	#Prm[{{nameof(Left)}}][矩形左邊。]
	#Prm[{{nameof(Top)}}][矩形上邊。]
	#Prm[{{nameof(Right)}}][矩形右邊。]
	#Prm[{{nameof(Bottom)}}][矩形下邊。]
	#Prm[{{nameof(FgLeft)}}][前景視窗左邊；未知時 {{nameof(HasForeground)}} 傳 false。]
	#Prm[{{nameof(FgTop)}}][前景視窗上邊。]
	#Prm[{{nameof(FgRight)}}][前景視窗右邊。]
	#Prm[{{nameof(FgBottom)}}][前景視窗下邊。]
	#Prm[{{nameof(HasForeground)}}][是否取得前景視窗矩形。]
	#Prm[{{nameof(HasCaret)}}][是否取得 `GetCaretPos`（客戶區座標）。]
	#Prm[{{nameof(CaretX)}}][caret 的客戶區 X。]
	#Prm[{{nameof(CaretY)}}][caret 的客戶區 Y。]
	#Prm[{{nameof(X)}}][採用的錨點 X（螢幕座標）。]
	#Prm[{{nameof(Y)}}][採用的錨點 Y（螢幕座標，取矩形下緣）。]
	#Prm[{{nameof(RectTop)}}][矩形的上緣（螢幕座標）；浮窗翻到上方時用它定位。]
	#Rtn[true 表示採用。]
	""")]
	public static bool TryAdopt(
		i32 Left, i32 Top, i32 Right, i32 Bottom,
		i32 FgLeft, i32 FgTop, i32 FgRight, i32 FgBottom, bool HasForeground,
		bool HasCaret, i32 CaretX, i32 CaretY,
		out i32 X, out i32 Y, out i32 RectTop
	){
		X = 0;
		Y = 0;
		RectTop = 0;
		// Weasel：`(rc.left != 0 || rc.top != 0)` 才算有效。
		if(Left == 0 && Top == 0){
			return false;
		}
		var l = Left;
		var t = Top;
		var r = Right;
		var b = Bottom;
		// 落在前景視窗外：當成客戶區座標，平移到螢幕座標（Weasel 的 enhanced_position）。
		if(HasForeground && (l < FgLeft || l > FgRight || t < FgTop || t > FgBottom)){
			var dx = FgLeft - l + (HasCaret ? CaretX : NoCaret);
			var dy = FgTop - t + (HasCaret ? CaretY : NoCaret);
			l += dx;
			r += dx;
			t += dy;
			b += dy;
		}
		X = l;
		Y = b;
		RectTop = t;
		return true;
	}

	[Doc($$"""
	送給服務端的錨點；三級：量到的插入點 → 前景視窗左上角加位移 → (0,0)（未知）。
	這是「量測從來沒成功過」時的兜底，不是主路徑。
	#Prm[{{nameof(CaretValid)}}][是否量到過插入點。]
	#Prm[{{nameof(CaretX)}}][量到的插入點 X。]
	#Prm[{{nameof(CaretY)}}][量到的插入點 Y。]
	#Prm[{{nameof(HasForeground)}}][是否取得前景視窗矩形。]
	#Prm[{{nameof(FgLeft)}}][前景視窗左。]
	#Prm[{{nameof(FgTop)}}][前景視窗上。]
	#Prm[{{nameof(OffsetX)}}][兜底錨點離視窗左邊的位移。]
	#Prm[{{nameof(OffsetY)}}][兜底錨點離視窗上邊的位移。]
	#Prm[{{nameof(X)}}][錨點 X；(0,0) 表示「未知」。]
	#Prm[{{nameof(Y)}}][錨點 Y。]
	""")]
	public static void ResolveAnchor(
		bool CaretValid, i32 CaretX, i32 CaretY,
		bool HasForeground, i32 FgLeft, i32 FgTop,
		i32 OffsetX, i32 OffsetY,
		out i32 X, out i32 Y
	){
		if(CaretValid){
			X = CaretX;
			Y = CaretY;
			return;
		}
		if(HasForeground){
			X = FgLeft + OffsetX;
			Y = FgTop + OffsetY;
			return;
		}
		// (0,0) 由接收端解釋成「不知道位置」：候選窗因此不會被搬到螢幕原點。
		X = 0;
		Y = 0;
	}
}
