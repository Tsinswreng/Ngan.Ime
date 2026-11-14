namespace Ngan.Ime.Tsf.Windows;

using System;
using System.IO;
using System.Text;

/// <summary>
/// 外殼的診斷日誌。
/// </summary>
/// <remarks>
/// *預設關閉*：這段程式跑在別人的行程裡（explorer、記事本），
/// 每個 COM 呼叫都寫一次檔會讓整個桌面變慢；先前 QI／AddRef／Release 逐次落盤，
/// 一次啟動就是上百次檔案 IO。
///
/// *怎麼打開*：設定環境變數 `NGAN_TSF_LOG=1` 後重開應用（TSF 在行程啟動時載入外殼，
/// 環境變數也是那時讀的）。檔案在 `%LOCALAPPDATA%\NganImPoc` 之外另立目錄，
/// 與服務端日誌分開，避免兩邊格式互相干擾。
///
/// *為何仍寫檔*：外殼沒有主控台、也沒有 CLR 日誌庫可用（本 DLL 不帶執行期），
/// 出問題時唯一能留證據的地方就是檔案。
/// </remarks>
internal static unsafe partial class NativeTsf {

	/// <summary>是否輸出診斷日誌；由環境變數 `NGAN_TSF_LOG=1` 開啟。</summary>
	private static readonly bool LogEnabled =
		Environment.GetEnvironmentVariable("NGAN_TSF_LOG") == "1";

	/// <summary>日誌目錄；首次寫入時才建立。</summary>
	private static readonly string LogDir =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NganImeTsf");

	/// <summary>日誌檔完整路徑。</summary>
	private static readonly string LogPath = Path.Combine(LogDir, "tsf-shell.log");

	/// <summary>序號；讓同一秒內的多行仍可排序。</summary>
	private static int LogSeq;

	/// <summary>
	/// 寫一行日誌；日誌關閉時直接返回。
	/// </summary>
	/// <param name="Message">訊息；不需要自帶時間戳與行程號。</param>
	private static void Log(string Message) {
		if (!LogEnabled) {
			return;
		}
		try {
			Directory.CreateDirectory(LogDir);
			var Seq = ++LogSeq;
			var Line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + Environment.ProcessId + "#" + Seq + "] " + Message + Environment.NewLine;
			lock (LogGate) {
				File.AppendAllText(LogPath, Line, Encoding.UTF8);
			}
		}
		catch {
			// 日誌失敗不該影響輸入。
		}
	}
}
