using Avalime.Core.Language;
using Avalime.UI.Infra;
using Avalonia.Controls;
using Tsinswreng.Avln.Grid;
using System.ComponentModel;

namespace Avalime.UI.Views.VoiceInput;
using Ctx = VmVoiceInputLangSelector;

/// 語音識別語言選擇覆蓋層的 View 聲明與交互設計。
///
/// 視覺結構參考 CsNgaq NormLang 選擇頁，但針對輸入法高度做精簡：
/// - 頂部為關閉、搜索輸入、清空、搜索操作。
/// - 剩餘區域為可滾動語言列表。
/// - 每項顯示 BCP 47 代碼、本地名稱與英文名稱。
/// - 無匹配結果時顯示明確提示。
///
/// 此 View 只回傳選擇或取消事件，不直接修改主語音頁的語言狀態。
public partial class ViewVoiceInputLangSelector : AppViewBase<Ctx>, IDisposable{
	/// 监听搜索结果集合变化并重建语言项；视图释放时必须解除订阅。
	PropertyChangedEventHandler? _ctxPropertyChangedHandler;

	/// 建立語言選擇覆蓋層並組裝控件樹。
	public partial ViewVoiceInputLangSelector();

	/// 語言選擇頁根垂直版面。
	public GridStack Root = new(IsRow: true);

	/// 不改變語言並關閉覆蓋層的按鈕。
	/// 輸入法空間有限，不依賴系統返回鍵作為唯一退出方式。
	public Button? CloseButton;

	/// 輸入語言名稱或 BCP 47 代碼的搜索框。
	/// Enter 鍵觸發 `VmVoiceInputLangSelector.Search()`。
	public TextBox? SearchInput;

	/// 清空搜索文字並恢復全部支持語言的按鈕。
	public Button? ClearSearchButton;

	/// 執行搜索的按鈕。
	public Button? SearchButton;

	/// 可滾動語言列表的外層容器。
	public ScrollViewer? LangScroll;

	/// 承載動態語言項目的垂直列表。
	/// 搜索结果变化后由 `RebuildLangItems` 重建，静态控件不保留已被过滤的语言项。
	public StackPanel? LangList;

	/// 搜索沒有匹配語言時顯示的提示文字。
	public TextBlock? EmptyText;

	/// 用戶選擇語言時通知語音輸入頁。
	public event Action<INormLang>? OnSelected;

	/// 用戶不改變語言並要求關閉覆蓋層時通知語音輸入頁。
	public event Action? OnCloseRequested;

	/// 組裝頂部工具列、語言列表與空結果提示。
	private partial void Render();

	/// 建立關閉、搜索輸入、清空與搜索按鈕所在的頂部工具列。
	private partial Control MkSearchBar();

	/// 建立可滾動語言列表區與空結果提示的疊層。
	private partial Control MkLangList();

	/// 按 ViewModel 當前 `Rows` 重建語言項目控件。
	/// 重建後同步空結果提示顯隱，不自行執行搜索邏輯。
	private partial void RebuildLangItems();

	/// 建立單個語言項目。
	/// 代碼使用主色突出，本地名稱為主要文字，英文名稱為次要灰色文字。
	private partial Control MkLangItem(INormLang Lang);

	/// 將指定語言回傳給語音輸入頁。
	private partial void SelectLang(INormLang Lang);

	/// 不改變語言並請求關閉覆蓋層。
	private partial void Close();

	/// 解除對語言選擇 ViewModel 的屬性變更訂閱。
	///
	/// 語言選擇覆蓋層可能隨輸入法視圖樹重建；若不解除訂閱，舊視圖仍會在搜索結果變化時
	/// 收到回調並持有其動態控件樹。主語音頁釋放時必須調用本方法，使覆蓋層生命週期
	/// 與父視圖保持一致。重複調用應保持安全，不應再次改變 ViewModel 狀態。
	public partial void Dispose();
}
