namespace Ngan.Ime.Tsf.Ipc;


[Doc($$"""
插入點座標的採用規則；純幾何，不碰 COM 也不碰 Win32。

*為何要抽出來*：候選窗黏在螢幕左上角那件事（2026-11-15）查了很久，根因有三層：
量測的狀態機、矩形的可信度判斷、以及「量不到時退到哪裡」。前兩層修完之後，
「什麼矩形可以用」「錨點取哪裡」這些判斷仍然埋在 static 類裡，只能真機驗。
抽成純函數之後可以直接餵數字測。

*三條規則*（與殼裡的註釋一致，不要各自演化）：
+ 矩形四邊全零 → 不可用（有些控件在沒有版面時回 0,0,0,0）。
+ 錨點要落在前景視窗的 ±{{nameof(SlackPx)}} 內；否則代表那個矩形不是螢幕座標系，直接用會跑到螢幕外。
+ 錨點取矩形的左下角（`Left`, `Bottom`）＝插入點下方，候選窗從那裡往下排。
""")]
public static class TsfCaretPolicy{

	[Doc("錨點允許超出前景視窗的像素數；用來吸收「插入點貼在邊界上」的情況。")]
	public const i32 SlackPx = 64;

	[Doc($$"""
	這個矩形能不能當插入點用。
	#Prm[{{nameof(Left)}}][矩形左邊（螢幕座標）。]
	#Prm[{{nameof(Top)}}][矩形上邊。]
	#Prm[{{nameof(Right)}}][矩形右邊。]
	#Prm[{{nameof(Bottom)}}][矩形下邊。]
	#Prm[{{nameof(FgLeft)}}][前景視窗左邊；前景未知時傳 0 並讓 {{nameof(HasForeground)}} 為 false。]
	#Prm[{{nameof(FgTop)}}][前景視窗上邊。]
	#Prm[{{nameof(FgRight)}}][前景視窗右邊。]
	#Prm[{{nameof(FgBottom)}}][前景視窗下邊。]
	#Prm[{{nameof(HasForeground)}}][是否取得前景視窗矩形；false 時跳過範圍檢查。]
	#Prm[{{nameof(X)}}][採用的錨點 X。]
	#Prm[{{nameof(Y)}}][採用的錨點 Y。]
	#Rtn[true 表示採用。]
	""")]
	public static bool TryAdopt(
		i32 Left, i32 Top, i32 Right, i32 Bottom,
		i32 FgLeft, i32 FgTop, i32 FgRight, i32 FgBottom,
		bool HasForeground,
		out i32 X, out i32 Y
	){
		X = 0;
		Y = 0;
		if(Left == 0 && Top == 0 && Right == 0 && Bottom == 0){
			return false;
		}
		X = Left;
		Y = Bottom;
		if(HasForeground && !IsNear(FgLeft, FgTop, FgRight, FgBottom, X, Y)){
			X = 0;
			Y = 0;
			return false;
		}
		return true;
	}

	[Doc($$"""
	點是否落在視窗範圍內（往外放寬 {{nameof(SlackPx)}} 像素）。
	#Prm[{{nameof(Left)}}][視窗左。]
	#Prm[{{nameof(Top)}}][視窗上。]
	#Prm[{{nameof(Right)}}][視窗右。]
	#Prm[{{nameof(Bottom)}}][視窗下。]
	#Prm[{{nameof(X)}}][點 X。]
	#Prm[{{nameof(Y)}}][點 Y。]
	#Rtn[true 表示在範圍內。]
	""")]
	public static bool IsNear(i32 Left, i32 Top, i32 Right, i32 Bottom, i32 X, i32 Y){
		return X >= Left - SlackPx && X <= Right + SlackPx
			&& Y >= Top - SlackPx && Y <= Bottom + SlackPx;
	}

	[Doc($$"""
	送給服務端的錨點；三級：量到的插入點 → 前景視窗左上角加位移 → (0,0)（未知）。
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
