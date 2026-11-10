using Ngan.Ime;
using Ngan.Ime.Infra.Storage;
using Ngan.Ime.UI;
using Avalonia;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Ngan.Ime.Desktop;

/// 通用桌面宿主（非 Windows 專用入口；Windows 的正式入口見 Ngan.Ime.Windows）。
/// 目前只裝配基礎設施與界面層，尚未接 Rime 引擎：解析 ISvcIme 會失敗，
/// 故適合作為界面預覽／跨平臺骨架，不是可用的輸入法。
sealed class Program {
	/// 本宿主在日誌與配置裡的自稱。
	const str AppName = "Ngan.Ime.Desktop";

	[STAThread]
	public static void Main(str[] Args){
		var Svc = new ServiceCollection();
		Svc.SetupCore();
		Svc.AddAppLog(MkrLogger.ConsoleEtFile(AppName));
		Svc.AddSingleton<I_GetBaseDir>(AppDirBaseDirMgr.Inst);
		AppCfg.Inst.Setup(AppDirBaseDirMgr.Inst);
		Svc.SetupCfg(AppCfg.Inst);
		Svc.SetupUi();
		Di.SvcProvider = Svc.BuildServiceProvider();

		BuildAvaloniaApp().StartWithClassicDesktopLifetime(Args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp() {
		var Options = new FontManagerOptions {
			DefaultFamilyName = "孤鹜 筑紫明朝",  // 默认字体
			FontFallbacks = new[] { new FontFallback { FontFamily = "Microsoft YaHei" } } // 备选字体
		};
		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			//.WithInterFont()
			.With(Options)
			.LogToTrace();
	}

}
