namespace Ngan.Ime.UI.Views.CandidatePopup;

using System;
using Avalonia;
using Avalonia.Threading;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Infra;

[Doc($$"""
{{nameof(ICandidatePopup)}} 的桌面實現：把呼叫切到 UI 執行緒，持有唯一的浮窗。

*為何要切執行緒*：服務端的管道迴圈跑在背景執行緒，控件只能在 UI 執行緒上碰。
呼叫方不必知道這件事，切換在這一層完成。

*為何視窗延後建立*：Android 等沒有視窗的宿主也可能註冊本類但從不呼叫；
延後到第一次 {{nameof(ShowAt)}} 才建，避免在沒有桌面生命週期的行程裡造 Window。

*為何只有一個實例*：候選窗是全域唯一（同時只會有一個輸入上下文在組字），
多開會出現兩個浮窗搶位置。

#Eg[
	```cs
	Popup.ShowAt("ni", Page, 100, 200, 184);
	```
]
""")]
public sealed class CandidatePopup : ICandidatePopup{

	[Doc("唯一的浮窗；第一次顯示時才建立，之後重用。")]
	ViewCandidatePopup? Window;

	[Doc("浮窗的 ViewModel；與視窗同時建立。")]
	VmCandidatePopup? Vm;

	[Doc($$"""
	套上新內容並顯示在指定位置；顯示與定位在同一次 UI 操作裡完成。

	*為何不能拆*：先前是 `Vm.Apply`（內含 `Refresh` → `Show`）之後再 `MoveTo`，
	兩者各是一則 UI 訊息，中間會渲染一幀，於是每顆鍵都「先出現在舊位置、再跳一次」。
	#Prm[{{nameof(Preedit)}}][組字串。]
	#Prm[{{nameof(Page)}}][候選頁快照。]
	#Prm[{{nameof(X)}}][插入點左邊。]
	#Prm[{{nameof(Y)}}][插入點下緣。]
	#Prm[{{nameof(Top)}}][插入點矩形上緣。]
	""")]
	public void ShowAt(str Preedit, CandidatePage Page, i32 X, i32 Y, i32 Top){
		Dispatcher.UIThread.Post(() => {
			try{
				EnsureWindow();
				// Apply 收尾會發一次 Applied，視圖訂閱它刷新內容；刷新不再自己 Show。
				Vm!.Apply(Preedit, Page);
				// 內容就緒之後才排版、定位、顯示——一次完成。
				Window!.ShowAt(new PixelPoint(X, Y), Top);
			}catch(Exception Ex){
				// 浮窗壞掉不該拖垮輸入：記日誌、收起，下次按鍵再試。
				AppLog.Error(Ex, "[CandidatePopup] 顯示失敗");
				Hide();
			}
		});
	}

	[Doc($$"""
	把浮窗移到插入點；還沒建立過浮窗就什麼都不做（沒有東西可移）。
	#Prm[{{nameof(X)}}][插入點左邊（螢幕座標）。]
	#Prm[{{nameof(Y)}}][插入點下緣（螢幕座標）。]
	#Prm[{{nameof(Top)}}][插入點矩形上緣。]
	""")]
	public void MoveTo(i32 X, i32 Y, i32 Top){
		Dispatcher.UIThread.Post(() => {
			try{
				if(Window is null){
					return;
				}
				Window.MoveTo(new PixelPoint(X, Y), Top);
			}catch(Exception Ex){
				// 移動失敗只影響位置，不該把候選內容一起收掉。
				AppLog.Error(Ex, "[CandidatePopup] 移動失敗");
			}
		});
	}

	[Doc("收起浮窗；尚未建立過就只清 VM。")]
	public void Hide(){
		Dispatcher.UIThread.Post(() => {
			try{
				Vm?.Clear();
				Window?.Hide();
				// 照 Weasel：sticky 只活到視窗收起為止（Weasel 是銷毀視窗時歸零，
				// 我們重用同一個視窗，所以在收起時歸零），否則一旦翻到上方就永遠在上方。
				if(Window is not null){
					Window.Sticky = false;
				}
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
