using Ngan.Ime;
using Ngan.Ime.Infra.Storage;
using Ngan.Ime.UI;
using Avalonia;
using Avalonia.Media;

namespace Ngan.Ime.Desktop;

[Doc("""
通用桌面宿主（非 Windows 專用入口；Windows 的正式入口見 Ngan.Ime.Windows）。

*能力*：只有界面與基礎設施，沒有引擎（組合根會補 NoopSvcIme）。
可以做界面預覽與跨平臺骨架，不是可用的輸入法。
""")]
sealed class Program {
	[Doc("本宿主在日誌與配置裡的自稱。")]
	const str AppName = "Ngan.Ime.Desktop";

	[Doc("裝配容器並啟動 Avalonia 桌面應用。")]
	[STAThread]
	public static void Main(str[] Args){
		AppComposition.MkServices(new HostBootstrap{
			AppName = AppName,
			BaseDir = AppDirBaseDirMgr.Inst,
			Logger = MkrLogger.ConsoleEtFile(AppName),
		});
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(Args);
	}

	[Doc("Avalonia 建置器；設計器也會呼叫，故不要移除。")]
	public static AppBuilder BuildAvaloniaApp() {
		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace();
	}
}
