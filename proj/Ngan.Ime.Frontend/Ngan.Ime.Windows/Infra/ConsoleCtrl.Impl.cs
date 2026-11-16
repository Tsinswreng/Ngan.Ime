namespace Ngan.Ime.Windows.Infra;

using System;
using System.IO;
using System.Runtime.InteropServices;

[Doc("""
{{nameof(ConsoleCtrl)}} 的實現：把 Win32 的主控台呼叫包成不拋的形式。

拆成宣告與實現兩個檔是本專案對「有部分實作的型別」的慣例；
宣告檔可以直接當文檔讀，不必看 P/Invoke 的細節。
""")]
public static partial class ConsoleCtrl{

	[Doc("脫離主控台；失敗只印一行到 stderr，不阻止行程啟動。")]
	public static partial void Detach(){
		try{
			if(!FreeConsole()){
				Console.Error.WriteLine("[Console] 脫離主控台失敗 err=" + Marshal.GetLastWin32Error());
				return;
			}
			// 脫離之後原本的主控台 handle 全部失效：把 Console 導向空 writer，
			// 免得日誌的 Console 分支每次寫入都拋例外（檔案分支不受影響）。
			Console.SetOut(TextWriter.Null);
			Console.SetError(TextWriter.Null);
		}catch(Exception Ex){
			Console.Error.WriteLine("[Console] 脫離主控台例外：" + Ex.Message);
		}
	}

	[Doc("忽略 Ctrl+C 與 Ctrl+Break；失敗只印一行，不阻止行程啟動。")]
	public static partial void IgnoreControlEvents(){
		try{
			SetConsoleCtrlHandler(IntPtr.Zero, true);
		}catch(Exception Ex){
			// 沒有主控台（例如從 GUI 宿主啟動）不是錯誤，照常啟動即可。
			Console.Error.WriteLine("[Console] 忽略控制事件失敗：" + Ex.Message);
		}
	}
}
