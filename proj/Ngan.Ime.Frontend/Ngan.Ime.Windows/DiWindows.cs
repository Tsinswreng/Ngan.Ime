namespace Ngan.Ime.Windows;

using Microsoft.Extensions.DependencyInjection;
using Ngan.Ime.Infra;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI;
using Ngan.Ime.Windows.Tsf;

[Doc($$"""
Windows 服務端的依賴注入裝配。

*只放本端特有的實現*：作業系統按鍵處理器。Rime 引擎、日誌源、界面 ViewModel
分別由 `DiRime`／`DiUi` 負責；鍵盤宿主與剪貼板用界面層的降級實現
（Windows 端還沒接真正的輸入法宿主，TSF 外殼走另一條管道）。

#Eg[
	```cs
	Svc.SetupWindows();
	```
]
""")]
public static class DiWindows{
	[Doc($$"""
	註冊 Windows 端的實現。
	#Rtn[同一個 {{nameof(IServiceCollection)}}，便於串接。]
	""")]
	public static IServiceCollection SetupWindows(
		this IServiceCollection z
		,OptHost Opt
	){
		// TSF 模式下「未處理的按鍵」要交回 TSF 外殼（回 IsEaten=0），由系統交給前景應用；
		// 若在這裡再用 keybd_event 補發，按鍵會重新進入 TSF，形成同一顆鍵的無限迴圈。
		if(Opt.DaemonMode){
			z.AddSingleton<IOsKeyProcessor, NoopOsKeyProcessor>();
		}else{
			// 桌面視窗模式：界面持有焦點、沒有外殼代收，故由服務端合成按鍵給前景視窗。
			z.AddSingleton<IOsKeyProcessor, WindowsKeyProcessor>();
		}
		// TSF 管道服務端：按鍵經具名管道進來，交給引擎，再把結果回給外殼。
		z.AddSingleton<ISvcTsfPipeServer, SvcTsfPipeServer>();
		// 模式切換提示（中／英、簡／繁）：訂閱 IImeState，切換時在光標旁閃一下。
		z.AddSingleton<SvcModeTip>();
		return z;
	}
}
