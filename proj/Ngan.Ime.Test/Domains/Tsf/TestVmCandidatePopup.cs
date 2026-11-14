using Ngan.Ime.Core.Ime;
using Ngan.Ime.UI.Views.CandidatePopup;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Tsf;

[Doc($$"""
被測對象：`{{nameof(VmCandidatePopup)}}`，候選浮窗的 ViewModel。

浮窗的排版要真的開視窗才驗得到，但「顯示甚麼」的決定都在 VM 裡：
組字、候選、高亮、有沒有內容。這些不碰 UI 執行緒，故可在測試裡直接驗。
""")]
public class TestVmCandidatePopup : Tester{
	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(Apply_Should_CopySnapshot), Apply_Should_CopySnapshot!);
		this.AddTestCase(nameof(Apply_Should_HideWhenEmpty), Apply_Should_HideWhenEmpty!);
		this.AddTestCase(nameof(Apply_Should_ClampHighlight), Apply_Should_ClampHighlight!);
		this.AddTestCase(nameof(Clear_Should_ResetAll), Clear_Should_ResetAll!);
	}

	[Doc("""
	套用快照之後，組字、候選、高亮都要與快照一致。
	""")]
	public async Task<nil> Apply_Should_CopySnapshot(obj? O){
		var T = Assert.IsTrue;
		var Vm = VmCandidatePopup.Mk();
		Vm.Apply("ni", Page(highlight: 1, "你", "泥"));
		T(Vm.Preedit == "ni", $"組字應為 ni，實際 {Vm.Preedit}");
		T(Vm.Candidates.Count == 2, $"候選應為 2 個，實際 {Vm.Candidates.Count}");
		T(Vm.Highlighted == 1, $"高亮應為 1，實際 {Vm.Highlighted}");
		T(Vm.HasContent, "有組字時應視為有內容");
		return NIL;
	}

	[Doc("""
	組字與候選都沒有時，View 應據此收起浮窗，故 HasContent 必須是 false。
	""")]
	public async Task<nil> Apply_Should_HideWhenEmpty(obj? O){
		var T = Assert.IsTrue;
		var Vm = VmCandidatePopup.Mk();
		Vm.Apply("", null);
		T(!Vm.HasContent, "沒有任何內容時不該顯示浮窗");
		T(Vm.Highlighted == -1, $"沒有候選時高亮應為 -1，實際 {Vm.Highlighted}");
		return NIL;
	}

	[Doc("""
	引擎可能給出越界的高亮（例如選字後少了一個候選）；要收斂到合法範圍，
	否則視圖拿負數下標會拋例外。
	""")]
	public async Task<nil> Apply_Should_ClampHighlight(obj? O){
		var T = Assert.IsTrue;
		var Vm = VmCandidatePopup.Mk();
		Vm.Apply("ni", Page(highlight: 9, "你", "泥"));
		T(Vm.Highlighted == 0, $"越界高亮應收斂到 0，實際 {Vm.Highlighted}");
		Vm.Apply("ni", Page(highlight: -5, "你"));
		T(Vm.Highlighted == 0, $"負數高亮應收斂到 0，實際 {Vm.Highlighted}");
		return NIL;
	}

	[Doc("""
	清空之後所有欄位都要回到初始值，避免下一次顯示殘留上一輪的候選。
	""")]
	public async Task<nil> Clear_Should_ResetAll(obj? O){
		var T = Assert.IsTrue;
		var Vm = VmCandidatePopup.Mk();
		Vm.Apply("ni", Page(highlight: 0, "你"));
		Vm.Clear();
		T(Vm.Preedit.Length == 0, "組字應清空");
		T(Vm.Candidates.Count == 0, "候選應清空");
		T(Vm.Highlighted == -1, "高亮應重設");
		T(!Vm.HasContent, "清空後不該有內容");
		return NIL;
	}

	[Doc("建一頁候選快照，供各用例共用。")]
	static CandidatePage Page(i32 highlight, params str[] Texts){
		var Page = new CandidatePage{ HighlightedIndex = highlight };
		foreach(var Text in Texts){
			Page.Data!.Add(new Candidate{ Text = Text, Comment = "" });
		}
		return Page;
	}
}
