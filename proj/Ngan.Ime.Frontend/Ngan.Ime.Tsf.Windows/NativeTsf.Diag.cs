namespace Ngan.Ime.Tsf.Windows;

using System;
using System.IO;
using System.Runtime.InteropServices;
using Ngan.Ime.Tsf.Ipc;

/// <summary>
/// 按鍵層的診斷日誌：只在「可能出事」的按鍵上落盤，平時不寫。
/// </summary>
/// <remarks>
/// *為什麼要有這個*：改鍵工具（AltNav、AutoHotkey）與輸入法打架時，
/// 能觀察的只有「TSF 送了哪些事件、我們回報吃不吃、引擎怎麼回答」。
/// 這些以前只在 `NGAN_TSF_LOG=1` 才寫，而環境變數對「從 Explorer 啟動的應用」根本設不到，
/// 於是每次都要重跑一次才知道缺什麼。現在有兩條開關，任何一條成立就記錄：
/// + 檔案 `%LOCALAPPDATA%\NganImeTsf\verbose-key` 存在：每一顆鍵都記，兩秒內生效。
/// + 不必檔案的常態記錄：有修飾鍵按着、按鍵是注入的、按鍵是方向鍵或 K。
///   日常打字不會觸發，改鍵工具的現場一定會被記下來。
///
/// *記錄內容*：虛擬鍵碼、本殼算出的修飾鍵位、實體左右修飾鍵狀態、方向、是否徵詢、
/// 是否注入、回報的吃鍵結論、引擎回應摘要。這幾項足以分辨
/// 「改鍵工具吞掉了 K」「我們吃掉了注入的 Down」「Alt 在到達應用前還按着」三種情況。
/// </remarks>
internal static partial class NativeTsf {

	/// <summary>診斷輸出檔名；與一般日誌同目錄，每行以 `[K]` 開頭便於 grep。</summary>
	private const string DiagLogFileName = "tsf-keys.log";

	/// <summary>存在即表示「每一顆鍵都記」。</summary>
	private const string DiagAllFlagFileName = "verbose-key";

	/// <summary>上一次檢查開關檔的時間。</summary>
	private static long DiagFlagCheckedTick;

	/// <summary>開關檔是否存在的快取。</summary>
	private static bool DiagFlagCached;

	/// <summary>開關檔的重新檢查間隔（毫秒）。</summary>
	private const long DiagFlagCheckMs = 2000;

	/// <summary>虛擬鍵碼：K。</summary>
	private const int VkK = 0x4B;

	/// <summary>虛擬鍵碼：左。</summary>
	private const int VkLeft = 0x25;

	/// <summary>虛擬鍵碼：上。</summary>
	private const int VkUp = 0x26;

	/// <summary>虛擬鍵碼：右。</summary>
	private const int VkRight = 0x27;

	/// <summary>虛擬鍵碼：下。</summary>
	private const int VkDown = 0x28;

	/// <summary>診斷檔的大小上限；超過就輪替成 `.old`。</summary>
	private const long DiagMaxBytes = 8L * 1024 * 1024;

	/// <summary>判斷這一顆鍵是否值得記錄。</summary>
	/// <param name="Vk">虛擬鍵碼。</param>
	/// <param name="Mods">本殼算出的修飾鍵位。</param>
	/// <param name="Injected">這顆鍵是不是別的程式注入的。</param>
	/// <returns>true 表示要記。</returns>
	private static bool DiagWanted(int Vk, int Mods, bool Injected) {
		if (DiagAllEnabled()) {
			return true;
		}
		if (Mods != 0 || Injected) {
			return true;
		}
		return Vk is VkK or VkLeft or VkUp or VkRight or VkDown;
	}

	/// <summary>檢查「全部記錄」開關檔。</summary>
	/// <returns>true 表示每一顆鍵都要記。</returns>
	/// <remarks>有快取，不會每顆鍵都碰檔案系統；使用者中途建立或刪除檔案，兩秒內生效。</remarks>
	private static bool DiagAllEnabled() {
		var Now = Environment.TickCount64;
		if (Now - DiagFlagCheckedTick < DiagFlagCheckMs) {
			return DiagFlagCached;
		}
		DiagFlagCheckedTick = Now;
		try {
			DiagFlagCached = File.Exists(Path.Combine(DiagDir(), DiagAllFlagFileName));
		}
		catch (Exception) {
			DiagFlagCached = false;
		}
		return DiagFlagCached;
	}

