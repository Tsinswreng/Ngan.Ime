using CommunityToolkit.Mvvm.ComponentModel;
using Avalime.Core.Infra;
using Avalime.Core.Infra.Log;
using Tsinswreng.CsCfg;
using System.Threading;

namespace Avalime.UI;

/// `Ime` 界面的共享 UI 狀態。由根 `VmIme` 與工具欄/剪貼板等子模塊共用。
public partial class ImeUiState : ObservableObject, IMk<ImeUiState>
{
	/// <summary>
	/// 共享 UI 狀態本身沒有外部構造依賴，未註冊時可直接 new。
	/// </summary>
	public static ImeUiState Mk()
	{
		return new ImeUiState();
	}

	static readonly Lock PersistLock = new();

	public ImeUiState()
	{
		// 分體開關屬於用戶顯式偏好，需要跨 hide/show 與進程存活保持。
		_IsSplitKeyboardEnabled = KeysCfg.Keyboard.IsSplitEnabled.GetFrom(AppCfg.Inst);
		// 系統按鍵映射同樣屬於顯式偏好，但實際生效位置在 OS 發送前的宿主層。
		_IsSystemKeyRemappingEnabled = KeysCfg.Keyboard.IsSystemKeyRemappingEnabled.GetFrom(AppCfg.Inst);
	}

	/// <summary>
	/// 用戶主動點擊工具欄後的日誌頁顯示狀態。
	/// </summary>
	bool _IsRimeLogPinnedVisible;
	public bool IsRimeLogPinnedVisible{
		get{return _IsRimeLogPinnedVisible;}
		set{
			if(SetProperty(ref _IsRimeLogPinnedVisible, value)){
				OnPropertyChanged(nameof(IsRimeLogVisible));
			}
		}
	}

	/// <summary>
	/// 系統在“尚未完成初始化”等場景下強制要求顯示日誌頁的狀態。
	/// </summary>
	bool _IsRimeLogForcedVisible;
	public bool IsRimeLogForcedVisible{
		get{return _IsRimeLogForcedVisible;}
		set{
			if(SetProperty(ref _IsRimeLogForcedVisible, value)){
				OnPropertyChanged(nameof(IsRimeLogVisible));
			}
		}
	}

	bool _IsCandidateCommentVisible;
	public bool IsCandidateCommentVisible{
		get{return _IsCandidateCommentVisible;}
		set{SetProperty(ref _IsCandidateCommentVisible, value);}
	}

	bool _IsSplitKeyboardEnabled;
	/// <summary>
	/// 用戶手動切換的分體鍵盤狀態。
	/// Android 宿主會監聽此值，決定是否切到 overlay 分體模式。
	/// </summary>
	public bool IsSplitKeyboardEnabled{
		get{return _IsSplitKeyboardEnabled;}
		set{
			if(SetProperty(ref _IsSplitKeyboardEnabled, value)){
				PersistSplitKeyboardEnabled(value);
			}
		}
	}

	bool _IsClipboardVisible;
	public bool IsClipboardVisible{
		get{return _IsClipboardVisible;}
		set{SetProperty(ref _IsClipboardVisible, value);}
	}

	bool _IsSystemKeyRemappingEnabled;
	/// <summary>
	/// 用戶手動切換的系統按鍵映射開關。
	/// 其作用域只覆蓋“Rime 未處理按鍵 -> OS 發送前”的映射鏈路。
	/// </summary>
	public bool IsSystemKeyRemappingEnabled{
		get{return _IsSystemKeyRemappingEnabled;}
		set{
			if(SetProperty(ref _IsSystemKeyRemappingEnabled, value)){
				PersistBoolCfg(KeysCfg.Keyboard.IsSystemKeyRemappingEnabled, value);
			}
		}
	}

	public bool IsRimeLogVisible{
		get{return IsRimeLogPinnedVisible || IsRimeLogForcedVisible;}
	}

	public void ToggleClipboard()
	{
		if(!IsClipboardVisible){
			IsRimeLogPinnedVisible = false;
		}
		IsClipboardVisible = !IsClipboardVisible;
	}

	public void ExitClipboard()
	{
		IsClipboardVisible = false;
	}

	public void ToggleRimeLog()
	{
		if(!IsRimeLogPinnedVisible){
			IsClipboardVisible = false;
		}
		IsRimeLogPinnedVisible = !IsRimeLogPinnedVisible;
	}

	public void ExitRimeLog()
	{
		IsRimeLogPinnedVisible = false;
		IsRimeLogForcedVisible = false;
	}

	public void SetForcedRimeLogVisible(bool Value)
	{
		IsRimeLogForcedVisible = Value;
	}

	public void ToggleCandidateComment()
	{
		IsCandidateCommentVisible = !IsCandidateCommentVisible;
	}

	public void ToggleSplitKeyboard()
	{
		AppLog.Info($"[SplitState] ToggleSplitKeyboard current={IsSplitKeyboardEnabled} next={!IsSplitKeyboardEnabled}");
		IsSplitKeyboardEnabled = !IsSplitKeyboardEnabled;
	}

	public void ToggleSystemKeyRemapping()
	{
		IsSystemKeyRemappingEnabled = !IsSystemKeyRemappingEnabled;
	}

	/// <summary>
	/// 這個狀態不僅影響當前 UI，還會被 Android 宿主用來決定下次 show 時是否直接進分體。
	/// 因此一旦變更，立即寫回可寫配置。
	/// </summary>
	static void PersistSplitKeyboardEnabled(bool Value)
	{
		AppLog.Info($"[SplitState] PersistSplitKeyboardEnabled value={Value}");
		PersistBoolCfg(KeysCfg.Keyboard.IsSplitEnabled, Value);
	}

	/// <summary>
	/// 共享的布爾偏好持久化入口。
	/// UI 線程只更新內存配置；真正寫盤放到後台，避免工具欄點擊被 IO 卡住。
	/// </summary>
	static void PersistBoolCfg(ICfgNode<bool> Key, bool Value)
	{
		AppCfg.Inst.Set(Key, Value);
		_ = Task.Run(() => {
			lock(PersistLock){
				AppCfg.Inst.Save();
			}
		});
	}
}
