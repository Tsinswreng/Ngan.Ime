namespace Ngan.Ime.UI.Views.ToolBar;

using Avalonia.Media;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.UI.Infra;

[Doc($$"""
工具欄 ViewModel：七個入口的顯示狀態與點擊命令。

*為何每個入口一個高亮屬性*：按鈕的填充色由「該頁是否打開」決定，
用屬性 + 綁定比在 View 裡手動改色可測、也不會漏掉某一處。
六個 `XxxForeground` 共用同一個計算規則（開啟 → 主色，否則白），
故在同一個狀態事件裡統一發通知。

*為何不持有 View*：本類只改 {{nameof(ImeUiState)}} 與命令引擎，不碰任何控件。

#Eg[
	```cs
	Vm.ToggleClipboard();
	```
]
""")]
public partial class VmToolBar : AppVmBase, IMk<VmToolBar>, IDisposable{

	[Doc("共享界面狀態（各頁開關）。")]
	public ImeUiState UiState{get;}

	[Doc("引擎狀態（簡繁）。")]
	public IImeState State{get;}

	[Doc("引擎選項（切換簡繁）。")]
	public IImeOptions Options{get;}

	[Doc("簡繁按鈕文字：簡體模式顯示「汉」，否則「漢」。")]
	public str HanLabel => State.IsSimplification ? Todo.I18n("汉") : Todo.I18n("漢");

	[Doc("候選註釋入口的高亮色。")]
	public IBrush CandidateCommentForeground => Pick(UiState.IsCandidateCommentVisible);

	[Doc("剪貼板入口的高亮色。")]
	public IBrush ClipboardForeground => Pick(UiState.IsClipboardVisible);

	[Doc("日誌入口的高亮色。")]
	public IBrush RimeLogForeground => Pick(UiState.IsRimeLogVisible);

	[Doc("語音入口的高亮色。")]
	public IBrush VoiceInputForeground => Pick(UiState.IsVoiceInputVisible);

	[Doc("分體鍵盤入口的高亮色。")]
	public IBrush SplitKeyboardForeground => Pick(UiState.IsSplitKeyboardEnabled);

	[Doc("按鍵映射入口的高亮色。")]
	public IBrush SystemKeyRemappingForeground => Pick(UiState.IsSystemKeyRemappingEnabled);

	[Doc("頂欄高度；與候選欄共用同一規則。")]
	public f64 TopBarHeight => UiState.IsCandidateCommentVisible
		? UiCfg.Inst.TopBarHeight
		: UiCfg.Inst.TopBarHeightNoComment;

	[Doc("依開關選色：開啟用主色，否則白色。")]
	static IBrush Pick(bool Enabled){
		return Enabled ? UiCfg.Inst.MainColor : Brushes.White;
	}

	[Doc("建立工具欄 VM 並訂閱兩個狀態源。")]
	public VmToolBar(ImeUiState UiState, IImeState State, IImeOptions Options){
		this.UiState = UiState;
		this.State = State;
		this.Options = Options;
		State.PropertyChanged += OnStateChanged;
		UiState.PropertyChanged += OnUiStateChanged;
		base.Init();
	}

	[Doc("兜底建立：無依賴（設計器預覽用）。")]
	protected VmToolBar(){
		UiState = default!;
		State = default!;
		Options = default!;
		base.Init();
	}

	[Doc("兜底建立。")]
	public static VmToolBar Mk(){
		return new VmToolBar();
	}

	[Doc("切換簡繁：交給引擎，成功後由引擎狀態回寫按鈕文字。")]
	public void ToggleSimplification(){
		Fire(Options.ToggleSimplificationAsy());
	}

	[Doc("切換剪貼板頁。")]
	public void ToggleClipboard(){
		UiState.ToggleClipboard();
	}

	[Doc("切換語音頁。")]
	public void ToggleVoiceInput(){
		UiState.ToggleVoiceInput();
	}

	[Doc("切換日誌頁。")]
	public void ToggleRimeLog(){
		UiState.ToggleRimeLog();
	}

	[Doc("切換候選註釋顯示。")]
	public void ToggleCandidateComment(){
		UiState.ToggleCandidateComment();
	}

	[Doc("切換分體鍵盤。")]
	public void ToggleSplitKeyboard(){
		UiState.ToggleSplitKeyboard();
	}

	[Doc("切換系統按鍵映射。")]
	public void ToggleSystemKeyRemapping(){
		UiState.ToggleSystemKeyRemapping();
	}

	[Doc("引擎狀態變更：簡繁切換時刷新按鈕文字。")]
	void OnStateChanged(obj? Sender, System.ComponentModel.PropertyChangedEventArgs Args){
		if(Args.PropertyName == nameof(IImeState.IsSimplification)){
			OnPropertyChanged(nameof(HanLabel));
		}
	}

	[Doc("界面狀態變更：刷新對應入口的高亮色與頂欄高度。")]
	void OnUiStateChanged(obj? Sender, System.ComponentModel.PropertyChangedEventArgs Args){
		switch(Args.PropertyName){
			case nameof(ImeUiState.IsCandidateCommentVisible):
				OnPropertyChanged(nameof(CandidateCommentForeground));
				OnPropertyChanged(nameof(TopBarHeight));
				break;
			case nameof(ImeUiState.IsClipboardVisible):
				OnPropertyChanged(nameof(ClipboardForeground));
				break;
			case nameof(ImeUiState.IsRimeLogVisible):
				OnPropertyChanged(nameof(RimeLogForeground));
				break;
			case nameof(ImeUiState.IsVoiceInputVisible):
				OnPropertyChanged(nameof(VoiceInputForeground));
				break;
			case nameof(ImeUiState.IsSplitKeyboardEnabled):
				OnPropertyChanged(nameof(SplitKeyboardForeground));
				break;
			case nameof(ImeUiState.IsSystemKeyRemappingEnabled):
				OnPropertyChanged(nameof(SystemKeyRemappingForeground));
				break;
		}
	}

	[Doc("解除事件訂閱。")]
	public void Dispose(){
		if(State is not null){
			State.PropertyChanged -= OnStateChanged;
		}
		if(UiState is not null){
			UiState.PropertyChanged -= OnUiStateChanged;
		}
		GC.SuppressFinalize(this);
	}
}
