namespace Ngan.Ime.Test;

/// 測試宿主入口。
///
/// *現狀*：本專案還沒有測試用例，也沒有接測試框架（CsTreeTest 尚未引入）。
/// 保留一個可執行的入口，是為了讓 `Ngan.Ime.Scripts` 的 `Test`／`TestAotWin` 命令
/// 從第一天就有東西可跑；接入測試框架時只改這個檔案。
internal static class Program{
	/// 目前只回報「尚無用例」；接上框架後改為建立 TestMgr 並執行。
	static i32 Main(str[] Args){
		Console.WriteLine("Ngan.Ime.Test: 尚無測試用例。");
		return 0;
	}
}
