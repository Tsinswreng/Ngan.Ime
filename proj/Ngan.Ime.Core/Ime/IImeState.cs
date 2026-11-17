namespace Ngan.Ime.Core.Ime;

using System.ComponentModel;

[Doc($$"""
輸入法的「可觀察狀態」，界面只讀。

*為何與輸入管線分開*：需要讀狀態的地方（鍵盤高亮、候選欄、preedit 顯示）遠多於需要送按鍵的地方；
把兩者放同一個介面，等於逼每個只讀的視圖都拿到「送鍵」與「切換模式」的能力（介面隔離原則）。

*為何繼承 {{nameof(INotifyPropertyChanged)}}*：這是 BCL 的通知契約，不是某個 MVVM 框架的型別，
故抽象層可以安全地依賴它；`CommunityToolkit` 之類只出現在界面層的實現裡。

#Eg[
	```cs
	IImeState State = Di.GetRSvc<IImeState>();
	bool Composing = State.IsComposing;
	```
]
""")]
public interface IImeState : INotifyPropertyChanged{
	[Doc("引擎是否已連接（初始化成功）。")]
	public bool IsConnected{get;}

	[Doc("是否正在連接／初始化引擎。")]
	public bool IsConnecting{get;}

	[Doc("是否為英文（ASCII）模式。")]
	public bool IsAsciiMode{get;}

	[Doc("是否為簡體輸出模式。")]
	public bool IsSimplification{get;}

	[Doc("引擎是否認為「正在組字」。")]
	public bool IsComposing{get;}

	[Doc("當前預編輯字串；沒有組字時為空串。")]
	public str Preedit{get;}

	[Doc("當前候選頁快照；沒有候選時是一個空頁（不為 null）。")]
	public CandidatePage Candidates{get;}

	[Doc($$"""
	引擎發來「模式選項通知」；參數是選項名與新值（`ascii_mode`、`simplification`）。

	*為何不能只用 {{nameof(INotifyPropertyChanged)}}*：那是「值變了」的通知，
	而 Weasel 的模式提示是「引擎發出 option 通知」就顯示——`set_option` 即使值沒變
	也會發通知（例如已經是英文模式、又按下切換鍵時）。照 Weasel 0.15.0
	`RimeWithWeasel.cpp` 的 `_ShowMessage`：`option` 通知進來就 `ShowWithTimeout`，
	與值有沒有變無關；而它**不會**每顆鍵都彈（每顆鍵都彈是我們自己加的，已刪）。
	""")]
	public event Action<str, bool>? OptionNotified;
}
