namespace Ngan.Ime.UI.Views.CandidatePopup;

using Avalonia;

[Doc($$"""
候選浮窗的錨點：插入點矩形在**螢幕物理像素**座標下的左邊、下緣、上緣。

*為何要獨立成型別*：`PixelPoint` 只有一個點，而定位需要「左邊 ＋ 下緣 ＋ 上緣」三件事
——上緣是翻到文字行上方時唯一能保證不壓字的依據。
""")]
public sealed class PopupAnchor{

	[Doc("插入點矩形的左邊（螢幕物理像素）。")]
	public i32 X{get;set;}

	[Doc("插入點矩形的下緣（螢幕物理像素）。")]
	public i32 Y{get;set;}

	[Doc("插入點矩形的上緣（螢幕物理像素）；翻到上方時用它。")]
	public i32 Top{get;set;}
}

[Doc($$"""
一次定位的結果：左上角座標、是否翻到上方、候選行是否倒排。
""")]
public sealed class PopupPlacement{

	[Doc("視窗左上角的螢幕 X（物理像素）。")]
	public i32 X{get;set;}

	[Doc("視窗左上角的螢幕 Y（物理像素）。")]
	public i32 Y{get;set;}

	[Doc("是否翻到插入點上方。")]
	public bool Flip{get;set;}

	[Doc("候選行是否倒排（翻到上方時第一候選貼着光標）。")]
	public bool Reversed{get;set;}
}

[Doc($$"""
候選浮窗的定位規則；純幾何，不碰視窗、不碰 UI 執行緒，故可用數字直接驗。

*以 Weasel 0.15.0 為準*（使用者實際在用的版本），逐條對照
`WeaselUI/WeaselPanel.cpp` 的 `_RepositionWindow`（該 tag 的第 869-928 行）：

+ 放在下面時，面板上緣就是插入點矩形的**下緣**——0.15.0 沒有 `OffsetRect(0, 6)`，
	那個 6px 間距是 0.16 之後才加的（0.15.0 的 `MoveTo` 是 `m_inputPos = rc`）；
+ 能不能放下面：`y > rcWorkArea.bottom`，而 `rcWorkArea.bottom` 已先減去視窗高度
	（`:892-893`、`:913`），等價於「插入點下緣 + 高度 > 工作區下緣」；
+ 翻到上方時：`y = m_inputPos.top - height`（`:915`），即「矩形上緣 − 高度」；
+ 翻上去時行序倒排（`m_istorepos`，`:916-918`，`vertical_auto_reverse`）；
+ x 夾在插入點所在螢幕的工作區內（`:908-909`），y 不得高於工作區上緣（`:924`）。

*0.15.0 沒有 sticky*：它每次都由當下的插入點重新判斷翻不翻，不會「翻過一次就固定在上方」；
那是 0.16 之後才有的 `m_sticky`。這裡照 0.15.0，不加。

*單位*：全部是物理像素。Weasel 從頭到尾只在像素下算（`GetWindowRect`、`MonitorFromRect`、
`GetMonitorInfo` 的 `rcWork` 都是像素）；呼叫端必須先把 Avalonia 的版面單位（DIP）乘上
螢幕縮放再傳進來，否則在 125%／150% 縮放下翻上去的位置會少算，面板下緣就會壓進文字行。
""")]
public static class PopupPlacer{

	[Doc($$"""
	算出候選窗該擺在哪裡。
	#Prm[{{nameof(Anchor)}}][插入點矩形（螢幕物理像素）。]
	#Prm[{{nameof(WidthPx)}}][候選窗的實際寬度（物理像素）。]
	#Prm[{{nameof(HeightPx)}}][候選窗的實際高度（物理像素）。]
	#Prm[{{nameof(Work)}}][插入點所在螢幕的工作區（物理像素）。]
	#Rtn[定位結果。]
	""")]
	public static PopupPlacement Compute(PopupAnchor Anchor, i32 WidthPx, i32 HeightPx, PixelRect Work){
		// 1: 放在下面的話，上緣就是插入點矩形的下緣（0.15.0 不加間距）。
		var Below = Anchor.Y;
		// 2: 放不下就翻到插入點上緣之上：面板下緣剛好貼着文字行上緣。
		var Flip = Below + HeightPx > Work.Bottom;
		var Y = Flip ? Anchor.Top - HeightPx : Below;
		// 3: x 對齊插入點左邊，再夾進工作區。
		var X = Anchor.X;
		var MaxX = Work.Right - WidthPx;
		if(X > MaxX){
			X = MaxX;
		}
		if(X < Work.X){
			X = Work.X;
		}
		// 4: 上方空間也不夠時不要跑到工作區外（照 0.15.0 的 `if (y < rcWorkArea.top)`）。
		if(Y < Work.Y){
			Y = Work.Y;
		}
		return new(){
			X = X,
			Y = Y,
			Flip = Flip,
			// 照 `m_istorepos`：翻到上方時候選行由下往上排，第一候選貼着光標。
			Reversed = Flip,
		};
	}
}
