using Foundation;
using UIKit;
using Ngan.Ime;
using Ngan.Ime.Infra.Storage;
using Ngan.Ime.UI;
using Avalonia;
using Avalonia.iOS;
using Tsinswreng.CsCore;

namespace Ngan.Ime.iOS;

[Doc("""
iOS 宿主。沒有檔案系統與引擎，故用 NoBaseDirMgr 與 NoopSvcIme（界面可預覽，不能輸入）。
""")]
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
	[Doc("本宿主在日誌與配置裡的自稱。")]
	const str AppName = "Ngan.Ime.iOS";

	[Doc("裝配容器並回傳 Avalonia 建置器。")]
	protected override AppBuilder CustomizeAppBuilder(AppBuilder Builder)
	{
		AppComposition.MkServices(new HostBootstrap{
			AppName = AppName,
			BaseDir = NoBaseDirMgr.Inst,
			Logger = MkrLogger.Console(AppName),
		});
		return base.CustomizeAppBuilder(Builder).WithInterFont();
	}
}
