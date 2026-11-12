namespace Ngan.Ime.Windows;

using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCore;
using Ngan.Ime.Core.Keys;

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
		// Rime 未處理的按鍵交給 Windows 的按鍵發送器。
		z.AddSingleton<IOsKeyProcessor, WindowsKeyProcessor>();
		return z;
	}
}
