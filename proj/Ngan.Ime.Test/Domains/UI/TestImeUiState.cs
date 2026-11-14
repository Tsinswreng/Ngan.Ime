using Ngan.Ime.UI;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.UI;

[Doc($$"""
被測對象：`{{nameof(ImeUiState)}}`，界面共享狀態機。

測三件事：body 各頁互斥、首屏日誌的生命週期不搶用戶已選的頁、布局狀態可共享讀寫。
""")]
public class TestImeUiState : Tester{
	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(Toggle_Should_BeMutuallyExclusive), Toggle_Should_BeMutuallyExclusive!);
		this.AddTestCase(nameof(InitialRimeLog_Should_RespectUserSelection), InitialRimeLog_Should_RespectUserSelection!);
		this.AddTestCase(nameof(LayoutState_Should_BeShared), LayoutState_Should_BeShared!);
	}

	[Doc("""
	剪貼板、語音、日誌三頁同時只允許一頁可見。
	""")]
	public async Task<nil> Toggle_Should_BeMutuallyExclusive(obj? O){
		var T = Assert.IsTrue;
		var Ui = ImeUiState.Mk();
		// 1: 開剪貼板。
		Ui.ToggleClipboard();
		T(Ui.IsClipboardVisible, "切換後剪貼板應可見");
		// 2: 開語音，剪貼板要自動收起來。
		Ui.ToggleVoiceInput();
		T(Ui.IsVoiceInputVisible && !Ui.IsClipboardVisible, "語音頁應與剪貼板互斥");
		// 3: 開日誌，語音要自動收起來。
		Ui.ToggleRimeLog();
		T(Ui.IsRimeLogVisible && !Ui.IsVoiceInputVisible, "日誌頁應與語音頁互斥");
		return NIL;
	}

	[Doc("""
	首屏日誌只在用戶尚未自己選過 body 頁時出現；用戶選過之後，連接成功也不得搶回日誌頁。
	""")]
	public async Task<nil> InitialRimeLog_Should_RespectUserSelection(obj? O){
		var T = Assert.IsTrue;
		var Ui = ImeUiState.Mk();
		// 1: 首屏自動開日誌，此時不算用戶選擇。
		Ui.BeginInitialRimeLog();
		T(Ui.IsRimeLogVisible && !Ui.HasUserSelectedBodyPage, "首屏日誌應打開且未標記用戶選擇");
		// 2: 用戶自己切到剪貼板，標記為已選擇。
		Ui.ToggleClipboard();
		T(Ui.HasUserSelectedBodyPage, "用戶操作後應標記已選擇 body 頁");
		// 3: 連接成功收尾時，不得把頁面搶回日誌。
		Ui.CompleteInitialRimeLog(true);
		T(Ui.IsClipboardVisible, "連接成功後不應搶回日誌頁");
		return NIL;
	}

	[Doc("""
	布局狀態是共享讀寫的，寫進去之後能原樣讀出。
	""")]
	public async Task<nil> LayoutState_Should_BeShared(obj? O){
		var T = Assert.IsTrue;
		var Ui = ImeUiState.Mk();
		Ui.IsNumLayout = true;
		Ui.IsShiftLocked = true;
		T(Ui.IsNumLayout && Ui.IsShiftLocked, "布局狀態應可共享讀寫");
		return NIL;
	}
}
