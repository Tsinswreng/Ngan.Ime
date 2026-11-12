namespace Ngan.Ime.UI.Views.VoiceInput;

using Avalonia.Media;
using Ngan.Ime.Core.Language;
using Ngan.Ime.Core.Voice;
using Ngan.Ime.UI.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
語音輸入頁的 ViewModel：協調「音頻採集 → 遠端識別 → 草稿 → 上屏」四件事。

*職責邊界*：本類只依賴 Core 的抽象（{{nameof(IAudioInputSource)}}／{{nameof(IVoiceRecognitionProvider)}}），
不認識火山協議，也不認識 Windows／Android 的錄音 API；換供應商或換平臺都不必改這裡。

*階段機*：Idle → Starting → Recording → Finalizing → Completed／Failed。
「忙碌」期間草稿只讀，避免流式結果覆蓋使用者正在改的字；
`CancelRecognition` 與「失敗」是兩條路：取消不顯示錯誤。

*錯誤文案*：庫層的 {{nameof(VoiceRecognitionException)}} 只帶分類（`Kind`）與英文診斷訊息；
面向使用者的中文在這裡依 `Kind` 映射，故庫不必為 UI 背文案。

#Eg[
	```cs
	await Vm.ToggleRecording(Ct); // 開始／結束／取消，依當前階段自動判斷
	```
]
""")]
public partial class VmVoiceInput : AppVmBase, IMk<VmVoiceInput>, IDisposable{

	[Doc("上屏目標（宿主能力）。")]
	readonly IKeyboardHost KeyboardHost;

	[Doc("識別服務供應商。")]
	readonly IVoiceRecognitionProvider RecognitionProvider;

	[Doc("音頻輸入源（麥克風）。")]
	readonly IAudioInputSource AudioInputSource;

	[Doc("本輪的音頻格式快照；會話開始後不受配置變更影響。")]
	readonly AudioInputOptions AudioOptions;

	[Doc("串行化開始／停止／取消／失敗清理。")]
	readonly SemaphoreSlim SessionGate = new(1, 1);

	[Doc("當前麥克風會話；非忙碌時為 null。")]
	IAudioInputSession? AudioSession;

	[Doc("當前識別會話；非忙碌時為 null。")]
	IVoiceRecognitionSession? RecognitionSession;

	[Doc("本輪會話的取消源；用於「取消」與管線異常時的統一收束。")]
	CancellationTokenSource? SessionCts;

	[Doc("當前識別語言；語言在會話建立時固定。")]
	public INormLang CurrentLang{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(CurrentLangDisplay));
			}
		}
	} = new NormLangDetail{
		Code = "zh-CN",
		NativeName = "中文（中國）",
		EnglishName = "Chinese (China)",
	};

	[Doc("語言按鈕的單行顯示：代碼 ＋ 本地名稱。")]
	public str CurrentLangDisplay{
		get{
			if(CurrentLang is INormLangDetail Detail && !str.IsNullOrWhiteSpace(Detail.NativeName)){
				return $"{Detail.Code}  {Detail.NativeName}";
			}
			return CurrentLang.Code;
		}
	}

	[Doc("前端階段。")]
	public EVoiceInputPhase Phase{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(IsRecording));
				OnPropertyChanged(nameof(IsBusy));
				OnPropertyChanged(nameof(CanSelectLang));
				OnPropertyChanged(nameof(IsDraftReadOnly));
				OnPropertyChanged(nameof(CanClearText));
				OnPropertyChanged(nameof(CanCommitText));
				OnPropertyChanged(nameof(RecordButtonEnabled));
				OnPropertyChanged(nameof(RecordButtonBackground));
				OnPropertyChanged(nameof(RecordButtonBorderBrush));
				OnPropertyChanged(nameof(RecordIconBrush));
			}
		}
	} = EVoiceInputPhase.Idle;

	[Doc("面板中的完整草稿；上屏用它，而不是供應商原始結果。")]
	public str DraftText{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(CanClearText));
				OnPropertyChanged(nameof(CanCommitText));
			}
		}
	} = "";

	[Doc("供應商已確認的穩定文本。")]
	public str FinalizedText{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("供應商可能替換的臨時尾句。")]
	public str InterimText{
		get;
		set{ SetProperty(ref field, value); }
	} = "";

	[Doc("頁面狀態提示。")]
	public str StatusText{
		get;
		set{ SetProperty(ref field, value); }
	} = Todo.I18n("點擊開始語音輸入");

	[Doc("最近一次錯誤的用戶可讀文案；空表示沒有錯誤。")]
	public str ErrorText{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(HasError));
			}
		}
	} = "";

	[Doc("宿主是否支援把文字提交到外部輸入目標。")]
	public bool IsCommitSupported{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(CanCommitText));
				OnPropertyChanged(nameof(ShowCommitUnsupported));
			}
		}
	}

	[Doc("是否正在錄音。")]
	public bool IsRecording => Phase == EVoiceInputPhase.Recording;

	[Doc("是否正在執行不可重入的語音操作。")]
	public bool IsBusy => Phase is EVoiceInputPhase.Starting or EVoiceInputPhase.Recording or EVoiceInputPhase.Finalizing;

	[Doc("語言選擇是否可用。")]
	public bool CanSelectLang => !IsBusy;

	[Doc("草稿是否只讀（忙碌時只讀，避免流式結果覆蓋人工編輯）。")]
	public bool IsDraftReadOnly => IsBusy;

	[Doc("清空是否可用。")]
	public bool CanClearText => !IsBusy && !str.IsNullOrWhiteSpace(DraftText);

	[Doc("上屏是否可用。")]
	public bool CanCommitText => IsCommitSupported && !IsBusy && !str.IsNullOrWhiteSpace(DraftText);

	[Doc("是否顯示「本宿主不支援上屏」提示。")]
	public bool ShowCommitUnsupported => !IsCommitSupported;

	[Doc("錄音按鈕是否可點：Starting 階段不可點，Finalizing 可點（當作取消）。")]
	public bool RecordButtonEnabled => Phase is not EVoiceInputPhase.Starting;

	[Doc("錄音按鈕背景：錄音中為主色，否則黑色。")]
	public IBrush RecordButtonBackground => IsRecording ? UiCfg.Inst.MainColor : Brushes.Black;

	[Doc("錄音按鈕邊框：錄音中為主色，否則白色。")]
	public IBrush RecordButtonBorderBrush => IsRecording ? UiCfg.Inst.MainColor : Brushes.White;

	[Doc("麥克風圖標填色：錄音中為黑色，否則白色。")]
	public IBrush RecordIconBrush => IsRecording ? Brushes.Black : Brushes.White;

	[Doc("是否顯示錯誤區。")]
	public bool HasError => !str.IsNullOrWhiteSpace(ErrorText);

	[Doc("建立並讀取宿主的上屏能力。")]
	public VmVoiceInput(
		IKeyboardHost KeyboardHost
		,IVoiceRecognitionProvider RecognitionProvider
		,IAudioInputSource AudioInputSource
		,AudioInputOptions AudioOptions
	){
		this.KeyboardHost = KeyboardHost;
		this.RecognitionProvider = RecognitionProvider;
		this.AudioInputSource = AudioInputSource;
		this.AudioOptions = AudioOptions;
		IsCommitSupported = KeyboardHost.CanCommitText;
		base.Init();
	}

	[Doc("兜底建立：無依賴（設計器預覽用）。")]
	protected VmVoiceInput(){
		KeyboardHost = default!;
		RecognitionProvider = default!;
		AudioInputSource = default!;
		AudioOptions = new AudioInputOptions();
		base.Init();
	}

	[Doc("兜底建立。")]
	public static VmVoiceInput Mk(){
		return new VmVoiceInput();
	}

	[Doc("套用語言；忙碌時忽略延遲回調。")]
	public partial void ApplyLang(INormLang Lang);

	[Doc("依當前階段開始／結束／取消一輪會話。")]
	public partial Task<nil> ToggleRecording(CT Ct);

	[Doc("給按鈕用的安全入口：啟動會話並把例外交給 HandleErr。")]
	public void ToggleRecordingSafely(){
		Fire(ToggleRecording(default));
	}

	[Doc("開始一輪識別：清狀態 → 建遠端會話 → 開麥克風 → 起兩條管線。")]
	public partial Task<nil> StartRecognition(CT Ct);

	[Doc("停止採集、發尾包、等待最終結果。")]
	public partial Task<nil> StopRecognition(CT Ct);

	[Doc("套用供應商的流式快照（切回 UI 線程）。")]
	public partial void ApplyRecognitionText(VoiceRecognitionResult Update);

	[Doc("清空草稿與錯誤（忙碌時不做事）。")]
	public partial void ClearText();

	[Doc("把草稿交給宿主上屏。")]
	public partial void CommitText();

	[Doc("取消當前會話；保留已取得的文本。")]
	public partial Task<nil> CancelRecognition(CT Ct);

	[Doc("循環讀取麥克風分片並依序送出。")]
	private partial Task<nil> PumpAudio(IAudioInputSession AudioSession, IVoiceRecognitionSession RecognitionSession, CT Ct);

	[Doc("循環讀取識別結果並套用到界面。")]
	private partial Task<nil> PumpResults(IVoiceRecognitionSession RecognitionSession, CT Ct);

	[Doc("看守兩條管線：任一失敗就在鎖內統一收束成 Failed。")]
	private partial Task<nil> WatchPipelinesAsy(Task<nil> SendTask, Task<nil> ReadTask);

	[Doc("冪等釋放音頻、網絡、取消源與任務引用。")]
	private partial Task<nil> ReleaseSessionResources(bool CancelRemote, CT Ct);

	[Doc("把異常映射成保留既有文本的 Failed 狀態。")]
	private partial Task<nil> FailRecognition(Exception Error, CT Ct);

	[Doc("依錯誤分類產生使用者可讀文案。")]
	private static partial str MkErrText(Exception Error);

	[Doc("解除資源（取消在途會話並釋放閘門）。")]
	public partial void Dispose();
}
