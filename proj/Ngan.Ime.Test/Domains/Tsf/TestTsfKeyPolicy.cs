using Ngan.Ime.Tsf.Ipc;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Tsf;

[Doc($$"""
被測對象：`{{nameof(TsfKeyPolicy)}}`，即外殼端「這顆鍵吃不吃」的決策。

這批用例對應的全是「只有真機才看得出來」的症狀：
修飾鍵被吃掉導致 Alt+按鍵 黏住、按下與擡起不成對、
應用只送一半事件（Word 只送多次 TestKeyDown、QQ 只送 KeyDown）時旗標殘留。
把它抽成純型別之後，這些症狀在單元測試裏就能重現。
""")]
public class TestTsfKeyPolicy : Tester{

	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(Modifier_Should_FollowEngine), Modifier_Should_FollowEngine!);
		this.AddTestCase(nameof(Up_Should_MirrorDownEaten), Up_Should_MirrorDownEaten!);
		this.AddTestCase(nameof(Up_Should_Pass_WhenDownPassed), Up_Should_Pass_WhenDownPassed!);
		this.AddTestCase(nameof(Repeated_TestDown_Should_ReuseAnswer), Repeated_TestDown_Should_ReuseAnswer!);
		this.AddTestCase(nameof(KeyDown_Should_TakeTestAnswer), KeyDown_Should_TakeTestAnswer!);
		this.AddTestCase(nameof(TestUp_Should_SettlePendingDown), TestUp_Should_SettlePendingDown!);
		this.AddTestCase(nameof(Reset_Should_ForgetEverything), Reset_Should_ForgetEverything!);
	}

	[Doc("""
	修飾鍵照樣送引擎，吃不吃由引擎回答；殼端不再另立「修飾鍵一律放行」的規則。

	理由：Weasel 就是所有鍵都送引擎（`WeaselTSF/KeyEventSink.cpp:11`），
	引擎對 Alt 等修飾鍵也有發言權。先前殼端自己決定放行，等於讓引擎永遠看不到它們。
	""")]
	public async Task<nil> Modifier_Should_FollowEngine(obj? O){
		var T = Assert.IsTrue;
		var Policy = new TsfKeyPolicy();
		foreach(var Vk in new[]{
			TsfKeyPolicy.VkShift, TsfKeyPolicy.VkCtrl, TsfKeyPolicy.VkMenu,
			TsfKeyPolicy.VkCapital, TsfKeyPolicy.VkLWin, TsfKeyPolicy.VkRWin,
			TsfKeyPolicy.VkLShift, TsfKeyPolicy.VkRMenu,
		}){
			T(TsfKeyPolicy.IsModifier(Vk), $"vk=0x{Vk:X} 應被視為修飾鍵");
			T(Policy.DecideEat(Vk, IsUp: false, EngineEaten: true), $"vk=0x{Vk:X} 引擎說吃就要吃");
			T(!Policy.DecideEat(Vk, IsUp: false, EngineEaten: false), $"vk=0x{Vk:X} 引擎說不吃就不吃");
		}
		return NIL;
	}

	[Doc("""
	按下被吃掉時，擡起也要吃掉——應用從頭到尾都不該看到這顆鍵，留下半個會讓狀態不一致。
	""")]
	public async Task<nil> Up_Should_MirrorDownEaten(obj? O){
		var T = Assert.IsTrue;
		var Policy = new TsfKeyPolicy();
		T(Policy.DecideEat(0x41, IsUp: false, EngineEaten: true), "按下的吃鍵結論應照引擎");
		T(Policy.DecideEat(0x41, IsUp: true, EngineEaten: true), "按下被吃，擡起也該被吃");
		return NIL;
	}

	[Doc("""
	按下被放行時，擡起一定放行——即使引擎在擡起時說「我處理了」。

	這是本輪修 AltNav 衝突的核心：引擎對「Alt 擡起」與「Alt 按下」的回答可以不對稱，
	若照單全收，應用就會永遠以為 Alt 還按着，之後所有合成鍵都變成 Alt+某鍵。
	""")]
	public async Task<nil> Up_Should_Pass_WhenDownPassed(obj? O){
		var T = Assert.IsTrue;
		var Policy = new TsfKeyPolicy();
		T(!Policy.DecideEat(0x4B, IsUp: false, EngineEaten: false), "按下沒被吃");
		T(!Policy.DecideEat(0x4B, IsUp: true, EngineEaten: true), "按下沒被吃時，擡起不可以被吃");
		return NIL;
	}

	[Doc("""
	同一顆鍵被重複徵詢（Word 2010 x64 實測）時要沿用上一次的結論，
	否則同一顆鍵會被引擎處理兩次、打出兩個字。
	""")]
	public async Task<nil> Repeated_TestDown_Should_ReuseAnswer(obj? O){
		var T = Assert.IsTrue;
		var Policy = new TsfKeyPolicy();
		T(!Policy.TryReuseTest(IsUp: false, Vk: 0x41), "第一次徵詢沒有答案可沿用");
		Policy.SetTest(IsUp: false, Vk: 0x41, Eaten: true);
		T(Policy.TryReuseTest(IsUp: false, Vk: 0x41), "同鍵同方向應可沿用");
		T(!Policy.TryReuseTest(IsUp: false, Vk: 0x42), "不同鍵不可沿用");
		T(!Policy.TryReuseTest(IsUp: true, Vk: 0x41), "不同方向不可沿用");
		return NIL;
	}

	[Doc("""
	`OnKeyDown` 要能領走 `OnTestKeyDown` 的結論，而且只領一次（避免重複問引擎）。
	""")]
	public async Task<nil> KeyDown_Should_TakeTestAnswer(obj? O){
		var T = Assert.IsTrue;
		var Policy = new TsfKeyPolicy();
		Policy.SetTest(IsUp: false, Vk: 0x41, Eaten: true);
		T(Policy.TryTakeKey(IsUp: false, Vk: 0x41, out var Eaten), "應領到徵詢結論");
		T(Eaten, "領到的結論應為吃掉");
		T(!Policy.TryTakeKey(IsUp: false, Vk: 0x41, out _), "第二次不該再領到");
		return NIL;
	}

	[Doc("""
	擡起事件到來時要結算「按下」尚未領走的徵詢結論。

	應用若只送 TestKeyDown 而不送 KeyDown（協議外的行為），旗標不清就會讓之後
	每一顆鍵都被誤判成「已經問過且已吃掉」，整臺機器打不出字。
	""")]
	public async Task<nil> TestUp_Should_SettlePendingDown(obj? O){
		var T = Assert.IsTrue;
		var Policy = new TsfKeyPolicy();
		Policy.SetTest(IsUp: false, Vk: 0x41, Eaten: true);
		Policy.SettleOpposite(IsUp: true);
		T(!Policy.TryReuseTest(IsUp: false, Vk: 0x41), "按下方向的殘留結論應已被結算");
		Policy.SetTest(IsUp: true, Vk: 0x41, Eaten: true);
		Policy.SettleOpposite(IsUp: false);
		T(!Policy.TryReuseTest(IsUp: true, Vk: 0x41), "擡起方向的殘留結論也應被結算");
		return NIL;
	}

	[Doc("""
	換輸入上下文時要能一次清乾淨，不可以把上一輪的結論帶到新控件。
	""")]
	public async Task<nil> Reset_Should_ForgetEverything(obj? O){
		var T = Assert.IsTrue;
		var Policy = new TsfKeyPolicy();
		Policy.DecideEat(0x41, IsUp: false, EngineEaten: true);
		Policy.SetTest(IsUp: false, Vk: 0x41, Eaten: true);
		Policy.Reset();
		T(!Policy.TryReuseTest(IsUp: false, Vk: 0x41), "徵詢結論應被清掉");
		T(!Policy.DecideEat(0x41, IsUp: true, EngineEaten: true), "按下的吃鍵記錄應被清掉");
		return NIL;
	}
}
