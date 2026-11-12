namespace Ngan.Ime.Windows;

using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCore;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI;

[Doc($$"""
Windows 宿主的依賴注入裝配。

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
	){
		// TSF 模式下「未處理的按鍵」要交回 TSF 外殼（回 IsEaten=0），由系統交給前景應用；
		// 若在這裡再用 keybd_event 補發，按鍵會重新進入 TSF，形成同一顆鍵的無限迴圈。
		if(App.DaemonMode){
			z.AddSingleton<IOsKeyProcessor, NoopOsKeyProcessor>();
		}else{
			// 桌面視窗模式：界面持有焦點、沒有外殼代收，故由宿主合成按鍵給前景視窗。
			z.AddSingleton<IOsKeyProcessor, WindowsKeyProcessor>();
		}
		return z;
	}
}
