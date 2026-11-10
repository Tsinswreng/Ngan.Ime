using Foundation;
using UIKit;
using Ngan.Ime;
using Ngan.Ime.Infra.Storage;
using Ngan.Ime.UI;
using Avalonia;
using Avalonia.iOS;
using Microsoft.Extensions.DependencyInjection;

namespace Ngan.Ime.iOS;

/// iOS 宿主。目前只裝配基礎設施與界面層，尚未接 Rime 引擎；
/// 沒有檔案系統抽象之外的儲存區需求，故儲存區用 NoBaseDirMgr。
[Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
	/// 本宿主在日誌與配置裡的自稱。
	const str AppName = "Ngan.Ime.iOS";

	protected override AppBuilder CustomizeAppBuilder(AppBuilder Builder)
	{
		var Svc = new ServiceCollection();
		Svc.SetupCore();
		Svc.AddAppLog(MkrLogger.Console(AppName));
		Svc.AddSingleton<I_GetBaseDir>(NoBaseDirMgr.Inst);
		AppCfg.Inst.Setup(NoBaseDirMgr.Inst);
		Svc.SetupCfg(AppCfg.Inst);
		Svc.SetupUi();
		Di.SvcProvider = Svc.BuildServiceProvider();

		return base.CustomizeAppBuilder(Builder)
			.WithInterFont();
	}
}
