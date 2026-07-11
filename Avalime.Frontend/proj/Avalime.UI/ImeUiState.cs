using CommunityToolkit.Mvvm.ComponentModel;
using Avalime.Core.Infra;
using Tsinswreng.CsCfg;

namespace Avalime.UI;

/// `Ime` 界面的共享 UI 狀態。由根 `VmIme` 與工具欄/剪貼板等子模塊共用。
public partial class ImeUiState : ObservableObject, IMk<ImeUiState>
{
	/// 共享狀態寫回可寫配置時的互斥鎖。
	/// 避免多個工具欄開關短時間連點時併發寫盤。
	static readonly Lock PersistLock = new();

	/// 共享 UI 狀態本身沒有外部構造依賴，未註冊時可直接 new。
	public static partial ImeUiState Mk();

	/// 用戶主動點擊工具欄後的日誌頁顯示狀態。
	public bool IsRimeLogPinnedVisible{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(IsRimeLogVisible));
			}
		}
	}

	/// 系統在“尚未完成初始化”等場景下強制要求顯示日誌頁的狀態。
	public bool IsRimeLogForcedVisible{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(IsRimeLogVisible));
			}
		}
	}

	public bool IsCandidateCommentVisible{
		get;
		set{SetProperty(ref field, value);}
	}

	/// 用戶手動切換的分體鍵盤狀態。
	/// Android 宿主會監聽此值，決定是否切到 overlay 分體模式。
	public bool IsSplitKeyboardEnabled{
		get;
		set{
			if(SetProperty(ref field, value)){
				PersistSplitKeyboardEnabled(value);
			}
		}
	} = KeysCfg.Keyboard.IsSplitEnabled.GetFrom(AppCfg.Inst);

	public bool IsClipboardVisible{
		get;
		set{SetProperty(ref field, value);}
	}

	/// 用戶手動切換的系統按鍵映射開關。
	/// 其作用域只覆蓋“Rime 未處理按鍵 -> OS 發送前”的映射鏈路。
	public bool IsSystemKeyRemappingEnabled{
		get;
		set{
			if(SetProperty(ref field, value)){
				PersistBoolCfg(KeysCfg.Keyboard.IsSystemKeyRemappingEnabled, value);
			}
		}
	} = KeysCfg.Keyboard.IsSystemKeyRemappingEnabled.GetFrom(AppCfg.Inst);

	public bool IsRimeLogVisible{
		get{return IsRimeLogPinnedVisible || IsRimeLogForcedVisible;}
	}

	/// 在鍵盤區與剪貼板頁之間切換。
	/// 當準備打開剪貼板頁時，先關閉手動釘住的日誌頁，避免 body 區雙重模式重疊。
	public partial void ToggleClipboard();

	/// 從剪貼板頁退出，回到普通鍵盤區。
	public partial void ExitClipboard();

	/// 切換手動釘住的日誌頁。
	/// 準備打開日誌頁時需要先退出剪貼板頁，保持 body 區語義單一。
	public partial void ToggleRimeLog();

	/// 完整退出日誌頁，同時清除手動釘住態與系統強制態。
	public partial void ExitRimeLog();

	/// 由外部連接流程寫入“系統強制顯示日誌頁”的狀態。
	public partial void SetForcedRimeLogVisible(bool Value);

	/// 切換候選詞 comment 區顯示狀態。
	public partial void ToggleCandidateComment();

	/// 切換分體鍵盤偏好，並通過屬性 setter 觸發持久化。
	public partial void ToggleSplitKeyboard();

	/// 切換系統按鍵映射偏好，並通過屬性 setter 觸發持久化。
	public partial void ToggleSystemKeyRemapping();

	/// 這個狀態不僅影響當前 UI，還會被 Android 宿主用來決定下次 show 時是否直接進分體。
	/// 因此一旦變更，立即寫回可寫配置。
	static partial void PersistSplitKeyboardEnabled(bool Value);

	/// 共享的布爾偏好持久化入口。
	/// UI 線程只更新內存配置；真正寫盤放到後台，避免工具欄點擊被 IO 卡住。
	static partial void PersistBoolCfg(ICfgNode<bool> Key, bool Value);
}
