using Avalime.Core.Keys;
using Avalime.Core.Infra;
using Avalime.ViewModels;
using Avalonia.Media;
using System.ComponentModel;

namespace Avalime.UI.Views.ToolBar;

public partial class VmToolBar : ViewModelBase
	, IMk<VmToolBar>
	, IDisposable
{
	public ImeUiState UiState { get; }
	public ISvcIme ImeState { get; }

	public str HanLabel => ImeState.IsSimplification ? "汉" : "漢";
	public IBrush CandidateCommentForeground => UiState.IsCandidateCommentVisible ? UiCfg.Inst.MainColor : Brushes.White;
	public IBrush VoiceInputForeground => UiState.IsVoiceInputVisible ? UiCfg.Inst.MainColor : Brushes.White;
	public IBrush SplitKeyboardForeground => UiState.IsSplitKeyboardEnabled ? UiCfg.Inst.MainColor : Brushes.White;
	public IBrush SystemKeyRemappingForeground => UiState.IsSystemKeyRemappingEnabled ? UiCfg.Inst.MainColor : Brushes.White;

	readonly PropertyChangedEventHandler _imePropertyChangedHandler;
	readonly PropertyChangedEventHandler _uiStatePropertyChangedHandler;

	public VmToolBar(ImeUiState UiState, ISvcIme ImeState){
		this.UiState = UiState;
		this.ImeState = ImeState;
		_imePropertyChangedHandler = (_, e) => {
			if(e.PropertyName == nameof(ISvcIme.IsSimplification)){
				OnPropertyChanged(nameof(HanLabel));
			}
		};
		_uiStatePropertyChangedHandler = (_, e) => {
			if(e.PropertyName == nameof(ImeUiState.IsVoiceInputVisible)){
				OnPropertyChanged(nameof(VoiceInputForeground));
			}
			if(e.PropertyName == nameof(ImeUiState.IsCandidateCommentVisible)){
				OnPropertyChanged(nameof(CandidateCommentForeground));
			}
			if(e.PropertyName == nameof(ImeUiState.IsSplitKeyboardEnabled)){
				OnPropertyChanged(nameof(SplitKeyboardForeground));
			}
			if(e.PropertyName == nameof(ImeUiState.IsSystemKeyRemappingEnabled)){
				OnPropertyChanged(nameof(SystemKeyRemappingForeground));
			}
		};
		ImeState.PropertyChanged += _imePropertyChangedHandler;
		UiState.PropertyChanged += _uiStatePropertyChangedHandler;
	}

	/// <summary>
	/// 工具欄 VM 的兜底創建路徑。
	/// </summary>
	public static VmToolBar Mk()
	{
		return new VmToolBar(
			Di.GetRSvc<ImeUiState>(),
			Di.GetRSvc<ISvcIme>()
		);
	}

	public void ToggleSimplification(){
		_ = ImeState.ToggleSimplificationAsy();
	}

	public void ToggleClipboard(){
		UiState.ToggleClipboard();
	}

	public partial void ToggleVoiceInput();

	public void ToggleRimeLog(){
		UiState.ToggleRimeLog();
	}

	public void ToggleCandidateComment(){
		UiState.ToggleCandidateComment();
	}

	public void ToggleSplitKeyboard(){
		UiState.ToggleSplitKeyboard();
	}

	public void ToggleSystemKeyRemapping(){
		UiState.ToggleSystemKeyRemapping();
	}

	public void Dispose()
	{
		ImeState.PropertyChanged -= _imePropertyChangedHandler;
		UiState.PropertyChanged -= _uiStatePropertyChangedHandler;
	}
}
