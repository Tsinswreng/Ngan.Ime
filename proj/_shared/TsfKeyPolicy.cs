namespace Ngan.Ime.Tsf.Ipc;

using Tsinswreng.CsCore;

// 本檔只用 BCL 與專案別名，故外殼（不帶 CLR 庫）也能一起編譯。
// `Doc` 屬性在外殼專案裏由 DocShim.cs 提供同名替身。

[Doc($$"""
外殼端的按鍵決策：哪一顆鍵要吃掉、哪一顆要放行，以及「徵詢」與「執行」怎麼配對。

*為何要獨立成型別*：這段邏輯原本散在 `NativeTsf` 的四個回調裏、用四個布林旗標表達，
彼此合法的組合沒有人定義過。「修飾鍵被吃掉」「旗標沒有成對清理」這兩類 bug 都是這樣來的。
抽成純型別之後它不碰 COM、不碰管道，可以直接餵事件序列測。

*三條不變式*：

+ 所有認得的鍵都送引擎，吃不吃由引擎的回答決定，修飾鍵沒有例外。
	這是 Weasel 的規則（`WeaselTSF/KeyEventSink.cpp:11`：只要 `ConvertKeyEvent` 認得就送，
	`*pfEaten` 直接用引擎的回答；`KeyEvent.cpp:22` 還把 Alt 的狀態放進修飾鍵位）。
	先前殼端自己加了「修飾鍵一律放行」，結果 Alt 永遠到不了引擎。
+ 同一顆鍵的「擡起」最多只能吃到與「按下」相同的程度。按下放行而擡起被吃掉，
	應用就會永遠以為那顆鍵還按着——「Alt+K 變成 Alt+下」這一類症狀就是這樣來的。
+ 每個事件只決策一次。TSF 對同一顆鍵可能連送多次 `OnTestKeyDown`（Word 2010 x64），
	也可能只送 `OnKeyDown`（QQ2012）；{{nameof(TryReuseTest)}} 與 {{nameof(TryTakeKey)}}
	保證「問引擎」與「用答案」各只發生一次。

*對照 Weasel（2026-11-16 逐行核對 `WeaselTSF/KeyEventSink.cpp`）*：

+ `OnTestKeyDown`／`OnTestKeyUp` **只在引擎說吃掉時**才把 pending 旗標設起來（`:101-104`、`:130-133`）。
+ `OnKeyDown`／`OnKeyUp` 一旦看到 pending，就**一律回 TRUE** 並清旗標，不再問引擎
	（`:111-118`、`:141-148`）——它不回去看上一次的具體結論。
+ 因此「引擎說不吃時，執行階段的回調會再問一次引擎」是 Weasel 的原樣行為，*不是缺陷*；
	它同時說明第一條與第三條不變式的精確範圍：只對「已被吃掉」的鍵成立。
	殼端因此把 `SetTest` 只在 `Eat == true` 時呼叫、把沿用 pending 的分支寫成 `IsEaten = 1`；
	改這兩行之前先讀本段，否則會把「照 Weasel」改成「自創規則」。

#Eg[
	```cs
	var Policy = new TsfKeyPolicy();
	bool Eat = Policy.DecideEat(0x41, IsUp: false, EngineEaten: true);
	```
]
""")]
public sealed class TsfKeyPolicy{

	[Doc("修飾鍵：Shift（VK_SHIFT 不分左右）。")]
	public const i32 VkShift = 0x10;

	[Doc("修飾鍵：Ctrl（VK_CONTROL 不分左右）。")]
	public const i32 VkCtrl = 0x11;

	[Doc("修飾鍵：Alt；AltNav 這類改鍵工具依賴它的狀態。")]
	public const i32 VkMenu = 0x12;

	[Doc("CapsLock；吃掉它等於讓使用者按不出大小寫。")]
	public const i32 VkCapital = 0x14;

	[Doc("左 Win。")]
	public const i32 VkLWin = 0x5B;

	[Doc("右 Win。")]
	public const i32 VkRWin = 0x5C;

	[Doc("左 Shift 的獨立虛擬鍵碼（部分應用送這一個而不是 VK_SHIFT）。")]
	public const i32 VkLShift = 0xA0;

	[Doc("右 Shift 的獨立虛擬鍵碼。")]
	public const i32 VkRShift = 0xA1;

	[Doc("左 Ctrl 的獨立虛擬鍵碼。")]
	public const i32 VkLControl = 0xA2;

	[Doc("右 Ctrl 的獨立虛擬鍵碼。")]
	public const i32 VkRControl = 0xA3;

