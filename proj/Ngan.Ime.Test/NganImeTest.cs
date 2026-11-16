using Ngan.Ime.Test.Domains.Keys;
using Ngan.Ime.Test.Domains.Layout;
using Ngan.Ime.Test.Domains.Rime;
using Ngan.Ime.Test.Domains.Tsf;
using Ngan.Ime.Test.Domains.UI;
using Ngan.Ime.Test.Domains.Voice;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test;

[Doc($$"""
本程序集對外的入口：只產出測試樹，不執行測試。

根節點名取程序集名 `Ngan.Ime`，被上層收編時才不會與上層的根重名。
執行入口在 `{{nameof(Program)}}`。
""")]
public static class NganImeTest{
	[Doc($$"""
	建出本程序集的整棵測試樹。

	#Rtn[測試樹的根節點；只建樹，不跑用例]
	""")]
	public static ITestNode MkTree(){
		var Tester = new NganImeTester();
		Tester.TestNode = new TestNode{
			UniqName = "Ngan.Ime",
		};
		// 掛完各測試類之後才標記已初始化。
		Tester.Init();
		// 根節點由本方法手工建立，所以框架不會代為設置這個旗標。
		Tester.Inited = true;
		return Tester.TestNode;
	}
}

[Doc($$"""
本程序集的組裝器：只掛測試類，不寫用例。

一個被測對象配一個測試類，用例寫在各自的測試類裏。
""")]
public class NganImeTester : Tester{
	[Doc("""
	把本程序集要跑的各測試類掛到根節點下。
	""")]
	public override void Init(){
		// AddTester 是 ITester 的擴展方法，故顯式用 this 作接收者。
		this.AddTester<TestKeyChars>();
		this.AddTester<TestKeyChar>();
		this.AddTester<TestKeyResolverByScan>();
		this.AddTester<TestRimeKeyCharConverter>();
		this.AddTester<TestKeyLayout>();
		this.AddTester<TestVolcengineProtocolCodec>();
		this.AddTester<TestImeUiState>();
		this.AddTester<TestTsfIpc>();
		this.AddTester<TestTsfKeyPolicy>();
		this.AddTester<TestTsfCompositionPlanner>();
		this.AddTester<TestTsfCaretPolicy>();
		this.AddTester<TestVmCandidatePopup>();
	}
}
