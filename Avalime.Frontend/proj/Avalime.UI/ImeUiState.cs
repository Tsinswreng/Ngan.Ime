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

	/// 本次 Rime 連接開始時自動顯示的初始日誌頁。
	/// 它只決定首屏；用戶一旦主動選擇 body 頁，就不再覆蓋該選擇。
	public bool IsInitialRimeLogVisible{
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

	/// 語音輸入頁是否顯示。
	/// 此狀態只替換 body 區，不改動上方 preedit 與 toolbar/candidates 的互斥邏輯。
	public bool IsVoiceInputVisible{
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
		get{return IsRimeLogPinnedVisible || IsInitialRimeLogVisible;}
	}

	/// 使用者是否已經主動選擇過 body 頁。
	/// 連接成功後只在此值為 false 時，才把初始日誌頁自動切回鍵盤。
	public bool HasUserSelectedBodyPage{get; private set;}

	/// 在鍵盤區與剪貼板頁之間切換。
	/// 當準備打開剪貼板頁時，先關閉手動釘住的日誌頁，避免 body 區雙重模式重疊。
	public partial void ToggleClipboard();

	/// 從剪貼板頁退出，回到普通鍵盤區。
	public partial void ExitClipboard();

	/// 在鍵盤區與語音輸入頁之間切換。
	/// 打開語音頁時需要先退出 clipboard / rime log，避免 body 區模式重疊。
	public partial void ToggleVoiceInput();

	/// 從語音輸入頁退出，回到普通鍵盤區。
	public partial void ExitVoiceInput();

	/// 切換手動釘住的日誌頁。
	/// 準備打開日誌頁時需要先退出剪貼板頁，保持 body 區語義單一。
	public partial void ToggleRimeLog();

	/// 完整退出日誌頁，回到普通鍵盤區。
	public partial void ExitRimeLog();

	/// 開始一次 Rime 連接時，將日誌頁作為初始 body 頁展示。
	public partial void BeginInitialRimeLog();

	/// 結束一次 Rime 連接。
	/// 成功時，僅在用戶未主動導航的情況下由初始日誌頁回到鍵盤；失敗則保留初始日誌頁供查閱。
	public partial void CompleteInitialRimeLog(bool IsSuccess);

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

	/// 記錄用戶對 body 頁的明確選擇，並撤銷僅用於首屏的自動日誌頁。
	partial void MarkUserSelectedBodyPage();
}