	/// <summary>診斷檔所在目錄。</summary>
	/// <returns>與一般日誌同一個目錄。</returns>
	private static string DiagDir() {
		var Base = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(Base, "NganImeTsf");
	}

	/// <summary>把一顆按鍵事件的完整觀察寫一行。</summary>
	/// <param name="Vk">虛擬鍵碼。</param>
	/// <param name="Mods">本殼算出的修飾鍵位。</param>
	/// <param name="IsUp">true 表示擡起。</param>
	/// <param name="IsTest">true 表示來自 OnTestKeyXxx。</param>
	/// <param name="LParam">TSF 帶進來的 lParam；第 24 位代表「注入」。</param>
	/// <param name="Eaten">回報給 TSF 的結論。</param>
	/// <param name="Reply">引擎回應；null 表示這一顆鍵沒有問引擎。</param>
	private static void DiagKey(int Vk, int Mods, bool IsUp, bool IsTest, IntPtr LParam, int Eaten, TsfReply? Reply) {
		var Injected = ((LParam.ToInt64() >> 24) & 1) != 0;
		if (!DiagWanted(Vk, Mods, Injected)) {
			return;
		}
		try {
			var LAlt = (GetAsyncKeyState(0xA4) & 0x8000) != 0;
			var RAlt = (GetAsyncKeyState(0xA5) & 0x8000) != 0;
			var LShift = (GetAsyncKeyState(0xA0) & 0x8000) != 0;
			var RShift = (GetAsyncKeyState(0xA1) & 0x8000) != 0;
			var LCtrl = (GetAsyncKeyState(0xA2) & 0x8000) != 0;
			var RCtrl = (GetAsyncKeyState(0xA3) & 0x8000) != 0;
			var Line = "[K] " + (IsUp ? "up" : "dn")
				+ (IsTest ? "/test" : "/key")
				+ " vk=0x" + Vk.ToString("X2") + "(" + Vk + ")"
				+ " mods=" + Mods
				+ " phys=LAlt" + (LAlt ? 1 : 0) + " RAlt" + (RAlt ? 1 : 0)
				+ " LSh" + (LShift ? 1 : 0) + " RSh" + (RShift ? 1 : 0)
				+ " LCt" + (LCtrl ? 1 : 0) + " RCt" + (RCtrl ? 1 : 0)
				+ " inj=" + (Injected ? 1 : 0)
				+ " eat=" + Eaten
				+ " reply=" + (Reply is null
					? "none"
					: "eaten" + (Reply.Eaten ? 1 : 0) + " commit" + Reply.Commits.Count + " pre" + (Reply.Preedit?.Length ?? 0) + " err=" + Reply.Err);
			AppendDiag(Line);
		}
		catch (Exception Exception) {
			LogError("DiagKey EX " + Exception);
		}
	}

	/// <summary>把一行追加到診斷檔；任何失敗都不影響輸入法。</summary>
	/// <param name="Line">要寫的內容。</param>
	private static void AppendDiag(string Line) {
		try {
			var Dir = DiagDir();
			Directory.CreateDirectory(Dir);
			var Target = Path.Combine(Dir, DiagLogFileName);
			var Info = new FileInfo(Target);
			if (Info.Exists && Info.Length > DiagMaxBytes) {
				File.Move(Target, Target + ".old", true);
			}
			File.AppendAllText(Target, DateTime.Now.ToString("HH:mm:ss.fff") + " [" + Environment.ProcessId + "] " + Line + Environment.NewLine);
		}
		catch (Exception) {
			// 靜默：診斷失敗不該讓按鍵流程出錯。
		}
	}

	/// <summary>查實體按鍵狀態，用來分辨「修飾鍵是否真的還按着」。</summary>
	/// <param name="VirtualKey">虛擬鍵碼。</param>
	/// <returns>最高位為 1 表示目前按着。</returns>
	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int VirtualKey);
}
