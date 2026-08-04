namespace Ngan.Ime.Windows;

using System.Text;
using Microsoft.Extensions.Logging;

/// Windows 桌面端的本機診斷日誌器。
/// 日誌固定放在程式輸出目錄，讓不附帶終端的 Avalonia 視窗也能保留啟動、Rime 與語音失敗堆棧。
/// 不寫入配置內容、音頻資料或第三方憑據。
sealed class WindowsFileLogger : ILogger{
	/// 序列化追加寫入，防止 Rime、音頻回調和 WebSocket 背景線程交錯破壞日誌行。
	readonly Lock WriteLock = new();

	/// 診斷檔的完整位置；啟動與排錯時可直接打開此單一檔案。
	readonly str LogPath;

	/// 建立本機檔案日誌器。
	/// 父目錄必須已存在，因為 AppContext.BaseDirectory 是應用程式的輸出目錄。
	public WindowsFileLogger(str LogPath){
		this.LogPath = LogPath;
	}

	/// 本機診斷保留所有非 None 等級，避免首次設備或網絡失敗只留下缺少上下文的錯誤行。
	public bool IsEnabled(LogLevel LogLevel){
		return LogLevel != LogLevel.None;
	}

	/// 本日誌器不保存 scope；呼叫端目前也沒有依賴 scope 內的資料。
	public IDisposable BeginScope<TState>(TState State)
		where TState : notnull
	{
		return NoOpScope.Inst;
	}

	/// 將格式化訊息與完整例外堆棧追加為一個原子日誌項。
	/// 日誌失敗不得反過來使輸入法或語音會話崩潰，因此所有 IO 例外在此吞掉。
	public void Log<TState>(
		LogLevel LogLevel
		, EventId EventId
		, TState State
		, Exception? Exception
		, Func<TState, Exception?, str> Formatter
	){
		if(!IsEnabled(LogLevel)){
			return;
		}
		try{
			var Message = Formatter(State, Exception);
			var Entry = new StringBuilder()
				.Append(DateTimeOffset.Now.ToString("O"))
				.Append(' ')
				.Append(LogLevel)
				.Append(' ')
				.Append(Message);
			if(Exception is not null){
				Entry.AppendLine().Append(Exception);
			}
			Entry.AppendLine();
			lock(WriteLock){
				File.AppendAllText(LogPath, Entry.ToString(), Encoding.UTF8);
			}
		}catch{
			// 診斷輸出失敗時保持應用可用；不能用本 logger 記錄自己，避免遞迴失敗。
		}
	}

	/// 無狀態 scope 的單例，滿足 ILogger 契約而不產生每條日誌的額外配置。
	sealed class NoOpScope : IDisposable{
		public static NoOpScope Inst{get;} = new();

		/// scope 沒有資源需要回收。
		public void Dispose(){}
	}
}
