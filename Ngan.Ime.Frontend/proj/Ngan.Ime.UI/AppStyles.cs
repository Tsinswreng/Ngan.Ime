namespace Ngan.Ime.UI;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Tsinswreng.CsCore;

[Doc($$"""
全域控件樣式（純 C#）。

*為何集中在一處*：按鈕的底色、邊框、圓角在每個 View 各寫一遍就會漂移；
集中在這裡之後，換主題只改一份。顏色取自 {{nameof(UiCfg)}}，不在樣式裡寫死。

*為何不用 FluentTheme 的默認按鈕外觀*：默認外觀的懸停底色會蓋掉輸入法的深色背景，
故改成「深色底 ＋ 邊框色跟隨背景」；懸停只把邊框換成主色。

#Eg[
	```cs
	Styles.Add(new FluentTheme());
	foreach(var One in AppStyles.Build()){ Styles.Add(One); }
	```
]
""")]
public static class AppStyles{

	[Doc("按鈕的深色底；與舊版一致。")]
	public static readonly IBrush ButtonBg = SolidColorBrush.Parse("#202020");

	[Doc("建立全部全域樣式；由 `App.Initialize` 逐條加入。")]
	public static IEnumerable<IStyle> Build(){
		// 1: 按鈕本體：深色底、無圓角、邊框顏色跟背景一致（視覺上沒有邊框）。
		yield return new Style(x => x.OfType<Button>()){
			Setters = {
				new Setter(TemplatedControl.BackgroundProperty, ButtonBg),
				new Setter(TemplatedControl.BorderBrushProperty, ButtonBg),
				new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
				new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
				new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
				new Setter(Layoutable.HorizontalAlignmentProperty, HAlign.Stretch),
				new Setter(Layoutable.VerticalAlignmentProperty, VAlign.Stretch),
			},
		};
		// 2: 懸停：只亮邊框，不動內容區底色。
		yield return new Style(x => x.OfType<Button>().Class(":pointerover")){
			Setters = {
				new Setter(TemplatedControl.BorderBrushProperty, UiCfg.Inst.MainColor),
				new Setter(TemplatedControl.BackgroundProperty, ButtonBg),
			},
		};
		// 3: 按下：邊框維持主色，避免閃回默認外觀。
		yield return new Style(x => x.OfType<Button>().Class(":pressed")){
			Setters = {
				new Setter(TemplatedControl.BorderBrushProperty, UiCfg.Inst.MainColor),
				new Setter(TemplatedControl.BackgroundProperty, ButtonBg),
			},
		};
	}
}
