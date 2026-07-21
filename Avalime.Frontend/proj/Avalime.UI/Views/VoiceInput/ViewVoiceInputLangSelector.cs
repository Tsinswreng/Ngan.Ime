using Avalime.Core.Language;
using Avalime.UI.Infra;
using Avalonia.Controls;
using Tsinswreng.Avln.Grid;

namespace Avalime.UI.Views.VoiceInput;
using Ctx = VmVoiceInputLangSelector;

/// 語音輸入使用的標準語言選擇視圖。
/// 視覺結構參考 CsNgaq NormLang 選擇頁，保留搜索列與可滾動語言列表；本輪不接搜索或選中狀態。
public partial class ViewVoiceInputLangSelector : AppViewBase<Ctx>{
	/// 建立語言選擇視圖並組裝純前端控件樹。
	public partial ViewVoiceInputLangSelector();

	/// 語言選擇頁根垂直版面。
	public GridStack Root = new(IsRow: true);

	/// 輸入語言名稱或 BCP 47 代碼的搜索框。
	public TextBox? SearchInput;

	/// 清空搜索文字的按鈕。
	public Button? ClearSearchButton;

	/// 執行語言搜索的按鈕。
	public Button? SearchButton;

	/// 可滾動語言列表的外層容器。
	public ScrollViewer? LangScroll;

	/// 承載語言項目的垂直列表。
	public StackPanel? LangList;

	/// 用戶選擇語言時通知語音輸入頁。
	public event Action<NormLangDetail>? OnSelected;

	/// 組裝搜索列與語言列表。
	partial void Render();

	/// 建立搜索、清空與搜索按鈕所在的頂部工具列。
	private partial Control MkSearchBar();

	/// 建立可滾動的語言列表區。
	private partial Control MkLangList();

	/// 建立單個語言項目。
	/// 項目展示 BCP 47 代碼、本地名稱與英文名稱。
	private partial Control MkLangItem(NormLangDetail Lang);

	/// 將指定語言回傳給語音輸入頁。
	partial void SelectLang(NormLangDetail Lang);
}
