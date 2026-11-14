using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test;

[Doc($$"""
測試入口：固定三步，順序不可調換。

+ 第一步：由 `{{nameof(NganImeTest.MkTree)}}` 建出測試樹；
+ 第二步：交給 `{{nameof(TreeTestExecutor)}}` 跑完整棵樹並打印匯總；
+ 第三步：依失敗數決定進程退出碼。

*覆蓋範圍*：按鍵登記表、按鍵值語義、Rime 鍵碼轉換、鍵盤布局資料、
火山協議編解碼、界面共享狀態機。這些都不需要 UI 線程、不碰真檔案。

#Eg[
	```sh
	dotnet run --project proj/Ngan.Ime.Test
	```
]
""")]
internal static class Program{
	[Doc("""
	跑完整棵測試樹；有失敗時退出碼為 1。
	""")]
	public static async Task<i32> Main(){
		// 1: 建樹；此時各測試類被實例化，用例掛到樹上。
		var Tree = NganImeTest.MkTree();
		// 2: 執行並打印匯總；默認不因失敗而拋，成敗由下面的退出碼表達。
		var Summary = await new TreeTestExecutor().RunEtPrint(Tree);
		// 3: 沒有失敗用例才算成功。
		return Summary.Failed > 0 ? 1 : 0;
	}
}
