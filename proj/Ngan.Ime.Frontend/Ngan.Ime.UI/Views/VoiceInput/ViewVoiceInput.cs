namespace Ngan.Ime.UI.Views.VoiceInput;

using Avalonia.Controls;
using Ngan.Ime.Core.Language;
using Ngan.Ime.UI.Infra;

[Doc($$"""
語音輸入頁：左邊是草稿，右邊是語言、狀態與錄音操作，語言選擇是覆蓋層。

*佈局*：`Grid` 三欄（草稿 `*`、分隔線 `Auto`、操作區 `2*`）；操作區內部用
`Auto,*,Auto,Auto,Auto` 五行，唯一的 `*` 行留給狀態文字吸收高度變化——
窗口變矮時先壓狀態區，而不是把按鈕擠出畫面。

*為何錄音視覺走綁定*：按鈕底色、邊框、圖標填色、可用性全部由 `Phase` 派生
（見 `VmVoiceInput` 的 `RecordButtonBackground` 等），View 不再手動同步。

#Eg[
	```cs
	var Page = new ViewVoiceInput();
	```
]
""")]
public partial class ViewVoiceInput : AppViewBase<VmVoiceInput>, IDisposable{

	[Doc("草稿文本框。")]
	public TextBox? _DraftTextBox;

	[Doc("錄音按鈕。")]
	public Button? _RecordButton;

	[Doc("麥克風圖標。")]
	public Avalonia.Controls.Shapes.Shape? _RecordIcon;

	[Doc("語言選擇覆蓋層。")]
	public ViewVoiceInputLangSelector? _LangSelector;

	[Doc("當前語言按鈕。")]
	public Button? _LangButton;

	[Doc("狀態文字。")]
	public TextBlock? _StatusText;

	[Doc("錯誤文字。")]
	public TextBlock? _ErrorText;

	[Doc("清空按鈕。")]
	public Button? _ClearButton;

	[Doc("上屏按鈕。")]
	public Button? _CommitButton;

	[Doc("建立控件樹。")]
	protected override partial object Build(VmVoiceInput vm);

	[Doc("釋放子覆蓋層與 ViewModel。")]
	public partial void Dispose();
}
