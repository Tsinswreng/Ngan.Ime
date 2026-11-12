//Impl: 聲明見 ImeUiState.cs。
namespace Ngan.Ime.UI;

using Tsinswreng.CsCfg;

public partial class ImeUiState{

	[Doc("進入剪貼板前先解除互斥頁，再翻轉自身。")]
	public partial void ToggleClipboard(){
		MarkUserSelectedBodyPage();
		if(!IsClipboardVisible){
			IsRimeLogPinnedVisible = false;
			IsVoiceInputVisible = false;
		}
		IsClipboardVisible = !IsClipboardVisible;
	}

	[Doc("退出剪貼板頁。")]
	public partial void ExitClipboard(){
		MarkUserSelectedBodyPage();
		IsClipboardVisible = false;
	}

	[Doc("進入語音頁前先解除互斥頁，再翻轉自身。")]
	public partial void ToggleVoiceInput(){
		MarkUserSelectedBodyPage();
		if(!IsVoiceInputVisible){
			IsClipboardVisible = false;
			IsRimeLogPinnedVisible = false;
		}
		IsVoiceInputVisible = !IsVoiceInputVisible;
	}

	[Doc("退出語音頁。")]
	public partial void ExitVoiceInput(){
		MarkUserSelectedBodyPage();
		IsVoiceInputVisible = false;
	}

	[Doc("進入日誌頁前先解除互斥頁，再翻轉手動釘住態。")]
	public partial void ToggleRimeLog(){
		MarkUserSelectedBodyPage();
		if(!IsRimeLogPinnedVisible){
			IsClipboardVisible = false;
			IsVoiceInputVisible = false;
		}
		IsRimeLogPinnedVisible = !IsRimeLogPinnedVisible;
	}

	[Doc("顯式退出日誌頁。")]
	public partial void ExitRimeLog(){
		MarkUserSelectedBodyPage();
		IsRimeLogPinnedVisible = false;
	}

	[Doc("每次新的連接都重新決定首屏，不沿用上一輪的用戶選擇。")]
	public partial void BeginInitialRimeLog(){
		HasUserSelectedBodyPage = false;
		IsRimeLogPinnedVisible = false;
		IsInitialRimeLogVisible = true;
	}

	[Doc("連接結束：成功且用戶沒打斷才自動回到鍵盤；失敗保留日誌供查看。")]
	public partial void CompleteInitialRimeLog(bool IsSuccess){
		if(IsSuccess && !HasUserSelectedBodyPage){
			IsInitialRimeLogVisible = false;
		}
	}

	[Doc("記錄用戶選擇並收起首屏日誌。")]
	partial void MarkUserSelectedBodyPage(){
		HasUserSelectedBodyPage = true;
		IsInitialRimeLogVisible = false;
	}

	[Doc("切換候選註釋。")]
	public partial void ToggleCandidateComment(){
		IsCandidateCommentVisible = !IsCandidateCommentVisible;
	}

	[Doc("切換分體鍵盤。")]
	public partial void ToggleSplitKeyboard(){
		IsSplitKeyboardEnabled = !IsSplitKeyboardEnabled;
	}

	[Doc("切換系統按鍵映射。")]
	public partial void ToggleSystemKeyRemapping(){
		IsSystemKeyRemappingEnabled = !IsSystemKeyRemappingEnabled;
	}

	[Doc("先改內存中的可寫配置，再把存檔丟到背景；失敗記日誌，不靜默。")]
	partial void PersistBoolCfg(KeyNode<bool> Key, bool Value){
		var Cfg = Rw;
		if(Cfg is null){
			return;
		}
		Cfg.SetNoSave(Key, Value);
		Fire(SaveAsy(Cfg));
	}

	[Doc("串行存檔：連點開關時只會一個接一個寫，不會互相覆蓋。")]
	static async Task<nil> SaveAsy(ICfgAccessor Cfg){
		await Task.Run(() => {
			lock(PersistLock){
				Cfg.Save();
			}
		});
		return NIL;
	}
}
