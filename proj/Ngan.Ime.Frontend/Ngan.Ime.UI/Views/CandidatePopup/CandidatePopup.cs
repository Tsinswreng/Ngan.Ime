namespace Ngan.Ime.UI.Views.CandidatePopup;

using System;
using Avalonia;
using Avalonia.Threading;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
{{nameof(ICandidatePopup)}} 的桌面實現：把呼叫切到 UI 執行緒，持有唯一的浮窗。

*為何要切執行緒*：服務端的管道迴圈跑在背景執行緒，控件只能在 UI 執行緒上碰。
呼叫方不必知道這件事，切換在這一層完成。

*為何視窗延後建立*：Android 等沒有視窗的宿主也可能註冊本類但從不呼叫；
延後到第一次 {{nameof(Show)}} 才建，避免在沒有桌面生命週期的行程裡造 Window。

*為何只有一個實例*：候選窗是全域唯一（同時只會有一個輸入上下文在組字），
多開會出現兩個浮窗搶位置。

#Eg[
	```cs
	Popup.Show("ni", Page, new PixelPoint(100, 200));
	```
]
""")]
public sealed class CandidatePopup : ICandidatePopup{

	[Doc("唯一的浮窗；第一次顯示時才建立，之後重用。")]
	ViewCandidatePopup? Window;

	[Doc("浮窗的 ViewModel；與視窗同時建立。")]
	VmCandidatePopup? Vm;

	[Doc($$"""
	顯示或更新浮窗。
	#Prm[{{nameof(Preedit)}}][組字串。]
	#Prm[{{nameof(Page)}}][候選頁快照。]
	#Prm[{{nameof(Anchor)}}][插入點的螢幕座標；null 表示未知，浮窗保持原位。]
	""")]
	public void Show(str Preedit, CandidatePage Page, PixelPoint? Anchor){
		Dispatcher.UIThread.Post(() => {
			try{
				EnsureWindow();
				Vm!.Apply(Preedit, Page);
				Window!.Refresh();
				// 位置未知時不動視窗：寧可留在上一次的位置，也不要把浮窗搬到螢幕原點。
				if(Anchor is {} Point){
					Window.MoveTo(Point);
				}
			}catch(Exception Ex){
				// 浮窗壞掉不該拖垮輸入：記日誌、收起，下次按鍵再試。
				AppLog.Error(Ex, "[CandidatePopup] 顯示失敗");
				Hide();
			}
		});
	}

	[Doc("收起浮窗；尚未建立過就只清 VM。")]
	public void Hide(){
		Dispatcher.UIThread.Post(() => {
			try{
				Vm?.Clear();
				Window?.Hide();
			}catch(Exception Ex){
				AppLog.Error(Ex, "[CandidatePopup] 收起失敗");
			}
		});
	}

	[Doc("首次使用時建立 ViewModel 與視窗。")]
	void EnsureWindow(){
		if(Window is not null){
			return;
		}
		Vm = VmCandidatePopup.Mk();
		Window = new ViewCandidatePopup(Vm);
	}
}
