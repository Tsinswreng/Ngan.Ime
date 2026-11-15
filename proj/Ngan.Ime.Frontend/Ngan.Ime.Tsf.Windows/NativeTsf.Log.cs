namespace Ngan.Ime.Tsf.Windows;

using System;
using System.IO;
using System.Text;

/// <summary>
/// 外殼（行程內 DLL）的診斷日誌。
/// </summary>
/// <remarks>
/// *分級*：Error／Warn 永遠寫檔——外殼跑在別人的行程裏，出事時沒有人會替它留證據，
/// 若連錯誤都要靠環境變數才記，等於把問題變成黑盒。Trace（逐鍵、QI、AddRef 之類）
/// 才由 `NGAN_TSF_LOG=1` 開啟，避免每顆鍵都做檔案 IO 拖慢桌面。
///
/// *為何不用 AppLog*：本 DLL 不帶 CLR 與日誌庫，AOT 下也不能反射載入；
/// 檔案是唯一能留下的證據，故自己寫一個最小實現，用法刻意與 AppLog 對齊（LogError／LogWarn）。
///
/// *輪替*：單檔 1MB，另外保留三份舊檔（`.1`～`.3`）；外殼可能長時間掛在 explorer 裏，
/// 不輪替會無限長大。檔案在 `%LOCALAPPDATA%\NganImeTsf\tsf-shell.log`，與服務端日誌分開。
///
/// *為何要防重入*：日誌會被向量化例外處理器呼叫，而寫檔本身又可能失敗或再觸發例外。
/// 沒有防重入就會無限遞迴，最後以堆疊溢位收場——在 explorer 裏面。
/// </remarks>
internal static unsafe partial class NativeTsf {

	/// <summary>單一檔案大小上限（位元組）。</summary>
	private const long MaxLogBytes = 1L << 20;

	/// <summary>保留的舊檔數量（不含現行檔）。</summary>
	private const int KeepLogFiles = 3;

	/// <summary>逐鍵與 COM 細節是否輸出；由環境變數 `NGAN_TSF_LOG=1` 開啟。</summary>
	private static readonly bool VerboseLog =
		Environment.GetEnvironmentVariable("NGAN_TSF_LOG") == "1";

	/// <summary>日誌目錄；首次寫入時才建立。</summary>
	private static readonly string LogDir =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NganImeTsf");

	/// <summary>現行日誌檔。</summary>
	private static readonly string LogPath = Path.Combine(LogDir, "tsf-shell.log");

	/// <summary>寫入互斥；UI 執行緒與 TSF 回調都可能寫。</summary>
	private static readonly object LogGate = new();

	/// <summary>本執行緒是否正在寫入；同一個執行緒重入就直接放棄（見類註釋）。</summary>
	[ThreadStatic]
	private static bool InWrite;

	/// <summary>常駐 writer；null 表示尚未開啟或剛關閉等待輪替。</summary>
	private static StreamWriter? LogWriter;

	/// <summary>目前檔案已寫入的位元組數，用來判斷何時輪替。</summary>
	private static long LogBytes;

	/// <summary>逐鍵、狀態變化之類的細節；預設不寫。</summary>
	private static void Log(string Message) {
		if (!VerboseLog) {
			return;
		}
		Write("T", Message);
	}

	/// <summary>警告：不致命，但代表某條路徑沒有按預期工作。</summary>
	private static void LogWarn(string Message) {
		Write("W", Message);
	}

	/// <summary>錯誤：例外、COM 呼叫失敗、狀態不一致；永遠寫。</summary>
	private static void LogError(string Message) {
		Write("E", Message);
	}

	/// <summary>寫一行；任何失敗都吞掉，日誌不能影響輸入。</summary>
	private static void Write(string Level, string Message) {
		// 重入保護要在 try 之前：finally 裏的 InWrite=false 只配對一次。
		if (InWrite) {
			return;
		}
		InWrite = true;
		try {
			lock (LogGate) {
				EnsureWriter();
				var Line = DateTime.Now.ToString("HH:mm:ss.fff")
					+ " [" + Environment.ProcessId + "][" + Level + "] "
					+ Message;
				LogWriter!.WriteLine(Line);
				// 粗估位元組數即可；中文字會被低估，不影響輪替的正確性。
				LogBytes += Line.Length + Environment.NewLine.Length;
			}
		}
		catch {
			// 日誌失敗不該影響輸入。
		}
		finally {
			InWrite = false;
		}
	}

	/// <summary>取得可寫的 writer；必要時開檔或先輪替。</summary>
	private static void EnsureWriter() {
		if (LogWriter is not null && LogBytes < MaxLogBytes) {
			return;
		}
		if (LogWriter is not null) {
			LogWriter.Dispose();
			LogWriter = null;
			Rotate();
		}
		Directory.CreateDirectory(LogDir);
		var Stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
		LogWriter = new StreamWriter(Stream, new UTF8Encoding(false)) { AutoFlush = true };
		LogBytes = Stream.Length;
	}

	/// <summary>
	/// 把現行檔改名成 `.1`，其餘往後推，最後只保留 <see cref="KeepLogFiles"/> 份舊檔。
	/// </summary>
	/// <remarks>
	/// 迴圈從「最舊的那一份」開始搬：先把 `.3` 刪掉、`.2`→`.3`、`.1`→`.2`、現行檔→`.1`。
	/// 先前寫成從 `.1` 往前搬，結果每輪都多長一份（現行檔 + `.1`～`.4`），
	/// 與註釋說的「保留三份」對不上。
	/// </remarks>
	private static void Rotate() {
		try {
			var Oldest = LogPath + "." + KeepLogFiles;
			if (File.Exists(Oldest)) {
				File.Delete(Oldest);
			}
			for (var Index = KeepLogFiles - 1; Index >= 1; Index--) {
				var From = LogPath + "." + Index;
				if (!File.Exists(From)) {
					continue;
				}
				File.Move(From, LogPath + "." + (Index + 1));
			}
			if (File.Exists(LogPath)) {
				File.Move(LogPath, LogPath + ".1");
			}
		}
		catch {
			// 輪替失敗就繼續寫同一個檔，總比沒有日誌好。
		}
	}
}
