namespace Ngan.Ime.Tsf.Windows;

using System;
using System.IO;

[Doc($$"""
外殼（行程內 DLL）的診斷日誌。
*分級*：Error／Warn 永遠寫檔——外殼跑在別人的行程裏，出事時沒有人會替它留證據，
若連錯誤都要靠環境變數才記，等於把問題變成黑盒。Trace（QI、AddRef 之類）才由
`NGAN_TSF_LOG=1` 開啟，避免每一顆鍵都做多餘的檔案 IO。

*為何不用 AppLog*：本 DLL 不帶 CLR 與日誌庫，AOT 下也不能反射載入；
檔案是唯一能留下的證據，故自己寫一個最小實現，用法刻意與 AppLog 對齊（LogError／LogWarn）。

*輪替與原子性*：單檔 1MB，另外保留三份舊檔（`.1`～`.3`）。實際寫入走
{{nameof(AppendLineAtomic)}}：一行一個跨行程互斥，避免同一台機器上多支版本、
多個行程同時寫時互相蓋掉。每行帶 `[模組標籤]`，才看得出是哪一版的外殼寫的。

*為何要防重入*：日誌會被向量化例外處理器呼叫，而寫檔本身又可能失敗或再觸發例外。
沒有防重入就會無限遞迴，最後以堆疊溢位收場——在 explorer 裏面。
""")]
internal static unsafe partial class NativeTsf {

	[Doc($$"""
	單一檔案大小上限（位元組）。
	""")]
	private const i64 MaxLogBytes = 1L << 20;

	[Doc($$"""
	保留的舊檔數量（不含現行檔）。
	""")]
	private const i32 KeepLogFiles = 3;

	[Doc($$"""
	逐鍵與 COM 細節是否輸出；由環境變數 `NGAN_TSF_LOG=1` 開啟。
	""")]
	private static readonly bool VerboseLog =
		Environment.GetEnvironmentVariable("NGAN_TSF_LOG") == "1";

	[Doc($$"""
	日誌目錄；首次寫入時才建立。
	""")]
	private static readonly str LogDir =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NganImeTsf");

	[Doc($$"""
	現行日誌檔。
	""")]
	private static readonly str LogPath = Path.Combine(LogDir, "tsf-shell.log");

	[Doc($$"""
	本模組的短標籤（安裝目錄名，例如 `v20261115-225136`）。
	版本化安裝讓同一行程裏可以同時有多支外殼，日誌卻共用一個檔。
	每行帶標籤之後，「這一行是哪一版做的」不必再從模組清單反推。
	""")]
	private static str SelfTag = "?";

	[Doc($$"""
	本執行緒是否正在寫入；同一個執行緒重入就直接放棄（見類註釋）。
	""")]
	[ThreadStatic]
	private static bool InWrite;

	[Doc($$"""
	算出本模組的短標籤；取安裝目錄名，取不到就用 `?`。
	#Rtn[模組標籤。]
	""")]
	private static str ComputeSelfTag() {
		try {
			var dir = Path.GetDirectoryName(GetOwnModulePath());
			if (!str.IsNullOrEmpty(dir)) {
				var name = Path.GetFileName(dir);
				if (!str.IsNullOrEmpty(name)) {
					return name;
				}
			}
		} catch {
			// 標籤只是為了讀日誌，失敗不影響任何行為。
		}
		return "?";
	}

	[Doc($$"""
	逐鍵、狀態變化之類的細節；預設不寫。
	""")]
	private static void Log(str Message) {
		if (!VerboseLog) {
			return;
		}
		Write("T", Message);
	}

	[Doc($$"""
	警告：不致命，但代表某條路徑沒有按預期工作。
	""")]
	private static void LogWarn(str Message) {
		Write("W", Message);
	}

	[Doc($$"""
	錯誤：例外、COM 呼叫失敗、狀態不一致；永遠寫。
	""")]
	private static void LogError(str Message) {
		Write("E", Message);
	}

	[Doc($$"""
	寫一行；任何失敗都吞掉，日誌不能影響輸入。
	#Prm[{{nameof(Level)}}][等級代號（T／W／E）。]
	#Prm[{{nameof(Message)}}][訊息本體。]
	""")]
	private static void Write(str Level, str Message) {
		// 重入保護要在 try 之前：finally 裏的 InWrite=false 只配對一次。
		if (InWrite) {
			return;
		}
		InWrite = true;
		try {
			var line = DateTime.Now.ToString("HH:mm:ss.fff")
				+ " [" + Environment.ProcessId + "]"
				+ "[" + Level + "]"
				+ "[" + SelfTag + "] "
				+ Message;
			AppendLineAtomic(LogPath, line, MaxLogBytes, KeepLogFiles);
		} catch {
			// 日誌失敗不該影響輸入。
		} finally {
			InWrite = false;
		}
	}
}
