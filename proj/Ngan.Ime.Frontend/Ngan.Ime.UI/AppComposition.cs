namespace Ngan.Ime.UI;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Infra.Storage;
using Ngan.Ime.UI.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
一個宿主要提供給組合根的東西。

*為何要這個型別*：先前六個入口各自貼同一段「SetupCore → AddAppLog → 建容器」，
靠人工保持一致，於是出現「有的頭忘了 SetupRime」「重複註冊 Voice VM」這類漂移。
現在入口只填這一份差異描述，其餘交給 {{nameof(AppComposition)}}。

*為什麽用委派而不是旗標*：接不接引擎、接哪個引擎是宿主的決定，
但具體的 `SetupRime()` 在 Rime 程序集裡，而 UI 不引用 Rime；
故以 `Action<IServiceCollection>` 讓宿主自己補，組合根不必知道引擎的名字。
""")]
public sealed partial class HostBootstrap{

	[Doc("本宿主在日誌與配置裡的自稱（同時是日誌檔名）。")]
	public required str AppName{get;init;}

	[Doc("應用儲存區根目錄的實現；Browser／iOS 用 `NoBaseDirMgr`。")]
	public required I_GetBaseDir BaseDir{get;init;}

	[Doc("日誌內層實現；桌面是「主控台＋檔案」、Android 是 logcat、Browser 是 Console。")]
	public required ILogger Logger{get;init;}

	[Doc("可選：接引擎（`Svc => Svc.SetupRime()`）。null 表示本宿主沒有引擎，用空實現。")]
	public Action<ServiceCollection>? SetupEngine{get;init;}

	[Doc("可選：本宿主的其他服務（鍵盤宿主、剪貼板、語音源、操作系統按鍵處理器）。")]
	public Action<ServiceCollection>? SetupHost{get;init;}

	[Doc("可選：建立容器後的收尾（例如 TSF 宿主啟動管道伺服器）。")]
	public Action<IServiceProvider>? AfterBuild{get;init;}
}

[Doc($$"""
組合根：六個入口共用的唯一裝配流程。

*順序不能換*：
1. 日誌門面先落地（啟動失敗才有地方看）；
2. 儲存區 → 配置（配置要早於任何讀配置的東西）；
3. 日誌等級（讀完配置才知道）；
4. 引擎（可選）、界面、宿主實現；
5. 建容器、設進程唯一入口、收尾。

*骨架宿主*：沒提供 {{nameof(HostBootstrap.SetupEngine)}} 時註冊 {{nameof(NoopSvcIme)}}，
界面能起來、能互動，只是沒有引擎——「構建綠」不再等於「運行必炸」。

#Eg[
	```cs
	var Provider = AppComposition.MkServices(new HostBootstrap{
		AppName = "Ngan.Ime.Windows",
		BaseDir = AppDirBaseDirMgr.Inst,
		Logger = MkrLogger.ConsoleEtFile("Ngan.Ime.Windows"),
		SetupEngine = Svc => Svc.SetupRime(),
		SetupHost = Svc => Svc.SetupWindows(),
	});
	```
]
""")]
public static class AppComposition{

	[Doc($$"""
	建立並回傳進程唯一的服務容器。
	#Prm[{{nameof(Boot)}}][本宿主的差異描述。]
	#Rtn[建好的容器；同時已寫入 {{nameof(Di)}}。]
	#Throw[{{nameof(TypedErr)}}][容器已存在（重複呼叫）時。]
	""")]
	public static IServiceProvider MkServices(HostBootstrap Boot){
		var Svc = new ServiceCollection();

		// 1: 日誌門面與核心服務。
		Svc.SetupCore();
		Svc.AddAppLog(Boot.Logger);

		// 2: 儲存區與配置（配置必須早於任何讀配置的地方，包含日誌等級與引擎）。
		Svc.AddSingleton(Boot.BaseDir);
		AppCfg.Inst.Setup(Boot.BaseDir);
		Svc.SetupCfg(AppCfg.Inst);

		// 3: 日誌等級（配置就緒後才決定）。
		ApplyLogLevel();

		// 4: 引擎（可選）與界面。
		if(Boot.SetupEngine is null){
			Svc.AddSingleton<NoopSvcIme>();
			Svc.AddSingleton<IImeState>(Svc => Svc.GetRequiredService<NoopSvcIme>());
			Svc.AddSingleton<IImeInput>(Svc => Svc.GetRequiredService<NoopSvcIme>());
			Svc.AddSingleton<IImeOptions>(Svc => Svc.GetRequiredService<NoopSvcIme>());
		}else{
			Boot.SetupEngine(Svc);
		}
		Svc.SetupUi();
		// 骨架宿主的降級實現先補；宿主自己的註冊在後面覆蓋。
		Svc.SetupUiFallbackHosts();

		// 5: 宿主差異。
		Boot.SetupHost?.Invoke(Svc);

		var Provider = Svc.BuildServiceProvider();
		// 進程唯一：重複建立容器直接拋（見 Di.SetSvcProvider 的理由）。
		Di.SetSvcProvider(Provider);
		Boot.AfterBuild?.Invoke(Provider);
		return Provider;
	}

	[Doc("把配置的 `LogMinLevel` 套到日誌門面；值域 0..6 對應 BCL 的 LogLevel。")]
	static void ApplyLogLevel(){
		var Level = AppCfg.Inst.Get(KeysAppCfg.LogMinLevel);
		AppLog.Inst.MinLevel = Level switch {
			<= 0 => LogLevel.Trace,
			1 => LogLevel.Debug,
			2 => LogLevel.Information,
			3 => LogLevel.Warning,
			4 => LogLevel.Error,
			5 => LogLevel.Critical,
			_ => LogLevel.None,
		};
	}
}
