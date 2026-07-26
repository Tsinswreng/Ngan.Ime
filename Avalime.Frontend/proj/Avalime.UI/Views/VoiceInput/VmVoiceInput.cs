using Avalime.Core.Infra;
using Avalime.Core.Language;
using Avalime.Core.Voice;
using Avalime.ViewModels;

namespace Avalime.UI.Views.VoiceInput;

/// 語音輸入頁的 ViewModel 聲明與前端狀態設計。
///
/// # 職責邊界
/// - 保存用戶選擇的識別語言與可編輯文本草稿。
/// - 接收供應商無關的流式文本快照，將穩定段和臨時段合成到文本框。
/// - 控制錄音、等待尾包、完成、錯誤等前端階段及按鈕可用條件。
/// - 僅在用戶主動點擊“上屏”後，才把最終草稿交給 `IKeyboardHost`。
/// - 不直接引用火山引擎 API、WebSocket、Windows 錄音 API 或 Android 麥克風 API。
///
/// # 文本衝突策略
/// 錄音和等待尾包期間，識別服務可能反覆改寫臨時尾句，因此文本框只讀；
/// 識別完成或失敗後恢復人工編輯。這避免“用戶剛修改文字，下一個流式包又覆蓋修改”的競態。
public partial class VmVoiceInput : ViewModelBase, IMk<VmVoiceInput>{
	/// 把用户确认后的草稿提交到当前平台输入目标的宿主抽象。
	/// VM 只依赖此跨平台接口；Windows/Android 的具体提交方式留在各自宿主程序集。
	readonly IKeyboardHost KeyboardHost;

	/// 供應商無關的流式語音識別入口；Windows 注入火山實現，Android 未來替換為平台適配實現。
	readonly IVoiceRecognitionProvider RecognitionProvider;

	/// 建立语音输入 VM 并读取当前宿主的上屏能力。
	/// 后续接入识别 Provider 时，在此构造函数继续注入抽象，不允许 VM 反查全局 DI。
	public partial VmVoiceInput(IKeyboardHost KeyboardHost, IVoiceRecognitionProvider RecognitionProvider);

	/// 當前語音識別語言。
	/// 使用 BCP 47 完整標籤區分地區變體，例如 `zh-CN`、`zh-TW`、`en-US`、`es-MX`。
	/// 切換語言只影響下一次識別；正在錄音時語言按鈕應禁用，避免中途改變 Provider 參數。
	public INormLang CurrentLang{
		get;
		set{SetProperty(ref field, value);}
	} = new NormLangDetail(){
		Code = "zh-CN",
		NativeName = "中文（中國）",
		EnglishName = "Chinese (China)",
	};

	/// 當前語言按鈕的單行顯示文字。
	/// 顯示格式由 VM 統一生成，例如 `zh-CN  中文（中國）`，View 不自行拼接業務文字。
	/// //Note 感覺不太符合Mvvm規範。不應該用轉換器嗎
	public str CurrentLangDisplay{
		get;
		set{SetProperty(ref field, value);}
	} = "zh-CN  中文（中國）";

