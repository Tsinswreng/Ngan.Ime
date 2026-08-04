using Ngan.Ime.Core.Language;
using Ngan.Ime.UI.Infra;
using Avalonia.Controls;
using Tsinswreng.Avln.Grid;
using System.ComponentModel;

namespace Ngan.Ime.UI.Views.VoiceInput;
using Ctx = VmVoiceInput;

/// 語音輸入頁 View 聲明與界面設計方案。
///
/// # 整體佈局
/// 頁面根節點使用橫向 `GridStack`，把有限的輸入法 body 高度轉化為可利用的橫向空間：
/// - 左欄是識別草稿區，使用 1 Star，約佔可用寬度的三分之一，並吃滿全部可用高度。
/// - 中間只保留一條窄分隔線，不使用 Margin 製造不可利用的空白。
/// - 右欄是操作區，使用 2 Star，約佔可用寬度的三分之二，以垂直 `GridStack` 組織全部操作。
///
/// 左欄不得再在文本框下方放置橫跨整頁的操作列；清空、上屏、語言、狀態與錄音均移入右欄。
/// 這樣即使 Windows 測試窗口高度較小，文本框仍能取得完整高度，錄音按鈕與底部操作也不會被裁切。
///
/// # 右側操作區
/// 右欄由上至下排列：
/// - 當前識別語言入口，使用 Auto 高度。
/// - 狀態與錯誤信息區，使用 Star 高度吸收剩餘空間；文字過長時換行，不向下擠出按鈕。
/// - 圓形錄音按鈕，使用 Auto 高度並保持完整可見。
/// - Windows 上屏能力提示，僅在宿主不支持時顯示。
/// - 清空與上屏操作列，使用 Auto 高度固定在右欄底部，兩個按鈕橫向並排。
///
/// 右欄的語言、錄音與底部操作均不得使用會把總高度撐出 body 的固定行高；
/// 可伸縮空間集中交給狀態區，使小高度窗口優先保住所有可操作控件。
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

	/// 語音頁根橫向版面。
	///
	/// 實現應設置三列：`1 Star / Auto 分隔線 / 2 Star`，並依次加入草稿區、分隔線與右側操作區。
	/// 禁止重新改成上下兩行，也禁止用原生 `Grid` 手動分配子控件列號；由 `GridStack` 自動放置三個子項。
	public GridStack Root = new(IsRow: false);

	/// 右側垂直操作區。
	///
	/// 實現應以 `Auto / Star / Auto / Auto / Auto` 行組織語言入口、狀態區、錄音按鈕、
	/// 能力提示與底部操作列。狀態區是唯一主要伸縮行，窗口變矮時先壓縮狀態留白，
	/// 不得裁掉錄音、清空或上屏按鈕。
	public GridStack ControlArea = new(IsRow: true);

	/// 右欄底部的橫向文本操作列。
	///
	/// 實現應使用等權重兩列放置清空與上屏；能力提示不夾在兩個按鈕之間，避免提示文字
	/// 把按鈕推到窗口外。兩個按鈕必須在窄右欄中仍保持完整邊界與可點擊區域。
	public GridStack DraftActionBar = new(IsRow: false);

	/// 顯示與編輯識別草稿的多行文本框。
	/// - 綁定 `DraftText`。
	/// - 填滿左欄的全部寬度與高度，不在其下方預留按鈕行或狀態行。
	/// - 支持換行與控件內部垂直滾動，長文本不得撐大整個語音頁。
	/// - `CanEditDraft=false` 時只讀，防止流式臨時結果覆蓋人工修改。
	/// - 完成或失敗後恢復編輯，讓用戶修正標點、專名與識別錯誤。
	public TextBox? DraftTextBox;

	/// 清空識別草稿的按鈕。
	/// 位於右欄最底部操作列左側，與上屏按鈕等寬。
	/// 綁定 `CanClearText`；忙碌或草稿為空時禁用，點擊後調用 `VmVoiceInput.ClearText()`。
	public Button? ClearTextButton;

	/// 將人工確認後的草稿提交到外部輸入目標的按鈕。
	/// 位於右欄最底部操作列右側，必須始終完整顯示，不得被窗口下沿裁切。
	/// 綁定 `CanCommitText`；Windows 原型因宿主不支持上屏而保持禁用。
	public Button? CommitTextButton;

	/// 上屏按鈕旁的能力提示。
	/// 位於底部操作列上方，獨佔右欄的一個 Auto 行並允許換行。
	/// Windows 不支持上屏時顯示簡短說明，避免用戶誤認為按鈕失效；Android 支持後隱藏並釋放該行高度。
	public TextBlock? CommitUnsupportedText;

	/// 顯示當前 BCP 47 語言並打開語言選擇覆蓋層的按鈕。
	/// 位於右欄頂部，寬度填滿右欄，避免只在頁面中央形成一個孤立膠囊。
	/// 綁定 `CanSelectLang`，正在錄音或等待尾包時禁用。
	public Button? LangButton;

	/// 語言按鈕的文字內容，綁定 `CurrentLangDisplay`。
	public TextBlock? LangText;

	/// 右側操作區中的圓形錄音按鈕。
	/// - Idle/Completed/Failed：黑色背景、白色麥克風，符合整頁深色模式。
	/// - Recording：`UiCfg.MainColor` 背景、黑色麥克風，點擊結束錄音並等待最終結果。
	/// - Finalizing：再次點擊取消等待與本輪會話。
	/// - Starting/Finalizing：保留忙碌視覺並禁止重複啟動。
	public Button? RecordButton;

	/// 錄音按鈕中央的麥克風圖形。
	/// 錄音按鈕必須清除 Fluent Button 的默認 Padding，並把水平/垂直內容對齊均設為 Center；
	/// Button 內容使用固定正方形 `Grid` 承載圖標，容器與圖標均雙向居中。麥克風底座與弧線的
	/// 墨量不對稱，即使 SVG 幾何邊界居中，視覺中心仍會略偏左；實現需以圖標寬度約 8.3% 的
	/// 固定水平右移校正可見輪廓。校正量按圖標尺寸比例計算，確保不同 DPI 下保持一致。
	/// 深色普通態使用白色圖標，主色錄音態使用黑色圖標，兩種狀態都保持足夠對比度。
	public Control? RecordIcon;

	/// 右欄狀態區的主要狀態提示，綁定 `StatusText`。
	/// 文字居中並允許換行；狀態區縮小時不得以最小高度把下方操作推出窗口。
	public TextBlock? StatusText;

	/// 最近一次語音操作錯誤的文字區。
	/// 與主要狀態共同放在右欄可伸縮狀態區，綁定 `ErrorText` 與 `HasError`。
	/// 錯誤不覆蓋已識別草稿；長錯誤允許換行或裁剪，但不得擠掉錄音與底部操作。
	public TextBlock? ErrorText;

	/// 覆蓋在語音頁上的語言選擇視圖。
	/// 打開時阻止下層文本與錄音控件接收點擊；選擇或取消後恢復主頁交互。
	public ViewVoiceInputLangSelector? LangSelector;

	/// 組裝左右兩欄、中央分隔線及覆蓋整個語音頁的語言選擇層。
	/// 主內容必須完全由橫向 `Root` 與垂直 `ControlArea` 兩級 `GridStack` 完成。
	private partial void Render();

	/// 建立填滿左欄的識別文本區。
	/// 返回內容只包含多行文本框及其邊框，不包含任何操作按鈕、語言或狀態控件。
	private partial Control MkDraftArea();

	/// 建立右側完整操作區。
	/// 使用 `ControlArea` 依次組裝語言入口、可伸縮狀態區、錄音按鈕、能力提示及底部操作列。
	private partial Control MkControlArea();

	/// 建立右欄底部文本操作列。
	/// 使用 `DraftActionBar` 的兩個等權重 Star 列，左側放清空、右側放上屏；本區不承載能力提示。
	private partial Control MkDraftActionBar();

	/// 建立當前語言入口。
	/// 語言選擇層只負責返回 `INormLang`，實際更新由 `VmVoiceInput.ApplyLang` 統一處理。
	private partial Control MkLangBar();

	/// 建立可伸縮的狀態與錯誤信息區。
	/// 本區佔用右欄唯一的 Star 行，負責吸收窗口高度變化，不包含錄音按鈕。
	private partial Control MkStatusArea();

	/// 建立圓形錄音按鈕區。
	/// 本區使用 Auto 高度，只保留按鈕需要的尺寸與小量內邊距，不再加入上下 Star 空白行。
	private partial Control MkRecordArea();

	/// 打開語言選擇覆蓋層。
	/// 正在識別時即使收到點擊也不得打開，實現需二次檢查 `CanSelectLang`。
	private partial void OpenLangSelector();

	/// 接收語言選擇結果、交給 VM 並關閉覆蓋層。
	private partial void OnLangSelected(INormLang Lang);

	/// 统一隐藏语言选择覆盖层，供“选择完成”和“取消返回”两条交互路径复用。
	private partial void CloseLangSelector();

	/// 按 VM 階段同步錄音按鈕的背景、圖標對比度與可點擊狀態。
	/// 視覺同步只讀取 VM 狀態，不在 View 中自行改變識別生命週期。
	private partial void SyncRecordingVisual();

	/// 點擊錄音按鈕後，空閒時開始；錄音中正常結束；等待最終結果時取消。
	/// 事件處理本身不直接開麥克風或建立網絡連接。
	private partial Task<nil> ToggleRecording(CT Ct);

	/// 點擊清空按鈕後調用 VM；View 不直接修改文本框內容。
	private partial void ClearText();

	/// 點擊上屏按鈕後調用 VM；View 不直接調用 `IKeyboardHost`。
	private partial void CommitText();

	/// 解除語言選擇事件與 VM 狀態訂閱，並取消仍在進行的識別會話。
	public partial void Dispose();
}
