namespace Ngan.Ime.Android;

using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCore;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI;

[Doc($$"""
Android 宿主的依賴注入裝配。

*只放本端特有的實現*：作業系統按鍵處理器（含使用者可切的映射層）。
Rime 引擎、日誌源、界面 ViewModel 分別由 `DiRime`／`DiUi` 負責；
鍵盤宿主與剪貼板需要執行期 Context，故由入口自行補。

*為何不再註冊「過渡樁」*：舊版註冊 `AndroidStubOsKeyProcessor`，真正的處理器卻由
`InputMethodService.OnCreate` 直接賦值給 `ISvcIme.OsKeyProcessor`——容器說的與實際跑的不是同一件事。
現在真處理器由本方法裝配，`CurrentInputConnection` 經 {{nameof(AndroidImeRuntime)}} 取得。

#Eg[
	```cs
	Svc.SetupAndroid();
	```
]
""")]
public static class DiAndroid{
	[Doc($$"""
	註冊 Android 端的實現。
	#Rtn[同一個 {{nameof(IServiceCollection)}}，便於串接。]
	""")]
	public static IServiceCollection SetupAndroid(
		this IServiceCollection z
	){
		z.AddSingleton(AndroidImeRuntime.Inst);
		// 未處理按鍵：先映射（使用者可切），再交給 Android 輸入連接。
		z.AddSingleton<IOsKeyProcessor>(Svc => {
			var Runtime = Svc.GetRequiredService<AndroidImeRuntime>();
			var Ui = Svc.GetRequiredService<ImeUiState>();
			return new KeyRemappingOsKeyProcessor(
				new AndroidOsKeyProcessor(() => Runtime.GetInputConnection?.Invoke()),
				new KeyEventRemapper(
					() => Ui.IsSystemKeyRemappingEnabled,
					// 手機輸入法的退格常被宿主誤判為長按刪除；映射為右 Alt 是本產品的既定行為。
					[new KeyRemapRule(KeyChars.Backspace, KeyChars.Alt_R)]
				)
			);
		});
		return z;
	}
}
