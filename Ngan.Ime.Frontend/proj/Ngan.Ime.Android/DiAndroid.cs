namespace Ngan.Ime.Android;

using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCore;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.Rime;
using Ngan.Ime.UI;

[Doc($$"""
Android 宿主的依賴注入裝配。

*只放本端特有的實現*：作業系統按鍵處理器（目前是過渡樁）。
Rime 引擎、日誌源、界面 ViewModel 分別由 {nameof(DiRime)}／{nameof(DiUi)} 負責。
鍵盤宿主與剪貼簿需要執行期的 Context／Service 實例，故仍由各入口自行註冊。

*兩個入口共用本方法*：`Application`（給 MainActivity）與 `InputMethodService`（給輸入法本身）
各建一個容器，但服務清單必須一致，否則兩邊功能會分岔。

#Eg[
	```cs
	Svc.SetupCore().SetupCfg(AppCfg.Inst).SetupRime().SetupAndroid().SetupUi();
	```
]
""")]
public static class DiAndroid {
	[Doc($$"""
	註冊 Android 端的實現。
	#Rtn[同一個 {nameof(IServiceCollection)}，便於串接。]
	""")]
	public static IServiceCollection SetupAndroid(
		this IServiceCollection z
	){
		// 目前仍是過渡樁；真正的 OsKeyProcessor 由 InputMethodService 在 OnCreate 之後按需替換。
		z.AddSingleton<IOsKeyProcessor, AndroidStubOsKeyProcessor>();
		return z;
	}
}
