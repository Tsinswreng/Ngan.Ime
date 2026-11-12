namespace Ngan.Ime.UI.Views.Input;

using Avalonia.Controls;
using Avalonia.Media;
using Ngan.Ime.UI.Infra;

[Doc($$"""
{{nameof(ViewInput)}} 的實現：一行折行文字。
""")]
public partial class ViewInput{

	[Doc("組出「黑底 ＋ 折行文字」，高度與字級取自 UiCfg。")]
	protected override partial object Build(VmInput vm){
		var Font = UiCfg.Inst.FontFamily;
		return new Border()
			.Background(Brushes.Black)
			.Height(UiCfg.Inst.PreeditHeight)
			.With(B => B.Padding = new Avalonia.Thickness(0))
			.Child(new TextBlock()
				.With(T => {
					_Text = T;
					T.Foreground = Brushes.White;
					T.FontSize = UiCfg.Inst.PreeditFontSize;
					T.Margin = new Avalonia.Thickness(4, 1, 0, 1);
					T.VerticalAlignment = VAlign.Center;
					T.TextWrapping = TextWrapping.Wrap;
					if(Font is not null){
						T.FontFamily = Font;
					}
				})
				.Text(vm, X => X.Text));
	}

	[Doc("釋放 ViewModel。")]
	public partial void Dispose(){
		vm?.Dispose();
		GC.SuppressFinalize(this);
	}
}
