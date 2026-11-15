namespace Ngan.Ime.Windows.Infra;

using System;

/// <summary>
/// <see cref="ConsoleCtrl"/> 的實現：把 `SetConsoleCtrlHandler` 包成不拋的呼叫。
/// </summary>
/// <remarks>
/// 拆成宣告與實現兩個檔是本專案對「有部分實作的型別」的慣例；
/// 這裡的實現只有五行，拆檔的價值在於宣告檔可以直接當文檔讀，不必看 P/Invoke 的細節。
/// </remarks>
public static partial class ConsoleCtrl{

	/// <summary>忽略主控台控制事件；失敗只印一行到 stderr，不阻止行程啟動。</summary>
	public static partial void IgnoreControlEvents(){
		try{
			SetConsoleCtrlHandler(IntPtr.Zero, true);
		}catch(Exception Ex){
			// 沒有主控台（例如從 GUI 宿主啟動）不是錯誤，照常啟動即可。
			Console.Error.WriteLine("[Console] 忽略控制事件失敗：" + Ex.Message);
		}
	}
}
