namespace Ngan.Ime.Tsf.Windows;

using System;
using System.IO;
using System.Text;
using System.Threading;

[Doc($$"""
外殼檔案日誌的共用底座：一行一次原子追加。
*為何需要跨行程互斥*：版本化安裝之後，同一個應用行程裏可能同時載入好幾支版本的
`Ngan.Ime.Tsf.dll`（2026-11-15 在 VSCode 的 WER 模組清單裏就看到四支），
每支都往同一個日誌檔寫。先前的寫法有兩個問題：
+ 逐鍵日誌用 `File.AppendAllText`（`FileShare.Read`），兩個 writer 同時開檔就互斥失敗，整行丟掉；
+ 一般日誌各自長期持有 `StreamWriter`，各自記住自己的檔內位置，寫久了會互相覆蓋。
兩者都會讓日誌「缺行」，而排查時「日誌裏沒有」被讀成「事件沒發生」——這次就是這樣被誤導的。

*做法*：每一行都在一個跨行程具名互斥內「開檔（Append）→ 寫 → 關檔」。
互斥保證「開檔到寫完」之間沒有第二個 writer，所以每行完整、順序即時序。
代價是每行一次開關檔；只有逐鍵日誌會踩到這個成本，可以接受。
""")]
internal static unsafe partial class NativeTsf {

	[Doc($$"""
	取得日誌互斥的最長等待（毫秒）；日誌不該把按鍵卡住，所以寧可漏原子性也不久等。
	""")]
	private const i32 FileGateTimeoutMs = 250;

	[Doc($$"""
	把一行追加到檔案；超過上限就先輪替。任何失敗都吞掉——日誌不能影響輸入。
	#Prm[{{nameof(FilePath)}}][目標檔的完整路徑。]
	#Prm[{{nameof(Line)}}][要寫的內容（不含換行）。]
	#Prm[{{nameof(MaxBytes)}}][單檔大小上限；小於等於 0 表示不輪替。]
	#Prm[{{nameof(Keep)}}][保留幾份舊檔（`.1`～`.N`）。]
	""")]
	private static void AppendLineAtomic(str FilePath, str Line, i64 MaxBytes, i32 Keep) {
		var gate = AcquireFileGate(FilePath);
		try {
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			if (MaxBytes > 0) {
				var info = new FileInfo(FilePath);
				if (info.Exists && info.Length > MaxBytes) {
					RotateFile(FilePath, Keep);
				}
			}
			// FileShare.ReadWrite：就算有人沒走這道門（舊版 DLL、使用者開著檔），也不會互斥失敗。
			using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
			var bytes = Encoding.UTF8.GetBytes(Line + Environment.NewLine);
			stream.Write(bytes, 0, bytes.Length);
			stream.Flush();
		} catch {
			// 日誌失敗不該影響輸入。
		} finally {
			ReleaseFileGate(gate);
		}
	}

	[Doc($$"""
	取得某個日誌檔的跨行程互斥；拿不到就回 null（照樣寫，只是少了原子性）。
	#Prm[{{nameof(FilePath)}}][日誌檔路徑。]
	#Rtn[已持有的互斥；null 表示沒有拿到。]
	""")]
	private static Mutex? AcquireFileGate(str FilePath) {
		Mutex? Gate = null;
		try {
			Gate = new Mutex(false, FileGateName(FilePath));
			try {
				if (!Gate.WaitOne(FileGateTimeoutMs)) {
					ReleaseFileGate(Gate);
					return null;
				}
			} catch (AbandonedMutexException) {
				// 上一個持有者在寫入途中死了；互斥現在歸我們（.NET 的語義），照樣往下寫。
			}
			return Gate;
		} catch {
			ReleaseFileGate(Gate);
			return null;
		}
	}

	[Doc($$"""
	釋放並丟掉互斥。
	#Prm[{{nameof(Gate)}}][先前取得的互斥；可為 null。]
	""")]
	private static void ReleaseFileGate(Mutex? Gate) {
		if (Gate is null) {
			return;
		}
		try {
			Gate.ReleaseMutex();
		} catch {
			// 不是持有者或已被丟棄：沒有東西要還。
		}
		try {
			Gate.Dispose();
		} catch {
			// 同上。
		}
	}

	[Doc($$"""
	由日誌檔路徑算出互斥名稱；用 FNV-1a 32 位元，名稱短且合法。
	#Prm[{{nameof(FilePath)}}][日誌檔路徑。]
	#Rtn[具名互斥的名稱。]
	""")]
	private static str FileGateName(str FilePath) {
		unchecked {
			var hash = 2166136261u;
			foreach (var ch in FilePath) {
				hash = (hash ^ ch) * 16777619u;
			}
			return "Local\\NganImeTsf.Log." + hash.ToString("X8");
		}
	}

	[Doc($$"""
	把現行檔改名成 `.1`，其餘往後推，最後只保留 `Keep` 份舊檔。
	#Prm[{{nameof(FilePath)}}][現行日誌檔。]
	#Prm[{{nameof(Keep)}}][保留的舊檔數量。]
	""")]
	private static void RotateFile(str FilePath, i32 Keep) {
		try {
			var oldest = FilePath + "." + Keep;
			if (File.Exists(oldest)) {
				File.Delete(oldest);
			}
			for (var index = Keep - 1; index >= 1; index--) {
				var from = FilePath + "." + index;
				if (!File.Exists(from)) {
					continue;
				}
				File.Move(from, FilePath + "." + (index + 1));
			}
			if (File.Exists(FilePath)) {
				File.Move(FilePath, FilePath + ".1");
			}
		} catch {
			// 輪替失敗就繼續寫同一個檔，總比沒有日誌好。
		}
	}
}
