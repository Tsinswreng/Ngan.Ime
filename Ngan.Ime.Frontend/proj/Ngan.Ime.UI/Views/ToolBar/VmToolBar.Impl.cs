using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Keys;

namespace Ngan.Ime.UI.Views.ToolBar;

/// 輸入法頂部工具欄 ViewModel 的既有實現。
/// 剪貼板與日誌圖標高亮通知將在聲明審查通過後補充。
public partial class VmToolBar{
	public partial VmToolBar(ImeUiState UiState, ISvcIme ImeState){
		this.UiState = UiState;
		this.ImeState = ImeState;
		_imePropertyChangedHandler = (_, E)=>{
			if(E.PropertyName == nameof(ISvcIme.IsSimplification)){
				OnPropertyChanged(nameof(HanLabel));
			}
		};
		_uiStatePropertyChangedHandler = (_, E)=>{
			// 剪貼板頁切換時刷新其工具欄圖標高亮色。
			if(E.PropertyName == nameof(ImeUiState.IsClipboardVisible)){
				OnPropertyChanged(nameof(ClipboardForeground));
			}
			// 日誌頁可能由手動釘住態或系統強制態改變，二者都通過合成屬性通知。
			if(E.PropertyName == nameof(ImeUiState.IsRimeLogVisible)){
				OnPropertyChanged(nameof(RimeLogForeground));
			}
			if(E.PropertyName == nameof(ImeUiState.IsVoiceInputVisible)){
				OnPropertyChanged(nameof(VoiceInputForeground));
			}
			if(E.PropertyName == nameof(ImeUiState.IsCandidateCommentVisible)){
				OnPropertyChanged(nameof(CandidateCommentForeground));
			}
			if(E.PropertyName == nameof(ImeUiState.IsSplitKeyboardEnabled)){
				OnPropertyChanged(nameof(SplitKeyboardForeground));
			}
			if(E.PropertyName == nameof(ImeUiState.IsSystemKeyRemappingEnabled)){
				OnPropertyChanged(nameof(SystemKeyRemappingForeground));
			}
		};
		ImeState.PropertyChanged += _imePropertyChangedHandler;
		UiState.PropertyChanged += _uiStatePropertyChangedHandler;
	}

	public static partial VmToolBar Mk(){
		return new VmToolBar(
			Di.GetRSvc<ImeUiState>(),
			Di.GetRSvc<ISvcIme>()
		);
	}

	public partial void ToggleSimplification(){
		_ = ImeState.ToggleSimplificationAsy();
	}

	public partial void ToggleClipboard(){
		UiState.ToggleClipboard();
	}

	public partial void ToggleVoiceInput(){
		UiState.ToggleVoiceInput();
	}

	public partial void ToggleRimeLog(){
		UiState.ToggleRimeLog();
	}

	public partial void ToggleCandidateComment(){
		UiState.ToggleCandidateComment();
	}

	public partial void ToggleSplitKeyboard(){
		UiState.ToggleSplitKeyboard();
	}

	public partial void ToggleSystemKeyRemapping(){
		UiState.ToggleSystemKeyRemapping();
	}

	public partial void Dispose(){
		ImeState.PropertyChanged -= _imePropertyChangedHandler;
		UiState.PropertyChanged -= _uiStatePropertyChangedHandler;
	}
}
