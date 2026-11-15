namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Ngan.Ime.Tsf.Ipc;

/// <summary>
/// 按鍵層的診斷日誌：每一顆鍵都留一行，任何時候都能回答「TSF 給了我們什麼、我們回了什麼、引擎說了什麼」。
/// </summary>
/// <remarks>
/// *為何全記*：先前挑著記（只記修飾鍵、方向鍵、K），結果使用者按的是 Alt+J 與 Alt+;，
/// 日誌裏什麼都沒有，只好再請他重跑一次。現在全記，用輪替控制大小。
///
/// *為何每行都要帶耗時*：這段程式跑在應用行程的 UI 執行緒上，一顆鍵的成本就是應用被卡住的時間。
/// `ms=` 是「進入 HandleKeyEvent 到寫完這行」的毫秒數；管道往返與編輯會話都在裏面。
///
/// *欄位*：`vk`／`scan`／`ext`（掃描碼與擴展位）、`mods`（我們算給引擎的修飾鍵位）、
/// `phys=`（`GetAsyncKeyState` 採樣，**事件處理完之後**才讀，只作參考）、`eat=`（回給 TSF 的結論）、
/// `reply=`（引擎的 eaten／commit／preedit）、`ms=`（本顆鍵耗時）、`comp=`（當時是否持有組字）、
/// `q=`（組字步驟佇列長度，非 0 表示會話還沒跑完）。
///
/// *檔案*：`%LOCALAPPDATA%\NganImeTsf\tsf-keys.log`，8MB 之後改名 `.1`（只留一份舊的）。
/// 寫入走 <see cref="AppendLineAtomic"/>，多版本、多行程同時寫也不會丟行。
/// </remarks>
internal static partial class NativeTsf {

	/// <summary>診斷輸出檔名；與一般日誌同目錄，每行以 `[K]` 開頭便於 grep。</summary>
	private const string DiagLogFileName = "tsf-keys.log";

	/// <summary>診斷檔的大小上限；超過就輪替成 `.1`。</summary>
	private const long DiagMaxBytes = 8L * 1024 * 1024;

	/// <summary>保留的舊診斷檔數量。</summary>
	private const int DiagKeepFiles = 1;

	/// <summary>
	/// 把一顆按鍵事件的完整觀察寫一行。
	/// </summary>
	/// <param name="State">文字服務狀態；用來取組字與佇列的即時狀況。</param>
	/// <param name="Vk">虛擬鍵碼。</param>
	/// <param name="Mods">本殼算出的修飾鍵位。</param>
	/// <param name="IsUp">true 表示擡起。</param>
	/// <param name="IsTest">true 表示來自 OnTestKeyXxx。</param>
	/// <param name="LParam">TSF 帶進來的 lParam。</param>
	/// <param name="Eaten">回報給 TSF 的結論。</param>
	/// <param name="Reply">引擎回應；null 表示這一顆鍵沒有問引擎。</param>
	/// <param name="StartedTs">進入 HandleKeyEvent 時的 <see cref="Stopwatch"/> 時間戳。</param>
	private static void DiagKey(TsfState State, int Vk, int Mods, bool IsUp, bool IsTest, IntPtr LParam, int Eaten, TsfReply? Reply, long StartedTs) {
		try {
			var Bits = LParam.ToInt64();
			// 第 24 位在 lParam 裏是「擴展鍵」，不是「注入」。注入標記只在 KBDLLHOOKSTRUCT.flags 裏，
			// TSF 不會傳給我們；這個欄位先前被當成「是否注入」讀，是錯的，現在照實標成 ext。
			var Ext = (Bits >> 24) & 1;
			var Scan = (Bits >> 16) & 0xFF;
			var ElapsedMs = Stopwatch.GetElapsedTime(StartedTs).TotalMilliseconds;
			int QueueLen;
			lock (State.CompositionJobs) {
				QueueLen = State.CompositionJobs.Count;
			}
			var LAlt = (GetAsyncKeyState(0xA4) & 0x8000) != 0;
			var RAlt = (GetAsyncKeyState(0xA5) & 0x8000) != 0;
			var LShift = (GetAsyncKeyState(0xA0) & 0x8000) != 0;
			var RShift = (GetAsyncKeyState(0xA1) & 0x8000) != 0;
			var LCtrl = (GetAsyncKeyState(0xA2) & 0x8000) != 0;
			var RCtrl = (GetAsyncKeyState(0xA3) & 0x8000) != 0;
			var Line = "[K] " + (IsUp ? "up" : "dn")
				+ (IsTest ? "/test" : "/key")
				+ " vk=0x" + Vk.ToString("X2") + "(" + Vk + ")"
				+ " scan=0x" + Scan.ToString("X2") + " ext=" + Ext
				+ " synth=" + (IsSynthesizedInput() ? 1 : 0)
				+ " tid=" + Environment.CurrentManagedThreadId
				+ " mods=" + Mods
				+ " phys=LAlt" + (LAlt ? 1 : 0) + " RAlt" + (RAlt ? 1 : 0)
				+ " LSh" + (LShift ? 1 : 0) + " RSh" + (RShift ? 1 : 0)
				+ " LCt" + (LCtrl ? 1 : 0) + " RCt" + (RCtrl ? 1 : 0)
				+ " eat=" + Eaten
				+ " ms=" + ElapsedMs.ToString("0.00")
				+ " comp=" + (Volatile.Read(ref State.Composition) != IntPtr.Zero ? 1 : 0)
				+ " q=" + QueueLen
				+ " reply=" + (Reply is null
					? "none"
					: "eaten" + (Reply.Eaten ? 1 : 0) + " commit" + Reply.Commits.Count + " text=[" + JoinCommits(Reply) + "] pre" + (Reply.Preedit?.Length ?? 0) + " err=" + Reply.Err);
			AppendDiag(Line);
		}
		catch (Exception Exception) {
			LogError("DiagKey EX " + Exception);
		}
	}

