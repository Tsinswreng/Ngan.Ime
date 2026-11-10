namespace Ngan.Ime.Infra.Log;

using Microsoft.Extensions.Logging;
using Tsinswreng.CsCore;

[Doc($$"""
內層日誌器的製造器：把「這一端要寫到哪裡」集中在一處。

*為何要有這個類*：`LoggerFactory.Create` 每次呼叫都會造一整套 provider，
各入口各寫一遍就是各漏一個；集中之後要加 sink（檔案、結構化、遠端）也只改這裡。

*兩種口味*：
- {nameof(Console)}：只有主控台。Linux 有終端、Browser 的 stdout 被導向開發者工具 Console。
- {nameof(ConsoleEtFile)}：主控台 ＋ 應用儲存區下的檔案。桌面雙擊啟動（WinExe）沒有終端，
  只看主控台等於什麼都看不到，故桌面端用這個。

Android 走 logcat（見 `AndroidLogger`），因為它本身就是行動端的「控制台」。
""")]
public static class MkrLogger {
	[Doc("進程內共用的主控台工廠；進程結束即隨之回收，故不提供 Dispose。")]
	static readonly Lazy<ILoggerFactory> ConsoleFactory = new(MkConsoleFactory);

	[Doc("序列化「主控台＋檔案」工廠的首次建立，避免兩個入口同時初始化時造出兩份。")]
	static readonly Lock InitLock = new();

	[Doc("主控台＋檔案的工廠；null 表示還沒建立。")]
	static ILoggerFactory? ConsoleEtFileFactory;

	[Doc("建立主控台工廠：Linux 寫 stdout，Browser 寫開發者工具的 Console（WASM 下 stdout 被導向那裡）。")]
	static ILoggerFactory MkConsoleFactory() {
		return LoggerFactory.Create(B => B.AddConsole());
	}

	[Doc($$"""
	取一個只寫主控台的日誌器。
	#Prm[{{nameof(Category)}}][分類名。宿主通常傳應用名稱，故同一應用的日誌行有共同前綴。]
	#Rtn[可直接當 AppLog.Inst 內層實現的日誌器。]
	""")]
	public static ILogger Console(str Category) {
		return ConsoleFactory.Value.CreateLogger(Category);
	}

	[Doc($$"""
	取一個「主控台 ＋ 檔案」雙寫的日誌器。日誌行同時進 stdout 與應用儲存區下的 `logs/<分類名>.log`。
	#Prm[{{nameof(Category)}}][分類名；同時決定檔名（`<分類名>.log`）。]
	#Rtn[可直接當 AppLog.Inst 內層實現的日誌器。]
	#Note[檔名由第一次呼叫決定：一個行程只有一個入口，故不會出現兩個檔名不一致的情況。]
	""")]
	public static ILogger ConsoleEtFile(str Category) {
		// 1: 只建一次（每個行程一個工廠），並用第一次的分類名當檔名。
		lock (InitLock) {
			ConsoleEtFileFactory ??= LoggerFactory.Create(B => {
				B.AddConsole();
				B.AddProvider(new FileLoggerProvider(Category + ".log"));
			});
		}
		// 2: 分類名由工廠再分派；檔案 provider 內部共用同一個 FileLogger。
		return ConsoleEtFileFactory.CreateLogger(Category);
	}
}
