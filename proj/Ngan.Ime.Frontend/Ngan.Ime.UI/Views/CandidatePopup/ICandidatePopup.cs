namespace Ngan.Ime.UI.Views.CandidatePopup;

using Avalonia;
using Ngan.Ime.Core.Ime;
using Tsinswreng.CsCore;

[Doc($$"""
桌面候選浮窗的對外介面。

*為何要這一層*：TSF 服務端只該知道「把候選顯示在游標旁」與「收起」，
不該知道那是 Avalonia 的視窗、更不該碰 UI 執行緒。
有了這個介面，服務端可以在沒有界面的環境裡換成空實現，測試也能塞替身。

*為何座標用 {{nameof(PixelPoint)}}*：它是 Avalonia 對「螢幕物理像素座標」的表達，
與視窗定位用的是同一個單位，中間不必再做一次換算。

#Eg[
	```cs
	Popup.Show("ni", Page, new PixelPoint(100, 200));
	```
]
""")]
public interface ICandidatePopup{

	[Doc($$"""
	顯示或更新候選浮窗。
	#Prm[{{nameof(Preedit)}}][組字串；空串表示只顯示候選。]
	#Prm[{{nameof(Page)}}][候選頁快照。]
	#Prm[{{nameof(Anchor)}}][插入點的螢幕座標；浮窗貼在它左下角。]
	""")]
	public void Show(str Preedit, CandidatePage Page, PixelPoint Anchor);

	[Doc("收起浮窗；已收起時無副作用。")]
	public void Hide();
}