	[Doc("左 Alt 的獨立虛擬鍵碼。")]
	public const i32 VkLMenu = 0xA4;

	[Doc("右 Alt 的獨立虛擬鍵碼。")]
	public const i32 VkRMenu = 0xA5;

	[Doc("記錄「按下是否被吃掉」的槽位數。正常打字同時按住的鍵遠少於此；滿了就整批清掉。")]
	const i32 MaxTrackedKeys = 32;

	[Doc("每顆鍵的虛擬鍵碼；0 表示空槽。")]
	readonly i32[] TrackedVks = new i32[MaxTrackedKeys];

	[Doc("與 {{nameof(TrackedVks)}} 同索引：該鍵按下時是否被吃掉。")]
	readonly bool[] TrackedEaten = new bool[MaxTrackedKeys];

	[Doc("已使用的槽位數；滿了就整批清掉，寧可失去成對保護也不要無界成長。")]
	i32 TrackedCount;

	[Doc("上一次 `OnTestKeyDown` 的鍵；{{nameof(HasTestDown)}} 為 false 時無意義。")]
	i32 TestDownVk;

	[Doc("上一次 `OnTestKeyDown` 的吃鍵結論。")]
	bool TestDownEaten;

	[Doc("是否有一個 `OnTestKeyDown` 的結論還沒被 `OnKeyDown` 領走。")]
	bool HasTestDown;

	[Doc("上一次 `OnTestKeyUp` 的鍵；語義同 {{nameof(TestDownVk)}}。")]
	i32 TestUpVk;

	[Doc("上一次 `OnTestKeyUp` 的吃鍵結論。")]
	bool TestUpEaten;

	[Doc("是否有一個 `OnTestKeyUp` 的結論還沒被 `OnKeyUp` 領走。")]
	bool HasTestUp;

	[Doc($$"""
	這顆鍵是不是修飾鍵（Shift／Ctrl／Alt／Win／CapsLock）。

	*這條規則只用來分類，不再用來放行*。先前殼端拿它當「一律不吃、也不問引擎」的依據，
	結果引擎永遠收不到 Alt 等修飾鍵，與 Weasel 的行為不同（Weasel 所有鍵都送引擎，
	吃不吃由引擎回答）。現在修飾鍵照樣送引擎，本方法只供其他判斷參考。

	#Prm[{{nameof(Vk)}}][虛擬鍵碼。]
	#Rtn[true 表示是修飾鍵。]
	""")]
	public static bool IsModifier(i32 Vk){
		return Vk == VkShift || Vk == VkCtrl || Vk == VkMenu || Vk == VkCapital
			|| Vk == VkLWin || Vk == VkRWin
			|| (Vk >= VkLShift && Vk <= VkRMenu);
	}

	[Doc($$"""
	決定這一顆按鍵事件要不要吃掉。

	*按下*：照引擎的回答，並記住結論（給擡起沿用）。
	*擡起*：引擎說吃、而且按下時也被吃，才吃。按下被放行時擡起一定放行，
	否則應用的修飾鍵狀態會卡住。

	#Prm[{{nameof(Vk)}}][虛擬鍵碼。]
	#Prm[{{nameof(IsUp)}}][true 表示擡起。]
	#Prm[{{nameof(EngineEaten)}}][引擎對這一顆事件的回答：是否處理了它。]
	#Rtn[殼端實際要回報給 TSF 的 `IsEaten`。]
	""")]
	public bool DecideEat(i32 Vk, bool IsUp, bool EngineEaten){
		if(!IsUp){
			Remember(Vk, EngineEaten);
			return EngineEaten;
		}
		var DownEaten = Take(Vk);
		return EngineEaten && DownEaten;
	}

	[Doc($$"""
	進入任何一個回調時呼叫：結算「反方向」尚未領走的徵詢結論。

	*為何要結算*：應用可能只送 `OnTestKeyDown` 而不送 `OnKeyDown`（或反之）。
	若不清掉，殘留的旗標會讓之後的按鍵被誤判成「已經問過且已吃掉」，
	於是整臺機器突然打不出字。Weasel 四個回調的入口都做同一件事。

	#Prm[{{nameof(IsUp)}}][這一個回調的方向。]
	""")]
	public void SettleOpposite(bool IsUp){
		if(IsUp){
			HasTestDown = false;
		}else{
			HasTestUp = false;
		}
	}

