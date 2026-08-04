using Ngan.Ime.Core.Keys;
using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.ViewModels;
using Avalonia.Threading;
using System.ComponentModel;

namespace Ngan.Ime.UI.Views.Ime;

public partial class VmIme : ViewModelBase
	, IMk<VmIme>
	, IDisposable
{
	public ISvcIme ImeState { get; }
	public ImeUiState UiState { get; }

	public bool IsComposing{
		get => field;
		set => SetProperty(ref field, value);
	}

	public bool IsClipboardVisible => UiState.IsClipboardVisible;

	public bool IsVoiceInputVisible => UiState.IsVoiceInputVisible;

	public bool IsRimeLogVisible => UiState.IsRimeLogVisible;

	public bool HasPreedit{
		get => field;
		set => SetProperty(ref field, value);
	}

	public bool ShowToolbar => !IsComposing || IsClipboardVisible;
	public bool ShowCandidates => (IsComposing || HasPreedit) && !IsClipboardVisible;
	public bool ShowPreedit => !IsClipboardVisible;
	public bool ShowKeyboard => !IsClipboardVisible && !IsVoiceInputVisible && !IsRimeLogVisible;
	public bool ShowClipboard => IsClipboardVisible;
	public bool ShowVoiceInput => IsVoiceInputVisible;
	/// 日誌、語音、剪貼板與鍵盤由 ImeUiState 保持互斥；此處只投影狀態。
	public bool ShowRimeLog => IsRimeLogVisible;

	readonly PropertyChangedEventHandler _propertyChangedHandler;
	readonly EventHandler<IEnumerable<IKeyEvent>> _afterInputHandler;
	readonly PropertyChangedEventHandler _uiStatePropertyChangedHandler;
	readonly EventHandler<bool> _connectCompletedHandler;

	public VmIme(ISvcIme ImeState, ImeUiState UiState){
		this.ImeState = ImeState;
		this.UiState = UiState;
		_propertyChangedHandler = (_, e) => {
			if(e.PropertyName is nameof(IsComposing) or nameof(HasPreedit)){
				OnPropertyChanged(nameof(ShowToolbar));
				OnPropertyChanged(nameof(ShowCandidates));
				OnPropertyChanged(nameof(ShowPreedit));
				OnPropertyChanged(nameof(ShowKeyboard));
				OnPropertyChanged(nameof(ShowClipboard));
				OnPropertyChanged(nameof(ShowRimeLog));
			}
		};
		PropertyChanged += _propertyChangedHandler;
		_uiStatePropertyChangedHandler = (_, e) => {
			if(e.PropertyName is nameof(ImeUiState.IsClipboardVisible) or nameof(ImeUiState.IsVoiceInputVisible) or nameof(ImeUiState.IsRimeLogVisible)){
				OnPropertyChanged(nameof(IsClipboardVisible));
				OnPropertyChanged(nameof(IsVoiceInputVisible));
				OnPropertyChanged(nameof(IsRimeLogVisible));
				OnPropertyChanged(nameof(ShowToolbar));
				OnPropertyChanged(nameof(ShowCandidates));
				OnPropertyChanged(nameof(ShowPreedit));
				OnPropertyChanged(nameof(ShowKeyboard));
				OnPropertyChanged(nameof(ShowClipboard));
				OnPropertyChanged(nameof(ShowVoiceInput));
				OnPropertyChanged(nameof(ShowRimeLog));
			}
		};
		UiState.PropertyChanged += _uiStatePropertyChangedHandler;

		_afterInputHandler = (_, _) => RefreshCompositionState();
		ImeState.AfterInput += _afterInputHandler;
		_connectCompletedHandler = OnConnectCompleted;
		ImeState.ConnectCompleted += _connectCompletedHandler;
		UiState.BeginInitialRimeLog();
		_ = StartConnectAsy();
	}

	/// <summary>
	/// 根 IME VM 的兜底創建路徑。
	/// </summary>
	public static VmIme Mk()
	{
		return new VmIme(
			Di.GetRSvc<ISvcIme>(),
			Di.GetRSvc<ImeUiState>()
		);
	}

	/// <summary>
	/// 由 UI 主動發起一次後端連接。
	/// 不在這裡 await 到構造函數外層，避免首屏 UI 被同步等待；異常則統一落日誌。
	/// </summary>
	async Task StartConnectAsy()
	{
		try{
			await ImeState.ConnectAsy();
		}catch(Exception Ex){
			AppLog.Error(Ex, "[VmIme] ConnectAsy failed");
		}
	}

	/// <summary>
	/// 後端初始化完成後，統一在 UI 線程刷新可見狀態。
	/// 成功時只在用戶尚未選頁時回到鍵盤；失敗時保留初始日誌頁供查看錯誤。
	/// </summary>
	void OnConnectCompleted(object? Sender, bool IsSuccess)
	{
		Dispatcher.UIThread.Post(() => {
			UiState.CompleteInitialRimeLog(IsSuccess);
			if(IsSuccess){
				RefreshCompositionState();
			}
		});
	}

	void RefreshCompositionState(){
		var composing = ImeState.IsComposing;
		var hasPreedit = !string.IsNullOrEmpty(ImeState.Preedit);
		Dispatcher.UIThread.Post(() => {
			IsComposing = composing;
			HasPreedit = hasPreedit;
		});
	}

	public void Dispose()
	{
		PropertyChanged -= _propertyChangedHandler;
		UiState.PropertyChanged -= _uiStatePropertyChangedHandler;
		ImeState.AfterInput -= _afterInputHandler;
		ImeState.ConnectCompleted -= _connectCompletedHandler;
	}
}
