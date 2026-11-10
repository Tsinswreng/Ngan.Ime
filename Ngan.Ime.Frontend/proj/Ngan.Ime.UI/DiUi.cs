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

*只註冊本程序集提供的東西*：各 ViewModel。宿主專屬實現（鍵盤宿主、剪貼簿、
Rime 引擎）由各宿主的 `DiXxx` 註冊。

#Eg[
	```cs
	Svc.SetupCore().SetupCfg(AppCfg.Inst).SetupUi();
	```
]
""")]
public static class DiUi {
	[Doc($$"""
	把界面層的 ViewModel 註冊成 transient（每次解析都是新的一份，符合視圖生命週期）。
	#Rtn[同一個 {nameof(IServiceCollection)}，便於串接。]
	""")]
	public static IServiceCollection SetupUi(
		this IServiceCollection z
	) {
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
