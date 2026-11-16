namespace Ngan.Ime.Tsf.Ipc;


[Doc("組字的一步；一步對應一次非同步編輯會話。")]
public enum ETsfCompositionStep{
	[Doc("開始組字（取消組字時也是先確認有組字可結束）。")]
	Start = 0,

	[Doc("把最終文字寫進組字範圍。")]
	Insert = 1,

	[Doc("結束組字。")]
	End = 2,
}

[Doc($$"""
一步組字操作要做什麼；`Clear` 只在 {{nameof(ETsfCompositionStep.End)}} 有意義。
#Prm[{{nameof(Step)}}][要做哪一步。]
#Prm[{{nameof(Clear)}}][結束前是否清空組字範圍。]
""")]
public readonly record struct TsfCompositionPlan(ETsfCompositionStep Step, bool Clear);

[Doc($$"""
由「引擎這一輪的回答」決定要排哪幾步組字；純函數。

*為何要抽出來*：這段判斷原本埋在殼的 `ScheduleCompositionWork` 裡（static 類 + COM 呼叫），
只有真機能驗。抽成純函數之後，「有／無上屏 × 有／無組字 × 有／無現存組字」八種組合可以逐條測。

*規則照 Weasel*（`WeaselTSF/WeaselTSF.cpp` 的 `DoEditSession`、`Composition.cpp` 的三個步驟）：
+ 有上屏文字：還沒組字就先開組字 → 寫入 → 結束（保留文字）；之後若還有組字串，再開一次組字；
+ 沒有上屏但有組字串：還沒組字就開組字，讓它在打字期間活着；
+ 兩者都沒有而我們還握着組字：排一個「結束並清空」。

#Eg[
	```cs
	var steps = {{nameof(TsfCompositionPlanner)}}.{{nameof(Plan)}}(hasComposition: false, hasCommit: true, hasPreedit: false);
	// → [(Start, false), (Insert, false), (End, false)]
	```
]
""")]
public static class TsfCompositionPlanner{

	[Doc($$"""
	算出要依序排入的步驟。
	#Prm[{{nameof(HasComposition)}}][目前是否握着組字。]
	#Prm[{{nameof(HasCommit)}}][引擎這一輪是否產生了上屏文字。]
	#Prm[{{nameof(HasPreedit)}}][引擎這一輪是否還有組字串。]
	#Rtn[要排的步驟；空陣列表示什麼都不做。]
	""")]
	public static TsfCompositionPlan[] Plan(bool HasComposition, bool HasCommit, bool HasPreedit){
		if(!HasCommit && !HasPreedit){
			// 引擎說沒有組字了（上屏完畢、取消、或本來就沒在組字）。
			return HasComposition ? [new(ETsfCompositionStep.End, true)] : [];
		}
		if(HasCommit){
			var R = new List<TsfCompositionPlan>(4);
			if(!HasComposition){
				R.Add(new(ETsfCompositionStep.Start, false));
			}
			R.Add(new(ETsfCompositionStep.Insert, false));
			R.Add(new(ETsfCompositionStep.End, false));
			if(HasPreedit){
				// 自動上屏之後引擎可能立刻開始下一段組字（Weasel 同一個分支）。
				R.Add(new(ETsfCompositionStep.Start, false));
			}
			return [.. R];
		}
		// 只有組字串：讓組字活起來。
		return HasComposition ? [] : [new(ETsfCompositionStep.Start, false)];
	}
}
