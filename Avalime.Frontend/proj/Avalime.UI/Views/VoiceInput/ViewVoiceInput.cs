using Avalime.Core.Language;
using Avalime.UI.Infra;
using Avalonia.Controls;
using Tsinswreng.Avln.Grid;
using System.ComponentModel;

namespace Avalime.UI.Views.VoiceInput;
using Ctx = VmVoiceInput;

/// 語音輸入頁 View 聲明與界面設計方案。
///
/// # 整體佈局
/// 頁面使用垂直 `GridStack`，主體分成等權重的上下兩部分：
/// - 上半部分至少佔可用 body 高度的 50%，放置多行識別文本框與文本操作列。
/// - 下半部分放置語言入口、狀態/錯誤信息及圓形錄音按鈕。
///
/// 文本框是本頁主要工作區，不把識別結果轉成候選詞，避免候選欄空間不足；
/// 識別結果先留在草稿中，允許完成後人工修改，只有點擊“上屏”才交給宿主。
///
/// # 流式顯示
/// 文本框綁定 `VmVoiceInput.DraftText`。錄音與等待最終結果期間只讀，
/// Provider 通过 VM 以“稳定段 + 临时段”快照更新；完成后恢复编辑。
/// View 不直接解析供应商响应，也不自行维护追加位置。
///
/// # Windows 降級
/// Windows 尚未接系統輸入法 API 時，`IsCommitSupported=false`，上屏按鈕保持禁用，
/// 但錄音、流式展示、人工編輯與清空仍可獨立驗證。
public partial class ViewVoiceInput : AppViewBase<Ctx>, IDisposable{
	/// 监听 VM 状态与派生可用性变化，刷新录音视觉及不能仅靠绑定表达的控件属性。
	PropertyChangedEventHandler? _ctxPropertyChangedHandler;

	/// 建立語音輸入頁、解析 ViewModel 並組裝完整控件樹。
	public partial ViewVoiceInput();

	/// 語音頁根垂直版面。
	/// 行定義應確保文本草稿區至少取得一半可用高度，控制區不得擠壓文本區到不足 50%。
	public GridStack Root = new(IsRow: true);

	/// 顯示與編輯識別草稿的多行文本框。
	/// - 綁定 `DraftText`。
	/// - 支持換行與垂直滾動。
	/// - `CanEditDraft=false` 時只讀，防止流式臨時結果覆蓋人工修改。
	/// - 完成或失敗後恢復編輯，讓用戶修正標點、專名與識別錯誤。
	public TextBox? DraftTextBox;

	/// 文本草稿區的滾動容器。
	/// 長文本必須在本區內滾動，不得把錄音按鈕與語言入口擠出畫面。
	public ScrollViewer? DraftScroll;

	/// 清空識別草稿的按鈕。
	/// 綁定 `CanClearText`；忙碌或草稿為空時禁用，點擊後調用 `VmVoiceInput.ClearText()`。
	public Button? ClearTextButton;

	/// 將人工確認後的草稿提交到外部輸入目標的按鈕。
	/// 綁定 `CanCommitText`；Windows 原型因宿主不支持上屏而保持禁用。
	public Button? CommitTextButton;

	/// 上屏按鈕旁的能力提示。
	/// Windows 不支持上屏時顯示明確說明，避免用戶誤認為按鈕失效；Android 支持後可隱藏。
	public TextBlock? CommitUnsupportedText;

	/// 顯示當前 BCP 47 語言並打開語言選擇覆蓋層的按鈕。
	/// 綁定 `CanSelectLang`，正在錄音或等待尾包時禁用。
	public Button? LangButton;

	/// 語言按鈕的文字內容，綁定 `CurrentLangDisplay`。
	public TextBlock? LangText;

	/// 語音頁中央的圓形錄音按鈕。
	/// - Idle/Completed/Failed：白色，點擊開始新會話。
	/// - Recording：`UiCfg.MainColor`，點擊停止錄音。
	/// - Starting/Finalizing：保留忙碌視覺並禁止重複啟動。
	public Button? RecordButton;

	/// 錄音按鈕中央的麥克風圖形。
	/// 背景使用主色時需要維持足夠對比度，不應與按鈕背景同色。
	public Control? RecordIcon;

	/// 錄音按鈕附近的主要狀態提示，綁定 `StatusText`。
	public TextBlock? StatusText;

	/// 最近一次語音操作錯誤的文字區。
	/// 綁定 `ErrorText` 與 `HasError`；錯誤不覆蓋已識別草稿，方便用戶保存已有結果。
	public TextBlock? ErrorText;

	/// 覆蓋在語音頁上的語言選擇視圖。
	/// 打開時阻止下層文本與錄音控件接收點擊；選擇或取消後恢復主頁交互。
	public ViewVoiceInputLangSelector? LangSelector;

	/// 組裝文本草稿區、文本操作列、語言入口、錄音控制區、狀態區及語言選擇覆蓋層。
	private partial void Render();

	/// 建立佔據至少半頁高度的識別文本區。
	/// 返回內容包含多行文本框和必要的內部滾動，不包含清空/上屏等外部操作。
	private partial Control MkDraftArea();

	/// 建立文本操作列。
	/// 左側放清空，右側放上屏；Windows 不支持上屏時同時顯示能力提示。
	private partial Control MkDraftActionBar();

	/// 建立當前語言入口。
	/// 語言選擇層只負責返回 `NormLangDetail`，實際更新由 `VmVoiceInput.ApplyLang` 統一處理。
	private partial Control MkLangBar();

	/// 建立錄音控制區。
	/// 包含主狀態、錯誤信息和圓形錄音按鈕；尺寸應適配 Windows 原型與未來 Android 鍵盤高度。
	private partial Control MkRecordArea();

	/// 打開語言選擇覆蓋層。
	/// 正在識別時即使收到點擊也不得打開，實現需二次檢查 `CanSelectLang`。
	private partial void OpenLangSelector();

	/// 接收語言選擇結果、交給 VM 並關閉覆蓋層。
	private partial void OnLangSelected(NormLangDetail Lang);

	/// 统一隐藏语言选择覆盖层，供“选择完成”和“取消返回”两条交互路径复用。
	private partial void CloseLangSelector();

	/// 按 VM 階段同步錄音按鈕的背景、圖標對比度與可點擊狀態。
	/// 視覺同步只讀取 VM 狀態，不在 View 中自行改變識別生命週期。
	private partial void SyncRecordingVisual();

	/// 點擊錄音按鈕後，將取消令牌交給 `VmVoiceInput.ToggleRecording`。
	/// 事件處理本身不直接開麥克風或建立網絡連接。
	private partial Task<nil> ToggleRecording(CT Ct);

	/// 點擊清空按鈕後調用 VM；View 不直接修改文本框內容。
	private partial void ClearText();

	/// 點擊上屏按鈕後調用 VM；View 不直接調用 `IKeyboardHost`。
	private partial void CommitText();

	/// 解除語言選擇事件與 VM 狀態訂閱，並取消仍在進行的識別會話。
	public partial void Dispose();
}
