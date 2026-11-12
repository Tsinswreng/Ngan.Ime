namespace Ngan.Ime.UI;

using Tsinswreng.CsCfg;
using Tsinswreng.CsCore;
using Ngan.Ime.UI.Infra;

[Doc($$"""
`Ime` 界面的共享狀態：body 頁互斥、候選註釋顯示、分體鍵盤與系統按鍵映射兩個持久化偏好，
以及三個鍵盤視圖（主鍵盤、分體左半、分體右半）共用的布局狀態。

*為何要共享*：分體鍵盤是把「一份鍵盤」裁成兩半顯示，三份視圖若各自持有
`IsNumLayout`／`IsShiftLocked`，在主鍵盤上切數字層時兩個半邊就完全不知道——
這不是「少同步一下」，而是狀態模型被架構性分叉。故布局狀態收在本單例。

*為何不從配置讀默認值到欄位初始化器*：那會讓「new 一個 ImeUiState」等於「要求全域配置已就緒」，
測試與設計器預覽都會被拖下水。現在配置只在 DI 建構子裡讀一次，`Mk()` 走純默認值。

*持久化*：只有兩個使用者偏好（分體、按鍵映射）寫回可寫配置；寫盤在背景完成並記日誌，
不讓 UI 線程被 IO 卡住，也不讓失敗變成靜默。

#Eg[
	```cs
	UiState.ToggleClipboard();
	bool Split = UiState.IsSplitKeyboardEnabled;
	```
]
""")]
public partial class ImeUiState : AppVmBase, IMk<ImeUiState>{

	[Doc("唯一可寫的配置源；`Mk()` 建立的實例沒有它（純內存狀態，不持久化）。")]
	readonly ICfgAccessor? Rw;

	[Doc("正在從配置載入：此期間的屬性賦值不得觸發寫盤。")]
	bool Loading = true;

	[Doc("持久化寫盤的串行鎖；避免連點工具欄時多個任務同時整檔重寫。")]
	static readonly Lock PersistLock = new();

	[Doc("純內存實例：沒有配置來源，兩個偏好取默認值且不持久化。供設計器與測試使用。")]
	protected ImeUiState(){
		Loading = false;
		Init();
	}

	[Doc("由容器建立：讀一次配置中的兩個持久化偏好。")]
	public ImeUiState(ICfgReader Cfg, ICfgAccessor Rw){
		this.Rw = Rw;
		IsSplitKeyboardEnabled = Cfg.Get(KeysAppCfg.Keyboard.IsSplitEnabled);
		IsSystemKeyRemappingEnabled = Cfg.Get(KeysAppCfg.Keyboard.IsSystemKeyRemappingEnabled);
		Loading = false;
		Init();
	}

	[Doc("兜底建立：無依賴的純內存實例。")]
	public static ImeUiState Mk(){
		return new ImeUiState();
	}

	#region body 頁

	[Doc("用戶主動點擊工具欄後的日誌頁顯示狀態。")]
	public bool IsRimeLogPinnedVisible{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(IsRimeLogVisible));
			}
		}
	}

	[Doc("本次連接開始時自動顯示的初始日誌頁；只決定首屏。")]
	public bool IsInitialRimeLogVisible{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(IsRimeLogVisible));
			}
		}
	}

	[Doc("是否顯示候選註釋。")]
	public bool IsCandidateCommentVisible{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("是否顯示剪貼板頁。")]
	public bool IsClipboardVisible{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("是否顯示語音輸入頁。")]
	public bool IsVoiceInputVisible{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("日誌頁是否顯示（手動釘住或首屏強制）。")]
	public bool IsRimeLogVisible => IsRimeLogPinnedVisible || IsInitialRimeLogVisible;

	[Doc("使用者是否已經主動選擇過 body 頁；連接完成後據此決定要不要自動切回鍵盤。")]
	public bool HasUserSelectedBodyPage{get; private set;}

	#endregion

	#region 持久化偏好

	[Doc("是否啟用分體鍵盤；變更即寫回可寫配置。")]
	public bool IsSplitKeyboardEnabled{
		get;
		set{
			if(SetProperty(ref field, value) && !Loading){
				PersistBoolCfg(KeysAppCfg.Keyboard.IsSplitEnabled, value);
			}
		}
	}

	[Doc("是否啟用「未處理按鍵 → OS」的系統按鍵映射。")]
	public bool IsSystemKeyRemappingEnabled{
		get;
		set{
			if(SetProperty(ref field, value) && !Loading){
				PersistBoolCfg(KeysAppCfg.Keyboard.IsSystemKeyRemappingEnabled, value);
			}
		}
	}

	#endregion

	#region 鍵盤布局（三個鍵盤視圖共享）

	[Doc("是否處於數字層；主鍵盤與左右半邊共用同一份。")]
	public bool IsNumLayout{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("是否鎖定 Shift（鍵帽高亮）；共用同一份。")]
	public bool IsShiftLocked{
		get;
		set{ SetProperty(ref field, value); }
	}

	#endregion

	[Doc("在鍵盤區與剪貼板頁之間切換；三個 body 頁互斥。")]
	public partial void ToggleClipboard();

	[Doc("從剪貼板頁退出。")]
	public partial void ExitClipboard();

	[Doc("在鍵盤區與語音輸入頁之間切換。")]
	public partial void ToggleVoiceInput();

	[Doc("從語音輸入頁退出。")]
	public partial void ExitVoiceInput();

	[Doc("切換手動釘住的日誌頁。")]
	public partial void ToggleRimeLog();

	[Doc("完整退出日誌頁。")]
	public partial void ExitRimeLog();

	[Doc("開始一次連接時把日誌頁作為首屏。")]
	public partial void BeginInitialRimeLog();

	[Doc("結束一次連接：成功且用戶未打斷時回到鍵盤；失敗則保留日誌。")]
	public partial void CompleteInitialRimeLog(bool IsSuccess);

	[Doc("切換候選註釋顯示。")]
	public partial void ToggleCandidateComment();

	[Doc("切換分體鍵盤偏好。")]
	public partial void ToggleSplitKeyboard();

	[Doc("切換系統按鍵映射偏好。")]
	public partial void ToggleSystemKeyRemapping();

	[Doc("記錄用戶已主動選過 body 頁，並撤銷首屏日誌。")]
	partial void MarkUserSelectedBodyPage();

	[Doc("把布爾偏好寫回可寫配置：先改內存，再在背景存檔。")]
	partial void PersistBoolCfg(KeyNode<bool> Key, bool Value);
}
