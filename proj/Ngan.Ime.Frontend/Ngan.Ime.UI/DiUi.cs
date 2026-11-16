namespace Ngan.Ime.UI;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ngan.Ime.UI.Infra;
using Ngan.Ime.UI.Views.CandidatePopup;
using Ngan.Ime.UI.Views.CandidatesBar;
using Ngan.Ime.UI.Views.Clipboard;
using Ngan.Ime.UI.Views.Ime;
using Ngan.Ime.UI.Views.Input;
using Ngan.Ime.UI.Views.KeyBoard;
using Ngan.Ime.UI.Views.RimeLog;
using Ngan.Ime.UI.Views.StatusTip;
using Ngan.Ime.UI.Views.ToolBar;
using Ngan.Ime.UI.Views.VoiceInput;

[Doc($$"""
界面程序集的依賴注入裝配。

*只註冊本程序集提供的東西*：共享界面狀態、日誌緩衝與各 ViewModel。
引擎（{{nameof(Ngan.Ime.Core.Ime.IImeState)}} 等）由 `DiRime` 或宿主提供；
鍵盤宿主與剪貼板由各宿主提供（骨架宿主用 {{nameof(NoopKeyboardHost)}}／{{nameof(NoopClipboardService)}}）。

*生命週期*：
- 共享狀態（{{nameof(ImeUiState)}}、{{nameof(EngineLogBuffer)}}）與「根 VM」{{nameof(VmIme)}} 用 singleton：
  分體鍵盤的頂條與主界面必須看到同一份狀態，否則會出現「這邊切了、那邊不知道」。
- 各頁 VM 也用 singleton：它們只是狀態投影，多處共用反而避免重複訂閱。
- {{nameof(VmKeyBoard)}} 用 transient：每個鍵盤視圖（主／左半／右半）各一份，
  它持有該視圖的鍵集合；布局狀態本身在 {{nameof(ImeUiState)}} 裡共享。

#Eg[
	```cs
	Svc.SetupCore().SetupCfg(AppCfg.Inst).SetupUi();
	```
]
""")]
public static class DiUi{

	[Doc($$"""
	註冊界面層的服務。
	#Rtn[同一個 {{nameof(IServiceCollection)}}，便於串接。]
	""")]
	public static IServiceCollection SetupUi(this IServiceCollection z){
		// 共享狀態：多個 ViewModel 都投影同一份。
		z.AddSingleton<ImeUiState>(Svc => new ImeUiState(
			Svc.GetRequiredService<Tsinswreng.CsCfg.ICfgReader>(),
			Svc.GetRequiredService<Tsinswreng.CsCfg.ICfgAccessor>()));
		// 引擎日誌緩衝：骨架宿主沒有引擎日誌源，退回空實例而不是讓解析失敗。
		z.AddSingleton<EngineLogBuffer>(Svc => {
			var LogSrc = Svc.GetService<Ngan.Ime.Infra.Log.IEngineLogSource>();
			return LogSrc is null ? EngineLogBuffer.Mk() : new EngineLogBuffer(LogSrc);
		});

		z.AddSingleton<VmIme>();
		z.AddSingleton<VmInput>();
		z.AddSingleton<VmToolBar>();
		z.AddSingleton<VmCandidatesBar>();
		z.AddSingleton<VmClipboard>();
		z.AddSingleton<VmRimeLog>();
		z.AddSingleton<VmVoiceInput>();
		z.AddSingleton<VmVoiceInputLangSelector>();
		z.AddTransient<VmKeyBoard>();
		// 桌面候選浮窗：只有 TSF 服務端會解析它，其他宿主不碰就不會建視窗。
		z.AddSingleton<ICandidatePopup, CandidatePopup>();
		// 光標旁的狀態提示（中／英、簡／繁）；同樣只有 TSF 服務端會用到。
		z.AddSingleton<IStatusTip, StatusTip>();
		return z;
	}

	[Doc($$"""
	骨架宿主（桌面預覽／Browser／iOS）的降級實現：只在宿主沒有註冊時補上。
	#Rtn[同一個 {{nameof(IServiceCollection)}}，便於串接。]
	""")]
	public static IServiceCollection SetupUiFallbackHosts(this IServiceCollection z){
		z.TryAddSingleton<IKeyboardHost, NoopKeyboardHost>();
		z.TryAddSingleton<IClipboardService, NoopClipboardService>();
		return z;
	}
}
