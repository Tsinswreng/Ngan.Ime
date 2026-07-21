using System.ComponentModel;
using Avalime.Core.Infra;
using Avalime.Core.Keys;
using Avalime.ViewModels;
using Avalonia.Media;

namespace Avalime.UI.Views.ToolBar;

/// 輸入法頂部工具欄的 ViewModel 聲明。
/// 統一提供各功能入口、當前功能頁的圖標高亮色及長生命週期狀態訂閱。
public partial class VmToolBar : ViewModelBase, IMk<VmToolBar>, IDisposable{
	/// 工具欄各功能頁共用的 UI 狀態。
	public ImeUiState UiState{get;}

	/// Rime 輸入狀態；目前用於簡繁切換及其顯示文字。
	public ISvcIme ImeState{get;}

	/// 簡繁切換按鈕文字。
	public str HanLabel{
		get{return ImeState.IsSimplification ? "汉" : "漢";}
	}

	/// 候選註釋功能啟用時使用主色，否則使用白色。
	public IBrush CandidateCommentForeground{
		get{return UiState.IsCandidateCommentVisible ? UiCfg.Inst.MainColor : Brushes.White;}
	}

	/// 剪貼板頁打開時使用主色，否則使用白色。
	public IBrush ClipboardForeground{
		get{return UiState.IsClipboardVisible ? UiCfg.Inst.MainColor : Brushes.White;}
	}

	/// 日誌頁顯示時使用主色，否則使用白色。
	/// 強制顯示與用戶手動釘住兩種情況都應視爲日誌頁已打開。
	public IBrush RimeLogForeground{
		get{return UiState.IsRimeLogVisible ? UiCfg.Inst.MainColor : Brushes.White;}
	}

	/// 語音輸入頁打開時使用主色，否則使用白色。
	public IBrush VoiceInputForeground{
		get{return UiState.IsVoiceInputVisible ? UiCfg.Inst.MainColor : Brushes.White;}
	}

	/// 分體鍵盤啟用時使用主色，否則使用白色。
	public IBrush SplitKeyboardForeground{
		get{return UiState.IsSplitKeyboardEnabled ? UiCfg.Inst.MainColor : Brushes.White;}
	}

	/// 系統按鍵映射啟用時使用主色，否則使用白色。
	public IBrush SystemKeyRemappingForeground{
		get{return UiState.IsSystemKeyRemappingEnabled ? UiCfg.Inst.MainColor : Brushes.White;}
	}

	/// 接收 Rime 狀態變更並刷新簡繁按鈕文字的事件處理器。
	readonly PropertyChangedEventHandler _imePropertyChangedHandler;

	/// 接收共享 UI 狀態變更並刷新各工具欄高亮色的事件處理器。
	readonly PropertyChangedEventHandler _uiStatePropertyChangedHandler;

	/// 建立工具欄 ViewModel 並訂閱所需的長生命週期狀態。
	public partial VmToolBar(ImeUiState UiState, ISvcIme ImeState);

	/// 工具欄 ViewModel 的兜底創建路徑。
	public static partial VmToolBar Mk();

	/// 切換 Rime 簡繁輸出模式。
	public partial void ToggleSimplification();

	/// 在普通鍵盤與剪貼板頁之間切換。
	public partial void ToggleClipboard();

	/// 在普通鍵盤與語音輸入頁之間切換。
	public partial void ToggleVoiceInput();

	/// 在普通鍵盤與 Rime 日誌頁之間切換。
	public partial void ToggleRimeLog();

	/// 切換候選詞註釋顯示狀態。
	public partial void ToggleCandidateComment();

	/// 切換分體鍵盤偏好。
	public partial void ToggleSplitKeyboard();

	/// 切換系統按鍵映射偏好。
	public partial void ToggleSystemKeyRemapping();

	/// 解除工具欄對 Rime 與共享 UI 狀態的事件訂閱。
	public partial void Dispose();
}
