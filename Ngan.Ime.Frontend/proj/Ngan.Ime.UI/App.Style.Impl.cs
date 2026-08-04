using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;

namespace Ngan.Ime.UI;

/// Ngan.Ime 全局控件樣式實現。
public partial class App{
	/// 統一 Button 的默認背景、邊框與鼠標懸停反饋。
	private partial void InitStyle(){
		// 普通按鈕使用穩定的深色背景；各頁以本地值指定的主色、白色等背景仍具有更高優先級。
		Styles.A(
			Sty.Is<Button>()
			.Set(
				X=>X.HorizontalAlignment
				,HAlign.Stretch
			).Set(
				X=>X.Background
				,new SolidColorBrush(Color.FromArgb(255, 32, 32, 32))
			).Set(
				X=>X.BorderThickness
				,new Thickness(1)
			)
		)
		// FluentTheme 的視覺背景實際畫在模板內 ContentPresenter 上；懸停規則因此必須命中模板子控件。
		.A(
			Sty.Is<Button>(X=>
				X.Class(PsdCls.pointerover)
				.Template().OfType<ContentPresenter>()
			).Set(
				X=>X.BorderBrush
				,UiCfg.Inst.MainColor
			).Set(
				X=>X.Background
				,new TemplateBinding{
					Property = TemplatedControl.BackgroundProperty,
				}
			)
		);

		// 默認讓邊框跟隨背景而隱藏；局部明確設置的 BorderBrush 使用更高優先級，不會被此規則覆蓋。
		TemplatedControl.BackgroundProperty.Changed.AddClassHandler<Button>((Btn, E)=>{
			Btn.SetValue(
				TemplatedControl.BorderBrushProperty
				,E.NewValue as IBrush
				,BindingPriority.Style
			);
		});
	}
}
