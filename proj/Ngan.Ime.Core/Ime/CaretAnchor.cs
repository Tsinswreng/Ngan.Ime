namespace Ngan.Ime.Core.Ime;

using Tsinswreng.CsCore;

[Doc($$"""
最近一次量到的插入點位置（螢幕座標、取插入點下緣）；由外殼推送、界面層讀取。

*為何需要*：候選浮窗與狀態提示都是「貼着光標」的視窗，而光標位置只有外殼拿得到
（`ITfContextView::GetTextExt`）。外殼每次量到就推一條 `P|ver|x|y`，服務端把它記在這裡，
界面層不必知道協議，也能在需要時把視窗擺到正確位置。

*為何是單例*：同一時間只有一個輸入焦點；協定也還沒有 client 身分
（Weasel 有 `session_id`，本專案留待統一接口抽取時一併做）。
""")]
public sealed class CaretAnchor{

	[Doc("插入點下緣的螢幕 X。")]
	public i32 X{get;set;}

	[Doc("插入點下緣的螢幕 Y。")]
	public i32 Y{get;set;}

	[Doc("是否量到過；false 時界面層不該把視窗搬到 (0,0)。")]
	public bool Has{get;set;}

	[Doc($$"""
	記下一次量測結果。
	#Prm[{{nameof(X)}}][插入點下緣的螢幕 $X$。]
	#Prm[{{nameof(Y)}}][插入點下緣的螢幕 $Y$。]
	""")]
	public void Set(i32 X, i32 Y){
		this.X = X;
		this.Y = Y;
		Has = true;
	}
}
