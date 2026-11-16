namespace Ngan.Ime.UI.Views.Preedit;

using Ngan.Ime.UI.Views.Input;

[Doc($$"""
預編輯列。

*為何是 {{nameof(ViewInput)}} 的子類*：兩者的行為完全一致（一行折行文字），
差別只在語義（主界面叫「預編輯」、語音頁叫「草稿」）。用子類表達語義、
不重寫一份相同的控件樹。
""")]
public class ViewPreedit : ViewInput{
}
