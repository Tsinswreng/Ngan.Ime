namespace Ngan.Ime.Infra.Log;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tsinswreng.CsCore;

[Doc($$"""
日誌相關的依賴注入裝配。

四個宿主原本各自貼同一段「設 {nameof(AppLog)}.Inst 的內層實現 ＋ AddSingleton ＋ AddLogging」，
改一處漏三處；收成 {nameof(AddAppLog)} 之後，入口只留一行，宿主之間只剩「傳哪個內層實現」這一處差異。
""")]
public static class DiLog {
	extension(IServiceCollection z) {
		[Doc($$"""
		把 {nameof(AppLog)} 接到容器，並指定本宿主的日誌去向。

		做完之後三種寫法都落到同一個內層實現：注入日誌介面、注入泛型日誌介面、直接調 {nameof(AppLog)}.Inst。

		#Prm[{{nameof(InnerLogger)}}][本宿主的日誌實現：桌面「主控台＋檔案」、Android logcat。]
		#Rtn[同一個 {nameof(IServiceCollection)}，便於串接。]
		#Eg[
			```cs
			Svc.AddAppLog(MkrLogger.ConsoleEtFile("Ngan.Ime.Windows"));
			Svc.AddAppLog(new AndroidLogger("Ngan.Ime"));
			```
		]
		""")]
		public IServiceCollection AddAppLog(ILogger InnerLogger) {
			// 1: 全局門面先指向本宿主的實現——這一步必須在建任何視圖之前完成，
			//    否則建視圖期間的日誌會落到空的內層實現上而丟失。
			AppLog.Inst.InnerLogger = InnerLogger;

			// 2: 讓注入取得的是同一個門面，而不是另一份實現。
			z.AddSingleton<ILogger>(_ => AppLog.Inst);

			// 3: 泛型日誌介面由 Logging 框架的分類機制產生，
			//    故換掉它的 provider 才能接到門面上；先清空默認 provider，避免多出第二條出口。
			z.AddLogging(B => {
				B.ClearProviders();
				B.AddProvider(AppLogLoggerProvider.Inst);
			});
			return z;
		}
	}
}