	/// <summary>把引擎這次要上屏的文字串成一個字串，供日誌比對。</summary>
	/// <param name="Reply">引擎回應。</param>
	/// <returns>上屏文字；多段以 `+` 連接，過長截斷。</returns>
	private static string JoinCommits(TsfReply Reply) {
		var Builder = new System.Text.StringBuilder();
		foreach (var One in Reply.Commits) {
			if (Builder.Length > 0) {
				Builder.Append('+');
			}
			Builder.Append(One);
		}
		var Text = Builder.ToString();
		return Text.Length <= 24 ? Text : Text.Substring(0, 24) + "…";
	}

	/// <summary>把一行追加到診斷檔；任何失敗都不影響輸入法。</summary>
	/// <param name="Line">要寫的內容。</param>
	private static void AppendDiag(string Line) {
		try {
			var Target = Path.Combine(DiagDir(), DiagLogFileName);
			var Full = DateTime.Now.ToString("HH:mm:ss.fff")
				+ " [" + Environment.ProcessId + "][" + SelfTag + "] " + Line;
			AppendLineAtomic(Target, Full, DiagMaxBytes, DiagKeepFiles);
		}
		catch {
			// 靜默：診斷失敗不該讓按鍵流程出錯。
		}
	}

	/// <summary>診斷檔所在目錄。</summary>
	/// <returns>與一般日誌同一個目錄。</returns>
	private static string DiagDir() {
		var Base = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(Base, "NganImeTsf");
	}

	/// <summary>
	/// 這顆鍵是不是由別的程式（改鍵工具、AutoHotkey）合成的。
	/// </summary>
	/// <returns>true 表示合成鍵。</returns>
	/// <remarks>
	/// *為何不用 lParam*：lParam 第 24 位是「擴展鍵」，不是「注入」；TSF 也不會把注入資訊傳進來。
	/// 先前把兩者搞混，導致日誌裡每一顆右方向鍵都被讀成「注入」，整段判讀全錯。
	/// 正確做法是問這一則訊息本身：`GetMessageExtraInfo` 對合成輸入會回
	/// `0xFF515700 | 旗標` 的簽名，其中 `0x10`（`LLKHF_INJECTED`）表示注入。
	/// 這是 Chromium 自己也在用的判定；拿不到訊息時它回 0，那就照實記成非合成。
	/// </remarks>
	private static bool IsSynthesizedInput() {
		try {
			var Info = GetMessageExtraInfo().ToInt64();
			const long SignatureMask = 0xFFFFFF00L;
			const long Signature = 0xFF515700L;
			const long InjectedFlag = 0x10L;
			return (Info & SignatureMask) == Signature && (Info & InjectedFlag) != 0;
		}
		catch {
			return false;
		}
	}

	/// <summary>取目前執行緒正在處理的那則訊息的附加資訊（合成輸入的簽名在這裏）。</summary>
	/// <returns>附加資訊。</returns>
	[DllImport("user32.dll")]
	private static extern IntPtr GetMessageExtraInfo();

	/// <summary>查實體按鍵狀態，用來分辨「修飾鍵是否真的還按着」。</summary>
	/// <param name="VirtualKey">虛擬鍵碼。</param>
	/// <returns>最高位為 1 表示目前按着。</returns>
	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int VirtualKey);
}