	[Doc($$"""
	這次 `OnTestKeyXxx` 能否沿用上一次的答案（不問引擎）。

	*回 true 就一定代表「吃掉」*：本類的 `SetTest` 只在引擎說吃掉時才被呼叫
	（`NativeTsf.HandleKeyEvent` 的 `if (IsTest && Eat)`），所以呼叫端領到 true 時回
	`IsEaten = 1` 是正確的——這正是 Weasel `OnKeyDown` 的行為：
	`if (_fTestKeyDownPending) { *pfEaten = TRUE; ... }`（`WeaselTSF/KeyEventSink.cpp:111-118`），
	它也是寫死 TRUE，不回去看上一次的具體值。
	若哪天有人讓 `SetTest` 也記錄「沒吃掉」，這裡就必須改成 `out bool Eaten`——
	這個前提寫在這裡，免得下一輪被當成 bug 改掉。
	""")]
	public bool TryReuseTest(bool IsUp, i32 Vk){
		if(IsUp){
			return HasTestUp && TestUpVk == Vk;
		}
		return HasTestDown && TestDownVk == Vk;
	}

	[Doc($$"""
	記下 `OnTestKeyXxx` 的結論，等對應的 `OnKeyXxx` 領走。
	#Prm[{{nameof(IsUp)}}][方向。]
	#Prm[{{nameof(Vk)}}][虛擬鍵碼。]
	#Prm[{{nameof(Eaten)}}][決策後的吃鍵結論。]
	""")]
	public void SetTest(bool IsUp, i32 Vk, bool Eaten){
		if(IsUp){
			HasTestUp = true;
			TestUpVk = Vk;
			TestUpEaten = Eaten;
		}else{
			HasTestDown = true;
			TestDownVk = Vk;
			TestDownEaten = Eaten;
		}
	}

	[Doc($$"""
	`OnKeyXxx` 進來時，領走同方向、同鍵的徵詢結論。
	#Prm[{{nameof(IsUp)}}][方向。]
	#Prm[{{nameof(Vk)}}][虛擬鍵碼。]
	#Prm[{{nameof(Eaten)}}][領到的吃鍵結論；回傳 false 時無意義。]
	#Rtn[true 表示這一顆鍵已經問過引擎，不要再問一次。]
	""")]
	public bool TryTakeKey(bool IsUp, i32 Vk, out bool Eaten){
		if(IsUp){
			if(!HasTestUp || TestUpVk != Vk){
				Eaten = false;
				return false;
			}
			Eaten = TestUpEaten;
			HasTestUp = false;
			return true;
		}
		if(!HasTestDown || TestDownVk != Vk){
			Eaten = false;
			return false;
		}
		Eaten = TestDownEaten;
		HasTestDown = false;
		return true;
	}

	[Doc("丟掉全部狀態；換輸入上下文或停用時呼叫，避免把上一輪的結論帶到新情境。")]
	public void Reset(){
		Array.Clear(TrackedVks);
		Array.Clear(TrackedEaten);
		TrackedCount = 0;
		HasTestDown = false;
		HasTestUp = false;
		TestDownVk = 0;
		TestUpVk = 0;
		TestDownEaten = false;
		TestUpEaten = false;
	}

	[Doc("記住某顆鍵按下時的結論；同一顆鍵重複記錄就覆蓋。")]
	void Remember(i32 Vk, bool Eaten){
		for(var Index = 0; Index < TrackedCount; Index++){
			if(TrackedVks[Index] == Vk){
				TrackedEaten[Index] = Eaten;
				return;
			}
		}
		if(TrackedCount >= MaxTrackedKeys){
			// 滿了：清空重來。失去成對保護只影響「按下沒被吃、擡起被吃」這種極端情況，
			// 而無界成長會在長期執行的行程（explorer）裏慢慢吃記憶體。
			TrackedCount = 0;
		}
		TrackedVks[TrackedCount] = Vk;
		TrackedEaten[TrackedCount] = Eaten;
		TrackedCount++;
	}

	[Doc("取走某顆鍵按下時的結論並清掉記錄；沒有記錄時回 false（視為沒被吃）。")]
	bool Take(i32 Vk){
		for(var Index = 0; Index < TrackedCount; Index++){
			if(TrackedVks[Index] != Vk){
				continue;
			}
			var Eaten = TrackedEaten[Index];
			// 用尾端元素補洞，不必維持順序；槽位數很小，這是常數時間。
			TrackedVks[Index] = TrackedVks[TrackedCount - 1];
			TrackedEaten[Index] = TrackedEaten[TrackedCount - 1];
			TrackedVks[TrackedCount - 1] = 0;
			TrackedEaten[TrackedCount - 1] = false;
			TrackedCount--;
			return Eaten;
		}
		return false;
	}
}
