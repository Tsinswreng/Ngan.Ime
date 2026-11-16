namespace Ngan.Ime.UI.Views.StatusTip;

using System;
using Avalonia;
using Avalonia.Threading;
using Ngan.Ime.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
{{nameof(IStatusTip)}} 的桌面實現：切 UI 執行緒、延後建窗、自動收起。

*為何自動收起*：Weasel 的狀態圖示只在「沒在組字」時常駐；我們沒有常駐的必要，
切完模式閃一下就夠，否則會一直擋在光標旁邊。
""")]
public sealed class StatusTip : IStatusTip{

	[Doc("提示視窗；第一次顯示時才建立。")]
	ViewStatusTip? Window;

	[Doc("自動收起的計時器；每次 Show 重新計時。")]
	DispatcherTimer? Timer;

	[Doc("提示停留時間（毫秒）。")]
	const i32 VisibleMs = 900;

	[Doc($$"""
	顯示提示；Anchor 為 null 時只更新內容、不動位置。
	#Prm[{{nameof(Text)}}][要顯示的字。]
	#Prm[{{nameof(Anchor)}}][插入點下緣的螢幕座標。]
	""")]
	public void Show(str Text, PixelPoint? Anchor){
		Dispatcher.UIThread.Post(() => {
			try{
				EnsureWindow();
				Window!.SetText(Text);
				// 先顯示再定位：SizeToContent 的視窗要排版後才有正確尺寸，
				// 否則第一次貼邊時會用 (0,0) 的寬高去算。
				Window.Show();
				if(Anchor is {} Point){
					Window.MoveTo(Point);
				}
				RestartTimer();
			}catch(Exception Ex){
				AppLog.Error(Ex, "[StatusTip] 顯示失敗");
				return;
			}
			AppLog.Info($"[Tip] shown text={Text} x={(Anchor?.X ?? -1)} y={(Anchor?.Y ?? -1)} visible={Window?.IsVisible}");
		});
	}

	[Doc("收起提示；已收起時無副作用。")]
	public void Hide(){
		Dispatcher.UIThread.Post(() => {
			try{
				Timer?.Stop();
				Window?.Hide();
			}catch(Exception Ex){
				AppLog.Error(Ex, "[StatusTip] 收起失敗");
			}
		});
	}

	[Doc("首次使用時建立視窗。")]
	void EnsureWindow(){
		if(Window is not null){
			return;
		}
		Window = new ViewStatusTip();
	}

	[Doc("重新計時；到點就收起。")]
	void RestartTimer(){
		Timer ??= new DispatcherTimer{ Interval = TimeSpan.FromMilliseconds(VisibleMs) };
		Timer.Stop();
		Timer.Tick -= OnTick;
		Timer.Tick += OnTick;
		Timer.Start();
	}

	[Doc("計時到點：收起並停止計時。")]
	void OnTick(object? Sender, EventArgs Args){
		Timer?.Stop();
		Window?.Hide();
	}
}
