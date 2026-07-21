using Avalime.UI.Infra;
using Avalonia.Controls;
using Tsinswreng.Avln.Grid;

namespace Avalime.UI.Views.VoiceInput;
using Ctx = VmVoiceInput;

/// 語音輸入頁的 View 聲明。
/// 目標佈局由語言入口、中央圓形錄音按鈕與下方狀態文案組成。
public partial class ViewVoiceInput : AppViewBase<Ctx>
	, IDisposable
{
	/// 建立語音輸入頁並組裝純前端視圖。
	public partial ViewVoiceInput();

	/// 語音頁根垂直版面。
	/// 語言列位於頂部，錄音操作區佔據剩餘空間。
	public GridStack Root = new(IsRow: true);

	/// 顯示當前 BCP 47 語言並打開語言選擇區的按鈕。
	public Button? LangButton;

	/// 語言按鈕的文字內容，後續綁定 `CurrentLangDisplay`。
	public TextBlock? LangText;

	/// 語音頁中央的圓形錄音按鈕。
	/// 本輪只畫出白色非錄音外觀，不接點擊與錄音狀態。
	public Button? RecordButton;

	/// 錄音按鈕中央的麥克風圖形承載控件。
	public Control? RecordIcon;

	/// 錄音按鈕下方的操作提示。
	public TextBlock? StatusText;

	/// 覆蓋在語音頁上的語言選擇視圖。
	/// 本輪只保留控件樹位置，不實現顯隱狀態。
	public ViewVoiceInputLangSelector? LangSelector;

	/// 組裝語音輸入頁視圖樹。
	/// 包含頂部語言入口、中央錄音按鈕、狀態文案及語言選擇覆蓋層。
	partial void Render();

	/// 建立頂部當前語言入口。
	private partial Control MkLangBar();

	/// 建立中央錄音操作區。
	private partial Control MkRecordArea();

	/// 打開語言選擇區。
	/// 本輪僅保留前端事件入口，不實現顯隱狀態。
	partial void OpenLangSelector();

	/// 接收語言選擇結果並關閉選擇區。
	/// 本輪僅聲明 View 與 ViewModel 之間的接縫。
	partial void OnLangSelected(Avalime.Core.Language.NormLangDetail Lang);

	/// 同步錄音按鈕的視覺狀態。
	/// 非錄音態為白色，錄音態為 `UiCfg.MainColor`。
	partial void SyncRecordingVisual();

	public partial void Dispose();
}
