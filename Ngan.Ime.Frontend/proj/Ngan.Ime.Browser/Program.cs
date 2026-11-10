using Ngan.Ime;
using Ngan.Ime.Infra.Storage;
using Ngan.Ime.UI;
using Avalonia;
using Avalonia.Browser;
using Microsoft.Extensions.DependencyInjection;

namespace Ngan.Ime.Browser;

/// 瀏覽器宿主（WASM）。沒有檔案系統，故儲存區用 NoBaseDirMgr、日誌只進開發者工具 Console。
/// 目前只裝配基礎設施與界面層，尚未接 Rime 引擎。
internal sealed partial class Program
{
	/// 本宿主在日誌與配置裡的自稱。
	const str AppName = "Ngan.Ime.Browser";

	private static Task Main(string[] Args){
		var Svc = new ServiceCollection();
		Svc.SetupCore();
		Svc.AddAppLog(MkrLogger.Console(AppName));
		Svc.AddSingleton<I_GetBaseDir>(NoBaseDirMgr.Inst);
		AppCfg.Inst.Setup(NoBaseDirMgr.Inst);
		Svc.SetupCfg(AppCfg.Inst);
		Svc.SetupUi();
		Di.SvcProvider = Svc.BuildServiceProvider();

		return BuildAvaloniaApp()
			.WithInterFont()
			.StartBrowserAppAsync("out");
	}

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>();
}
