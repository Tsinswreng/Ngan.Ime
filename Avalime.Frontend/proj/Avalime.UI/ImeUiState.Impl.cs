using Avalime.Core.Infra;
using Avalime.Core.Infra.Log;
using Tsinswreng.CsCfg;

namespace Avalime.UI;

public partial class ImeUiState {
	public static partial ImeUiState Mk() {
		return new ImeUiState();
	}

	public partial void ToggleClipboard() {
		// 任何工具欄操作都代表用戶已選擇 body 頁，不再讓初始化流程搶回日誌頁。
		MarkUserSelectedBodyPage();

		// step 1: 進入剪貼板頁前，先解除和它互斥的手動日誌頁。
		if (!IsClipboardVisible) {
			IsRimeLogPinnedVisible = false;
			IsVoiceInputVisible = false;
		}

		// step 2: 再翻轉剪貼板頁狀態。
		IsClipboardVisible = !IsClipboardVisible;
	}

	public partial void ExitClipboard() {
		MarkUserSelectedBodyPage();
		IsClipboardVisible = false;
	}

	public partial void ToggleVoiceInput() {
		MarkUserSelectedBodyPage();

		// 語音頁與其他 body 模式互斥；本階段只提供前端頁面入口。
		if (!IsVoiceInputVisible) {
			IsClipboardVisible = false;
			IsRimeLogPinnedVisible = false;
		}
		IsVoiceInputVisible = !IsVoiceInputVisible;
	}

	public partial void ExitVoiceInput() {
		MarkUserSelectedBodyPage();
		IsVoiceInputVisible = false;
	}

	public partial void ToggleRimeLog() {
		MarkUserSelectedBodyPage();

		// step 1: 打開手動日誌頁前，先退出剪貼板頁，避免 body 區同時承擔兩種模式。
		if (!IsRimeLogPinnedVisible) {
			IsClipboardVisible = false;
			IsVoiceInputVisible = false;
		}

		// step 2: 再翻轉手動釘住態。
		IsRimeLogPinnedVisible = !IsRimeLogPinnedVisible;
	}

	public partial void ExitRimeLog() {
		// 顯式退出日誌頁就是回到鍵盤的使用者選擇。
		MarkUserSelectedBodyPage();
		IsRimeLogPinnedVisible = false;
	}

	public partial void BeginInitialRimeLog() {
		// 每個新的連接流程都有自己的首屏決策，不能沿用上一輪的用戶操作標記。
		HasUserSelectedBodyPage = false;
		IsRimeLogPinnedVisible = false;
		IsInitialRimeLogVisible = true;
	}

	public partial void CompleteInitialRimeLog(bool IsSuccess) {
		// 失敗時保留日誌以提供錯誤輸出；成功且未被用戶打斷時才回到鍵盤。
		if(IsSuccess && !HasUserSelectedBodyPage){
			IsInitialRimeLogVisible = false;
		}
	}

	partial void MarkUserSelectedBodyPage() {
		HasUserSelectedBodyPage = true;
		IsInitialRimeLogVisible = false;
	}

	public partial void ToggleCandidateComment() {
		IsCandidateCommentVisible = !IsCandidateCommentVisible;
	}

	public partial void ToggleSplitKeyboard() {
		AppLog.Info($"[SplitState] ToggleSplitKeyboard current={IsSplitKeyboardEnabled} next={!IsSplitKeyboardEnabled}");
		IsSplitKeyboardEnabled = !IsSplitKeyboardEnabled;
	}

	public partial void ToggleSystemKeyRemapping() {
		IsSystemKeyRemappingEnabled = !IsSystemKeyRemappingEnabled;
	}

	static partial void PersistSplitKeyboardEnabled(bool Value) {
		AppLog.Info($"[SplitState] PersistSplitKeyboardEnabled value={Value}");
		PersistBoolCfg(KeysCfg.Keyboard.IsSplitEnabled, Value);
	}

	static partial void PersistBoolCfg(ICfgNode<bool> Key, bool Value) {
		// step 1: 先更新內存中的可寫配置，保證當前 UI 能立即看到新值。
		AppCfg.Inst.Set(Key, Value);

		// step 2: 寫盤放到後台並串行化，避免 UI 線程被 IO 卡住。
		_ = Task.Run(() => {
			lock (PersistLock) {
				AppCfg.Inst.Save();
			}
		});
	}
}