	/// 當前識別會話所處的前端階段。
	/// setter 需要同步通知所有派生可用性屬性及錄音按鈕視覺屬性。
	public EVoiceInputPhase Phase{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(IsRecording));
				OnPropertyChanged(nameof(IsBusy));
				OnPropertyChanged(nameof(CanSelectLang));
				OnPropertyChanged(nameof(CanEditDraft));
				OnPropertyChanged(nameof(CanClearText));
				OnPropertyChanged(nameof(CanCommitText));
			}
		}
	} = EVoiceInputPhase.Idle;

	/// 文本框展示與人工編輯的完整草稿。
	/// 流式識別時由 `ApplyRecognitionText` 使用穩定段和臨時段重建；完成後允許用戶任意修改。
	/// 点击“上屏”使用此屬性，而不是直接使用 Provider 的原始結果。
	public str DraftText{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(CanClearText));
				OnPropertyChanged(nameof(CanCommitText));
			}
		}
	} = "";

	/// Provider 已確認、不會再修改的文本快照。
	/// 此值主要供流式合併與診斷使用；View 的主文本框仍綁定 `DraftText`。
	public str FinalizedText{
		get;
		set{SetProperty(ref field, value);}
	} = "";

	/// Provider 當前可能在下一包被替換的臨時尾句。
	/// UI 可在未來選擇以不同顏色展示，但第一版先與穩定文本合併顯示在同一文本框中。
	public str InterimText{
		get;
		set{SetProperty(ref field, value);}
	} = "";

	/// 頁面當前狀態提示，例如“正在連接”“正在錄音”“正在等待最終結果”。
	/// 正常狀態與錯誤詳情分開，避免錯誤消失後仍殘留在主要操作提示中。
	public str StatusText{
		get;
		set{SetProperty(ref field, value);}
	} = "點擊開始語音輸入";

	/// 最近一次錄音或識別錯誤的可讀信息。
	/// 錯誤發生後保留已識別文本；開始新會話時清空此值。
	public str ErrorText{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(HasError));
			}
		}
	} = "";

	/// 當前宿主是否真正支持把文字提交到外部輸入目標。
	/// Windows 原型宿主目前返回 `false`，因此上屏按鈕禁用；Android 適配完成後返回 `true`。
	public bool IsCommitSupported{
		get;
		set{
			if(SetProperty(ref field, value)){
				OnPropertyChanged(nameof(CanCommitText));
			}
		}
	} = false;

	/// 錄音按鈕是否處於錄音高亮狀態。
	/// View 使用此屬性把按鈕背景從白色切換為 `UiCfg.MainColor`。
	public bool IsRecording{
		get{return Phase == EVoiceInputPhase.Recording;}
	}

	/// 是否正在執行不可重入的語音操作。
	/// `Starting`、`Recording`、`Finalizing` 均視為忙碌，防止同時開始多個會話。
	public bool IsBusy{
		get{return Phase is EVoiceInputPhase.Starting or EVoiceInputPhase.Recording or EVoiceInputPhase.Finalizing;}
	}

	/// 語言選擇入口是否可用。
	/// 正在識別時禁用；完成、失敗或空閒時均可為下一次會話選擇語言。
	public bool CanSelectLang{
		get{return !IsBusy;}
	}

	/// 文本框是否允許人工修改。
	/// 流式更新期間只讀，完成或失敗後可修改識別結果，再由用戶決定是否上屏。
	public bool CanEditDraft{
		get{return !IsBusy;}
	}

	/// 清空按鈕是否可用。
	/// 忙碌時禁止清空，避免下一個识别包又把文本补回来；非忙碌且草稿非空时可用。
	public bool CanClearText{
		get{return !IsBusy && !str.IsNullOrWhiteSpace(DraftText);}
	}

	/// 上屏按鈕是否可用。
	/// 必須同時滿足宿主支持上屏、會話不忙碌且草稿非空；Windows 原型因此會自然保持禁用。
	public bool CanCommitText{
		get{return IsCommitSupported && !IsBusy && !str.IsNullOrWhiteSpace(DraftText);}
	}

	/// 是否顯示錯誤區。
	public bool HasError{
		get{return !str.IsNullOrWhiteSpace(ErrorText);}
	}

	/// 語音頁 VM 的兜底創建路徑。
	public static partial VmVoiceInput Mk();

	/// 接收語言選擇頁返回的標準化語言，更新 `CurrentLang` 與顯示文字。
	/// 正在識別時即使收到延遲回調也應忽略，保證一輪會話的語言參數不變。
	public partial void ApplyLang(NormLangDetail Lang);

	/// 由錄音按鈕觸發開始或停止動作。
	/// 空閒、完成或失敗狀態下開始新會話；錄音狀態下請求停止；其他過渡狀態忽略重複點擊。
	public partial Task<nil> ToggleRecording(CT Ct);

	/// 開始一次新識別會話。
	/// 實現順序應為：清錯誤與舊臨時段 → 進入 Starting → 啟動 Provider → 進入 Recording。
	public partial Task<nil> StartRecognition(CT Ct);

	/// 停止採集音頻並等待 Provider 返回最後穩定文本。
	/// 此方法不應直接把文字上屏；完成後僅進入 `Completed` 並開放人工編輯。
	public partial Task<nil> StopRecognition(CT Ct);

	/// 接收 Provider 的流式文本快照。
	/// 每次使用 `FinalizedText + InterimText` 重建 `DraftText`，而不是盲目追加響應文本。
	public partial void ApplyRecognitionText(VoiceRecognitionTextUpdate Update);

	/// 清空草稿、穩定段、臨時段與錯誤信息。
	/// 忙碌狀態下不執行，避免破壞正在進行的會話資料。
	public partial void ClearText();

	/// 將用戶確認後的 `DraftText` 提交給宿主輸入目標。
	/// Windows 宿主不支持時保持禁用；實現仍需二次檢查能力和空文本，不能只依賴按鈕狀態。
	public partial void CommitText();

	/// 語音頁退出、輸入法隱藏或宿主釋放時取消當前會話並回收資源。
	/// 取消不等同於清空，已經取得的文本仍保留供用戶處理。
	public partial Task<nil> CancelRecognition(CT Ct);
}
