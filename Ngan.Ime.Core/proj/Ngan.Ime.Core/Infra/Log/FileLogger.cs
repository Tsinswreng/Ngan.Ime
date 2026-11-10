namespace Ngan.Ime.Infra.Log;

using System.Text;
using Microsoft.Extensions.Logging;
using Tsinswreng.CsCore;
using Ngan.Ime.Infra.Storage;

[Doc($$"""
把日誌追加寫進「應用儲存區／logs/」下某個檔案的 {nameof(ILogger)}。

*為何桌面端要有檔案 sink*：桌面入口是 WinExe，雙擊啟動時沒有終端，主控台輸出無處可去；
啟動失敗與背景錯誤因此無跡可尋。故桌面端是「主控台 ＋ 檔案」雙寫（見 {nameof(MkrLogger.ConsoleEtFile)}）。

*路徑從哪來*：經 {nameof(Di)} 取 {nameof(I_GetBaseDir)}，故本類不假設自己在哪個安裝位置；
解析延遲到第一次寫入（那時 `Di.SvcProvider` 已賦值）。

*失敗策略*：寫檔失敗時吞掉（記錄日誌不該讓應用崩潰），主控台那一路仍然正常輸出。
""")]
public sealed class FileLogger : ILogger {
	[Doc("日誌檔在應用儲存區下的子目錄名。")]
	public const str DfltSubDir = "logs";

	[Doc("日誌檔名，例如 `Ngan.Ime.Windows.log`。")]
	public str FileName { get; set; } = "app.log";

	[Doc("應用儲存區下的子目錄。")]
	public str SubDir { get; set; } = DfltSubDir;

	[Doc("序列化追加寫入：日誌可能來自 UI、Rime 回調、網絡回調多條線程，不加鎖會寫壞同一行。")]
	readonly Lock WriteLock = new();

	[Doc("解析後的路徑快取；null 表示還沒解析過。")]
	str? _Path;

	[Doc($$"""
	解析日誌檔路徑並確保目錄存在。只做一次，之後用快取。
	#Rtn[日誌檔的完整路徑。]
	""")]
	str GetPath() {
		if (_Path is not null) {
			return _Path;
		}
		// 1: 根目錄由容器提供（見 AppDirBaseDirMgr／ExternalFilesBaseDirMgr）。
		var Dir = Di.GetRSvc<I_GetBaseDir>().Combine(SubDir);
		// 2: 首次寫入前建目錄；之後不再檢查。
		Directory.CreateDirectory(Dir);
		return _Path = Path.Combine(Dir, FileName);
	}

	[Doc("本機診斷保留所有非 None 等級，避免首次失敗只留下缺少上下文的錯誤行。")]
	public bool IsEnabled(LogLevel LogLevel) {
		return LogLevel != LogLevel.None;
	}

	[Doc("本日誌器不保存 scope；檔案格式裡沒有對應欄位。")]
	public IDisposable BeginScope<TState>(TState State) where TState : notnull {
		return NoOpScope.Inst;
	}

	[Doc($$"""
	把格式化訊息與完整例外堆棧追加為一個原子日誌項。
	#Prm[{{nameof(LogLevel)}}][等級。]
	#Prm[{{nameof(EventId)}}][事件識別，檔案格式目前忽略。]
	#Prm[{{nameof(State)}}][訊息狀態，交給官方格式化委託。]
	#Prm[{{nameof(Exception)}}][可選例外，其堆棧另起一段。]
	#Prm[{{nameof(Formatter)}}][把狀態與例外組成文字的官方委託。]
	""")]
	public void Log<TState>(
		LogLevel LogLevel
		, EventId EventId
		, TState State
		, Exception? Exception
		, Func<TState, Exception?, str> Formatter
	) {
		if (!IsEnabled(LogLevel)) {
			return;
		}
		try {
			// 1: 「時間 等級 訊息」一行；例外另起，方便直接看堆棧。
			var Entry = new StringBuilder()
				.Append(DateTimeOffset.Now.ToString("O"))
				.Append(' ')
				.Append(LogLevel)
				.Append(' ')
				.Append(Formatter(State, Exception));
			if (Exception is not null) {
				Entry.AppendLine().Append(Exception);
			}
			Entry.AppendLine();
			// 2: 整條在鎖內一次寫完，避免與其他線程的日誌交錯。
			lock (WriteLock) {
				File.AppendAllText(GetPath(), Entry.ToString(), Encoding.UTF8);
			}
		} catch {
			// 寫檔失敗時保持應用可用；主控台那一路仍會輸出。
		}
	}

	[Doc("無狀態 scope 的單例，滿足 ILogger 契約而不為每條日誌多做一次配置。")]
	sealed class NoOpScope : IDisposable {
		[Doc("唯一實例。")]
		public static NoOpScope Inst { get; } = new();

		[Doc("沒有資源需要回收。")]
		public void Dispose() { }
	}
}

[Doc($$"""
{nameof(FileLogger)} 的 provider：讓它能在 `LoggerFactory` 的 provider 機制裡與 `AddConsole()` 並列，
從而一條日誌同時進主控台與檔案。
""")]
public sealed class FileLoggerProvider : ILoggerProvider {
	[Doc("本 provider 製造的唯一檔案日誌器；分類名不參與檔案格式，故多個分類共用同一份。")]
	readonly FileLogger Logger;

	[Doc($$"""
	建立 provider。
	#Prm[{{nameof(FileName)}}][日誌檔名。]
	""")]
	public FileLoggerProvider(str FileName) {
		Logger = new FileLogger { FileName = FileName };
	}

	[Doc("回傳共用的檔案日誌器（忽略分類名，避免同一行程寫出多個檔案）。")]
	public ILogger CreateLogger(str CategoryName) {
		return Logger;
	}

	[Doc("沒有可釋放資源；檔案是每次寫入時開關的。")]
	public void Dispose() { }
}
