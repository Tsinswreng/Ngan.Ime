namespace Ngan.Ime.UI;

using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCore;
using Ngan.Ime.UI.Views.CandidatesBar;
using Ngan.Ime.UI.Views.Clipboard;
using Ngan.Ime.UI.Views.Ime;
using Ngan.Ime.UI.Views.Input;
using Ngan.Ime.UI.Views.Key;
using Ngan.Ime.UI.Views.KeyBoard;
using Ngan.Ime.UI.Views.RimeLog;
using Ngan.Ime.UI.Views.ToolBar;
using Ngan.Ime.UI.Views.VoiceInput;

[Doc($$"""
Ui 程序集（平臺無關界面）的依賴注入裝配。

*只註冊本程序集提供的東西*：各 ViewModel 與界面共享狀態。宿主專屬實現（鍵盤宿主、
剪貼簿、Rime 引擎）由各宿主的 `DiXxx` 註冊。

#Eg[
	```cs
	Svc.SetupCore().SetupCfg(AppCfg.Inst).SetupUi();
	```
]
""")]
public static class DiUi {
	[Doc($$"""
	註冊界面層的服務。

	*生命週期*：ViewModel 用 transient（每次解析都是新的一份，符合視圖生命週期）；
	{{nameof(ImeUiState)}} 與 {{nameof(RimeLogBuffer)}} 用 singleton——它們是全界面共享的狀態，
	每個視圖各拿一份會導致「這邊切了分體、那邊還顯示整塊」。

	#Rtn[同一個 {nameof(IServiceCollection)}，便於串接。]
	""")]
	public static IServiceCollection SetupUi(
		this IServiceCollection z
	) {
		// 共享狀態：多個 ViewModel 都投影同一份。
		z.AddSingleton<ImeUiState>(_ => ImeUiState.Mk());
		z.AddSingleton<RimeLogBuffer>();

		z.AddTransient<VmIme>();
		z.AddTransient<VmToolBar>();
		z.AddTransient<VmCandidatesBar>();
		z.AddTransient<VmInput>();
		z.AddTransient<VmClipboard>();
		z.AddTransient<VmRimeLog>();
		z.AddTransient<VmKey>();
		z.AddTransient<VmKeyBoard>();
		z.AddTransient<VmVoiceInput>();
		z.AddTransient<VmVoiceInputLangSelector>();
		return z;
	}
}
