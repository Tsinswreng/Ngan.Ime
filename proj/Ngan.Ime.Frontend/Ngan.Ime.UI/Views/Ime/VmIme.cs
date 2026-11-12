namespace Ngan.Ime.UI.Views.Ime;

using System.ComponentModel;
using Avalonia.Threading;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
輸入法根 ViewModel：把引擎狀態與共享界面狀態投影成各區域的可見性。

*為何是 singleton*：分體鍵盤的頂條與主界面共用同一個根 VM；若每次 `DiOrMk` 都新建，
建構子副作用（連引擎、重置首屏日誌）會重跑，導致「切到分體後 body 被搶回日誌頁」。
故 DI 註冊為 singleton，且建構子**不再**啟動連接——改由第一次建立視圖時呼叫 {{nameof(Start)}}。

*可見性規則*：body 區同一時間只有一個頁面（鍵盤／剪貼板／語音／日誌），
互斥由 {{nameof(ImeUiState)}} 保證；本類只做投影，不自己維護第二份互斥狀態。

#Eg[
	```cs
	Vm.Start(); // 由 ViewIme.OnAfterInitialized 呼叫一次
	```
]
""")]
public partial class VmIme : AppVmBase, IMk<VmIme>, IDisposable{

	[Doc("引擎狀態。")]
	public IImeState State{get;}

	[Doc("引擎輸入入口（AfterInput / ConnectCompleted）。")]
	public IImeInput Input{get;}

	[Doc("引擎選項（本類透傳，供工具欄透過本 VM 操作）。")]
	public IImeOptions Options{get;}

	[Doc("共享界面狀態。")]
	public ImeUiState UiState{get;}

	[Doc("引擎是否正在組字。")]
	public bool IsComposing{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("是否有非空 preedit。")]
	public bool HasPreedit{
		get;
		set{ SetProperty(ref field, value); }
	}

	[Doc("連接是否已由本 VM 啟動；避免多個視圖重複連線。")]
	bool Started;

	[Doc("工具欄是否可見：沒在組字時顯示，或剪貼板頁需要它。")]
	public bool ShowToolbar => !IsComposing || UiState.IsClipboardVisible;

	[Doc("候選欄是否可見：組字中或有 preedit，且不在剪貼板頁。")]
	public bool ShowCandidates => (IsComposing || HasPreedit) && !UiState.IsClipboardVisible;

	[Doc("預編輯列是否可見。")]
	public bool ShowPreedit => !UiState.IsClipboardVisible;

	[Doc("鍵盤是否可見。")]
	public bool ShowKeyboard => !UiState.IsClipboardVisible && !UiState.IsVoiceInputVisible && !UiState.IsRimeLogVisible;

	[Doc("剪貼板頁是否可見。")]
	public bool ShowClipboard => UiState.IsClipboardVisible;

	[Doc("語音頁是否可見。")]
	public bool ShowVoiceInput => UiState.IsVoiceInputVisible;

	[Doc("日誌頁是否可見。")]
	public bool ShowRimeLog => UiState.IsRimeLogVisible;

	[Doc("頂欄高度；與候選欄、工具欄共用同一規則。")]
	public f64 TopBarHeight => UiState.IsCandidateCommentVisible
		? UiCfg.Inst.TopBarHeight
		: UiCfg.Inst.TopBarHeightNoComment;

	[Doc("建立根 VM 並訂閱引擎與界面狀態。")]
	public VmIme(IImeState State, IImeInput Input, IImeOptions Options, ImeUiState UiState){
		this.State = State;
		this.Input = Input;
		this.Options = Options;
		this.UiState = UiState;
		Input.AfterInput += OnAfterInput;
		Input.ConnectCompleted += OnConnectCompleted;
		UiState.PropertyChanged += OnUiStateChanged;
		base.Init();
	}

	[Doc("兜底建立：無依賴（設計器預覽用）。")]
	protected VmIme(){
		State = default!;
		Input = default!;
		Options = default!;
		UiState = default!;
		base.Init();
	}

	[Doc("兜底建立。")]
	public static VmIme Mk(){
		return new VmIme();
	}

	[Doc($$"""
	由界面在首次建立後呼叫：把首屏設為日誌頁並在背景連接引擎。
	重複呼叫只生效一次（{{nameof(Started)}}）。
	""")]
	public void Start(){
		if(Started){
			return;
		}
		Started = true;
		UiState.BeginInitialRimeLog();
		Fire(Input.ConnectAsy());
	}

	[Doc("一批按鍵處理完：刷新組字狀態與 preedit 有無。")]
	void OnAfterInput(obj? Sender, IEnumerable<IKeyEvent> Keys){
		var Composing = State.IsComposing;
		var HasPreedit = !string.IsNullOrEmpty(State.Preedit);
		Dispatcher.UIThread.Post(() => {
			IsComposing = Composing;
			this.HasPreedit = HasPreedit;
		});
	}

	[Doc("連接完成：成功且用戶未選頁時回到鍵盤；失敗保留日誌。")]
	void OnConnectCompleted(obj? Sender, bool IsSuccess){
		Dispatcher.UIThread.Post(() => {
			UiState.CompleteInitialRimeLog(IsSuccess);
			if(IsSuccess){
				RefreshVisibility();
			}
		});
	}

	[Doc("界面狀態變更：重發所有派生的可見性屬性。")]
	void OnUiStateChanged(obj? Sender, PropertyChangedEventArgs Args){
		switch(Args.PropertyName){
			case nameof(ImeUiState.IsClipboardVisible):
			case nameof(ImeUiState.IsVoiceInputVisible):
			case nameof(ImeUiState.IsRimeLogVisible):
			case nameof(ImeUiState.IsRimeLogPinnedVisible):
			case nameof(ImeUiState.IsInitialRimeLogVisible):
				RefreshVisibility();
				break;
			case nameof(ImeUiState.IsCandidateCommentVisible):
				OnPropertyChanged(nameof(TopBarHeight));
				break;
		}
	}

	[Doc("重發所有可見性與頂欄高度。")]
	void RefreshVisibility(){
		OnPropertyChanged(nameof(IsComposing));
		OnPropertyChanged(nameof(HasPreedit));
		OnPropertyChanged(nameof(ShowToolbar));
		OnPropertyChanged(nameof(ShowCandidates));
		OnPropertyChanged(nameof(ShowPreedit));
		OnPropertyChanged(nameof(ShowKeyboard));
		OnPropertyChanged(nameof(ShowClipboard));
		OnPropertyChanged(nameof(ShowVoiceInput));
		OnPropertyChanged(nameof(ShowRimeLog));
		OnPropertyChanged(nameof(TopBarHeight));
	}

	[Doc("解除事件訂閱。")]
	public void Dispose(){
		if(Input is not null){
			Input.AfterInput -= OnAfterInput;
			Input.ConnectCompleted -= OnConnectCompleted;
		}
		if(UiState is not null){
			UiState.PropertyChanged -= OnUiStateChanged;
		}
		GC.SuppressFinalize(this);
	}
}
