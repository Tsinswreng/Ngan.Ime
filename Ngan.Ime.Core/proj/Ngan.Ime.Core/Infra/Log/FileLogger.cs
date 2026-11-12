namespace Ngan.Ime.Infra.Log;

using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Tsinswreng.CsCore;
using Ngan.Ime.Infra.Storage;

[Doc($$"""
把日誌追加寫進「應用儲存區／logs/」下某個檔案的 {{nameof(ILogger)}}。

*為何桌面端要有檔案 sink*：桌面入口是 WinExe，雙擊啟動時沒有終端，主控台輸出無處可去；
啟動失敗與背景錯誤因此無跡可尋。故桌面端是「主控台 ＋ 檔案」雙寫（見 {{nameof(MkrLogger.ConsoleEtFile)}}）。

*為何要背景寫入*：舊版每條日誌 `File.AppendAllText`（開檔→寫→關檔），
而輸入法熱路徑一次按鍵可產生十幾條日誌，於是每顆鍵都付出十幾次檔案開關的成本。
這裡改成單一背景寫入者持有一個 {{nameof(StreamWriter)}}，呼叫方只做一次入隊。

*佇列滿了就丟最舊的*：日誌不能反過來阻塞輸入。丟棄會計數並在下次成功寫入時記一條 Warn。

*路徑從哪來*：經 {{nameof(Di)}} 取 {{nameof(I_GetBaseDir)}}，故本類不假設自己在哪個安裝位置；
解析延遲到第一次寫入（那時 `Di.SvcProvider` 已賦值）。

*失敗策略*：寫檔失敗時吞掉並停用本 sink（記錄日誌不該讓應用崩潰），主控台那一路仍然正常輸出。
""")]
public sealed class FileLogger : ILogger, IDisposable {

	[Doc("日誌檔在應用儲存區下的子目錄名。")]
	public const str DfltSubDir = "logs";

	[Doc("佇列容量；滿了丟最舊的一條。")]
	public const i32 QueueCapacity = 4096;

	[Doc("日誌檔名，例如 `Ngan.Ime.Windows.log`。")]
	public str FileName { get; set; } = "app.log";

	[Doc("應用儲存區下的子目錄。")]
	public str SubDir { get; set; } = DfltSubDir;

	[Doc("本 sink 的最低等級；低於它的日誌連格式化都不做。")]
	public LogLevel MinLevel { get; set; } = LogLevel.Trace;

	[Doc("待寫入的已格式化條目。")]
	readonly Channel<str> Queue = Channel.CreateBounded<str>(new BoundedChannelOptions(QueueCapacity){
		SingleReader = true,
		SingleWriter = false,
		FullMode = BoundedChannelFullMode.DropOldest,
	});

	[Doc("啟動背景寫入者的鎖；多條線程首次寫日誌時只啟動一次。")]
	readonly Lock StartLock = new();

	[Doc("背景寫入任務；Dispose 時等待它把佇列寫完。")]
	Task? WriterTask;

	[Doc("解析後的路徑快取；null 表示還沒解析過。")]
	str? _Path;

	[Doc("檔案寫入器；只在背景寫入者裡使用。")]
	StreamWriter? Writer;

	[Doc("被丟棄的日誌條數；下次成功寫入時記一條 Warn 後歸零。")]
	i64 DroppedCount;

	[Doc("本 sink 是否已停用（寫檔失敗或已釋放）。")]
	bool Disabled;

	[Doc("解析日誌檔路徑並確保目錄存在。只做一次，之後用快取。")]
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

	[Doc("本 sink 接受所有非 None 等級；真正的門檻由 MinLevel 與外層 AppLog 決定。")]
	public bool IsEnabled(LogLevel LogLevel) {
		return !Disabled && LogLevel != Microsoft.Extensions.Logging.LogLevel.None && LogLevel >= MinLevel;
	}

	[Doc("本日誌器不保存 scope；檔案格式裡沒有對應欄位。")]
	public IDisposable BeginScope<TState>(TState State) where TState : notnull {
		return NoOpScope.Inst;
	}

	[Doc($$"""
	把格式化訊息與完整例外堆棧組成一個原子日誌項並入隊；實際寫檔在背景完成。
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
		// 2: 入隊即返回；寫檔由唯一的背景寫入者串行完成。
		if (Queue.Reader.Count >= QueueCapacity) {
			Interlocked.Increment(ref DroppedCount);
		}
		Queue.Writer.TryWrite(Entry.ToString());
		Start();
	}

	[Doc("確保背景寫入者已啟動。")]
	void Start() {
		lock (StartLock) {
			WriterTask ??= Task.Run(WriteLoopAsy);
		}
	}

	[Doc("背景寫入迴圈：逐條寫入同一個 StreamWriter；失敗即停用本 sink 並保留主控台輸出。")]
	async Task WriteLoopAsy() {
		try {
			await foreach (var Entry in Queue.Reader.ReadAllAsync()) {
				try {
					Writer ??= MkWriter();
					if (Writer is null) {
						return;
					}
					var Dropped = Interlocked.Exchange(ref DroppedCount, 0);
					if (Dropped > 0) {
						Writer.WriteLine($"{DateTimeOffset.Now:O} Warning [FileLogger] 佇列已滿，丟棄了 {Dropped} 條日誌");
					}
					Writer.Write(Entry);
				} catch (Exception Ex) {
					// 檔案不可寫時不反覆重試：停用本 sink，只留這一行給主控台。
					Disabled = true;
					AppLog.Warn("[FileLogger] 寫入日誌檔失敗，已停用檔案 sink：" + Ex.Message);
					return;
				}
			}
		} catch (Exception Ex) {
			AppLog.Warn("[FileLogger] 背景寫入者異常退出：" + Ex.Message);
		}
	}

	[Doc("建立（或重建）檔案寫入器；目錄或檔案開不起來時回 null 並停用本 sink。")]
	StreamWriter? MkWriter() {
		try {
			var Path = GetPath();
			var Stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
			// AutoFlush：每條日誌都落到 OS，程式崩潰時不丟最後幾行。批次緩衝由 StreamWriter 自己提供，
			// 與舊版「每條開關檔」相比已經少掉 open/close 的成本。
			return new StreamWriter(Stream, new UTF8Encoding(false)){ AutoFlush = true };
		} catch (Exception Ex) {
			Disabled = true;
			AppLog.Warn("[FileLogger] 無法開啟日誌檔：" + Ex.Message);
			return null;
		}
	}

	[Doc("停止接收新日誌，等待佇列寫完並關閉檔案。")]
	public void Dispose() {
		Queue.Writer.TryComplete();
		var Task = WriterTask;
		if (Task is not null) {
			try {
				Task.Wait(TimeSpan.FromSeconds(2));
			} catch (AggregateException) {
				// 背景寫入者自己已記過日誌，這裡不再重複。
			}
		}
		Writer?.Dispose();
		Writer = null;
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
{{nameof(FileLogger)}} 的 provider：讓它能在 `LoggerFactory` 的 provider 機制裡與 `AddConsole()` 並列，
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

	[Doc("關閉檔案並等待佇列寫完。")]
	public void Dispose() {
		Logger.Dispose();
	}
}
