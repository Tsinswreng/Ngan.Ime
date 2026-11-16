using Ngan.Ime.Tsf.Ipc;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Tsf;

[Doc($$"""
被測對象：{{nameof(TsfCompositionPlanner)}}，即「這一顆鍵要排哪幾步組字」的決策。

這批用例對應的是「上屏只發生一次、組字該活多久」這兩件事：
有／無上屏 × 有／無組字串 × 有／無現存組字，共八種組合。
殼那邊只負責照著這個序列排隊，判斷本身不碰 COM，故可以直接在這裡釘死。
""")]
public class TestTsfCompositionPlanner : Tester{

	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(NoContent_Should_EndWhenComposing), NoContent_Should_EndWhenComposing!);
		this.AddTestCase(nameof(NoContent_Should_DoNothingWhenIdle), NoContent_Should_DoNothingWhenIdle!);
		this.AddTestCase(nameof(CommitWithoutComposition_Should_StartInsertEnd), CommitWithoutComposition_Should_StartInsertEnd!);
		this.AddTestCase(nameof(CommitWithComposition_Should_SkipStart), CommitWithComposition_Should_SkipStart!);
		this.AddTestCase(nameof(CommitWithPreedit_Should_StartAgain), CommitWithPreedit_Should_StartAgain!);
		this.AddTestCase(nameof(PreeditOnly_Should_StartOnce), PreeditOnly_Should_StartOnce!);
		this.AddTestCase(nameof(PreeditOnly_WithComposition_Should_DoNothing), PreeditOnly_WithComposition_Should_DoNothing!);
	}

	[Doc("""
	引擎說沒有組字了而我們還握着組字：排一個「結束並清空」。
	""")]
	public async Task<nil> NoContent_Should_EndWhenComposing(obj? O){
		var T = Assert.IsTrue;
		var Steps = TsfCompositionPlanner.Plan(HasComposition: true, HasCommit: false, HasPreedit: false);
		T(Steps.Length == 1, $"應只有一步，實際 {Steps.Length}");
		T(Steps[0].Step == ETsfCompositionStep.End, $"應為結束，實際 {Steps[0].Step}");
		T(Steps[0].Clear, "取消組字時要清空範圍");
		return NIL;
	}

	[Doc("""
	本來就沒有組字、引擎也沒有內容：什麼都不必做。
	""")]
	public async Task<nil> NoContent_Should_DoNothingWhenIdle(obj? O){
		var T = Assert.IsTrue;
		T(TsfCompositionPlanner.Plan(false, false, false).Length == 0, "不該排任何步驟");
		return NIL;
	}

	[Doc("""
	有上屏、還沒組字：開始 → 寫入 → 結束，且結束時不清空（保留剛寫進去的字）。
	""")]
	public async Task<nil> CommitWithoutComposition_Should_StartInsertEnd(obj? O){
		var T = Assert.IsTrue;
		var Steps = TsfCompositionPlanner.Plan(HasComposition: false, HasCommit: true, HasPreedit: false);
		T(Steps.Length == 3, $"應為三步，實際 {Steps.Length}");
		T(Steps[0].Step == ETsfCompositionStep.Start, "第一步應為開始組字");
		T(Steps[1].Step == ETsfCompositionStep.Insert, "第二步應為寫入");
		T(Steps[2].Step == ETsfCompositionStep.End, "第三步應為結束");
		T(!Steps[2].Clear, "上屏結束時不可清空（否則剛寫的字會被刪掉）");
		return NIL;
	}

	[Doc("""
	有上屏、已經在組字：不必再開一次組字，直接寫入 → 結束。
	""")]
	public async Task<nil> CommitWithComposition_Should_SkipStart(obj? O){
		var T = Assert.IsTrue;
		var Steps = TsfCompositionPlanner.Plan(HasComposition: true, HasCommit: true, HasPreedit: false);
		T(Steps.Length == 2, $"應為兩步，實際 {Steps.Length}");
		T(Steps[0].Step == ETsfCompositionStep.Insert, "第一步應為寫入");
		T(Steps[1].Step == ETsfCompositionStep.End, "第二步應為結束");
		return NIL;
	}

	[Doc("""
	有上屏而且引擎立刻開始下一段組字：結束之後再開一次組字。
	""")]
	public async Task<nil> CommitWithPreedit_Should_StartAgain(obj? O){
		var T = Assert.IsTrue;
		var Steps = TsfCompositionPlanner.Plan(HasComposition: true, HasCommit: true, HasPreedit: true);
		T(Steps.Length == 3, $"應為三步，實際 {Steps.Length}");
		T(Steps[0].Step == ETsfCompositionStep.Insert, "第一步應為寫入");
		T(Steps[1].Step == ETsfCompositionStep.End, "第二步應為結束");
		T(Steps[2].Step == ETsfCompositionStep.Start, "第三步應為重新開始組字");
		return NIL;
	}

	[Doc("""
	只有組字串、還沒組字：開一次組字，讓它在打字期間活着。
	""")]
	public async Task<nil> PreeditOnly_Should_StartOnce(obj? O){
		var T = Assert.IsTrue;
		var Steps = TsfCompositionPlanner.Plan(HasComposition: false, HasCommit: false, HasPreedit: true);
		T(Steps.Length == 1, $"應只有一步，實際 {Steps.Length}");
		T(Steps[0].Step == ETsfCompositionStep.Start, "應為開始組字");
		return NIL;
	}

	[Doc("""
	只有組字串、已經在組字：不必重開（重開會重置範圍與選取）。
	""")]
	public async Task<nil> PreeditOnly_WithComposition_Should_DoNothing(obj? O){
		var T = Assert.IsTrue;
		T(TsfCompositionPlanner.Plan(true, false, true).Length == 0, "不該排任何步驟");
		return NIL;
	}
}
