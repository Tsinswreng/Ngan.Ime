namespace Ngan.Ime.UI.Views.CandidatePopup;

using Avalonia;
using Ngan.Ime.Core.Ime;

[Doc($$"""
桌面候選浮窗的對外介面。

*為何要這一層*：TSF 服務端只該知道「把候選顯示在游標旁」與「收起」，
不該知道那是 Avalonia 的視窗、更不該碰 UI 執行緒。
有了這個介面，服務端可以在沒有界面的環境裡換成空實現，測試也能塞替身。

*為何座標可空*：「插入點未知」與「插入點在 (0,0)」是兩件事。協議用 (0,0) 當未知的哨兵，
	呼叫端必須把它翻成 null，否則量測一失敗浮窗就會被搬到螢幕左上角——這正是先前的症狀。

*已知限制：全機只有一個浮窗*。協定只帶按鍵與座標，沒有「哪個應用／哪個輸入上下文」的身分，
	故兩個應用同時組字時，後到的 Show 會把前一個的浮窗搬走並覆寫內容。
	真正的修法是在請求裏帶 client 標識（外殼的 pid 或 TSF 的 ClientId）並按它分桶；
	那要同时改協議、外殼、服務端與浮窗，屬於另一輪。
	在補上之前，請把本介面當成「單一前景組字」的實現，不要假設它能同時服務多個應用。

#Eg[
	```cs
	Popup.Show("ni", Page, new PixelPoint(100, 200));
	```
]
""")]
public interface ICandidatePopup{

	[Doc($$"""
	套上新內容並顯示在指定位置；顯示與定位在同一次 UI 操作裡完成。

	*為何不能拆成 Show ＋ MoveTo*：拆開就會「先畫在舊位置、下一幀再搬」，
	每一顆鍵都會跳一下。照 Weasel：位置算好、視窗才第一次出現。
	#Prm[{{nameof(Preedit)}}][組字串；空串表示只顯示候選。]
	#Prm[{{nameof(Page)}}][候選頁快照。]
	#Prm[{{nameof(X)}}][插入點左邊。]
	#Prm[{{nameof(Y)}}][插入點下緣。]
	#Prm[{{nameof(Top)}}][插入點矩形上緣。]
	""")]
	public void ShowAt(str Preedit, CandidatePage Page, i32 X, i32 Y, i32 Top);

	[Doc($$"""
	把浮窗移到插入點：插入點下緣為基準，浮窗往下 6px 開始；下方放不下就翻到插入點上方。
	#Prm[{{nameof(X)}}][插入點左邊（螢幕座標）。]
	#Prm[{{nameof(Y)}}][插入點下緣（螢幕座標）。]
	#Prm[{{nameof(Top)}}][插入點矩形上緣；翻到上方時用它定位，浮窗才不會蓋住文字行。]
	""")]
	public void MoveTo(i32 X, i32 Y, i32 Top);

	[Doc("收起浮窗；已收起時無副作用。")]
	public void Hide();
}
