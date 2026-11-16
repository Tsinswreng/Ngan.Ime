namespace Ngan.Ime.UI.Views.VoiceInput;

using Avalonia.Controls;
using Ngan.Ime.Core.Language;
using Ngan.Ime.UI.Infra;

[Doc($$"""
語言選擇覆蓋層：頂部搜索列 ＋ 可滾動語言列表 ＋ 空結果提示。

*為何是覆蓋層而不是新頁面*：輸入法高度有限，換頁會讓使用者失去「我正在輸入」的上下文；
覆蓋層只遮住語音頁右欄，關閉即回原處。

*事件*：本視圖只發 {{nameof(OnSelected)}} 與 {{nameof(OnCloseRequested)}}，
不直接改父頁的語言狀態——選擇的套用由父視圖呼叫 VM 完成。

#Eg[
	```cs
	Sel.OnSelected += Lang => Vm.ApplyLang(Lang);
	```
]
""")]
public partial class ViewVoiceInputLangSelector : AppViewBase<VmVoiceInputLangSelector>, IDisposable{

	[Doc("關閉按鈕。")]
	public Button? _CloseButton;

	[Doc("搜索輸入框。")]
	public TextBox? _SearchInput;

	[Doc("清空搜索按鈕。")]
	public Button? _ClearSearchButton;

	[Doc("執行搜索按鈕。")]
	public Button? _SearchButton;

	[Doc("語言列表面板。")]
	public StackPanel? _LangList;

	[Doc("空結果提示。")]
	public TextBlock? _EmptyText;

	[Doc("用戶選定語言。")]
	public event Action<INormLang>? OnSelected;

	[Doc("用戶要求關閉。")]
	public event Action? OnCloseRequested;

	[Doc("建立控件樹。")]
	protected override partial object Build(VmVoiceInputLangSelector vm);

	[Doc("訂閱結果集合變更並先建一次列表。")]
	protected override partial void OnAfterInitialized();

	[Doc("重建語言項並同步空結果提示。")]
	partial void RebuildLangItems();

	[Doc("建立單個語言項。")]
	private partial Control MkLangItem(INormLang Lang);

	[Doc("釋放 ViewModel 與訂閱。")]
	public partial void Dispose();
}
