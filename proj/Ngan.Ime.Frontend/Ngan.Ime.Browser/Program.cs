using Ngan.Ime;
using Ngan.Ime.Infra.Storage;
using Ngan.Ime.UI;
using Avalonia;
using Avalonia.Browser;

namespace Ngan.Ime.Browser;

[Doc("""
瀏覽器宿主（WASM）。沒有檔案系統，故儲存區用 NoBaseDirMgr、日誌只進開發者工具 Console；
沒有引擎，故組合根會補 NoopSvcIme（界面可預覽，不能真的輸入）。
""")]
internal sealed partial class Program
{
	[Doc("本宿主在日誌與配置裡的自稱。")]
	const str AppName = "Ngan.Ime.Browser";

	[Doc("裝配容器並啟動瀏覽器應用。")]
	private static Task Main(str[] Args){
		AppComposition.MkServices(new HostBootstrap{
			AppName = AppName,
			BaseDir = NoBaseDirMgr.Inst,
			Logger = MkrLogger.Console(AppName),
		});
		return BuildAvaloniaApp()
			.WithInterFont()
			.StartBrowserAppAsync("out");
	}

	[Doc("Avalonia 建置器。")]
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>();
}
