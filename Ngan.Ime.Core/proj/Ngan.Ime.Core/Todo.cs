namespace Ngan.Ime;

[Doc($$"""
界面文案的臨時入口。

*現狀*：本專案還沒有 i18n（沒有 `II18n`、沒有語言包），故 {{nameof(I18n)}} 只原樣返回字串。
它的價值在於**呼叫點先寫好**：等 i18n 落地時只改這一個函數，不必回頭掃全部硬編碼文案。

*約定*：所有面向使用者的硬編碼文案都經它，禁止在視圖或業務層直接寫中文字串。
""")]
public class Todo{
	[Doc($$"""
	取一段可顯示的文案。
	#Prm[{{nameof(S)}}][文案本體；現階段即最終輸出。]
	#Rtn[原樣返回 {{nameof(S)}}。]
	""")]
	public static str I18n(str S){
		return S;
	}
}
