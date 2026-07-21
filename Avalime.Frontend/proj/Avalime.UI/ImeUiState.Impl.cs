using Avalime.Core.Infra;
using Avalime.Core.Infra.Log;
using Tsinswreng.CsCfg;

namespace Avalime.UI;

public partial class ImeUiState {
	public static partial ImeUiState Mk() {
		return new ImeUiState();
	}

	public partial void ToggleClipboard() {
		// step 1: 進入剪貼板頁前，先解除和它互斥的手動日誌頁。
		if (!IsClipboardVisible) {
			IsRimeLogPinnedVisible = false;
			IsVoiceInputVisible = false;
		}

		// step 2: 再翻轉剪貼板頁狀態。
		IsClipboardVisible = !IsClipboardVisible;
	}

	public partial void ExitClipboard() {
		IsClipboardVisible = false;
	}

	public partial void ToggleVoiceInput() {
		// 語音頁與其他 body 模式互斥；本階段只提供前端頁面入口。
		if (!IsVoiceInputVisible) {
			IsClipboardVisible = false;
			IsRimeLogPinnedVisible = false;
		}
		IsVoiceInputVisible = !IsVoiceInputVisible;
	}

	public partial void ExitVoiceInput() {
		IsVoiceInputVisible = false;
	}

	public partial void ToggleRimeLog() {
		// step 1: 打開手動日誌頁前，先退出剪貼板頁，避免 body 區同時承擔兩種模式。
		if (!IsRimeLogPinnedVisible) {
			IsClipboardVisible = false;
			IsVoiceInputVisible = false;
		}

		// step 2: 再翻轉手動釘住態。
		IsRimeLogPinnedVisible = !IsRimeLogPinnedVisible;
	}

	public partial void ExitRimeLog() {
		// step 1: 同時清空手動態與強制態，讓 UI 完整回到普通模式。
		IsRimeLogPinnedVisible = false;
		IsRimeLogForcedVisible = false;
	}

	public partial void SetForcedRimeLogVisible(bool Value) {
		IsRimeLogForcedVisible = Value;
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
