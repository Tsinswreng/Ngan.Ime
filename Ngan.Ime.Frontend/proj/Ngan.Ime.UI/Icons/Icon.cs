namespace Ngan.Ime.UI.Icons;

using Avalonia.Media;
using SPath = Avalonia.Controls.Shapes.Path;

public class Icon : SPath
{
	public static Icon FromSvg(Svg Svg){
		var R = new Icon();
		R.Data = Geometry.Parse(Svg);
		R.Height = R.Width = UiCfg.Inst.BaseFontSize;
		R.HAlign(x=>x.Center);
		R.VAlign(x=>x.Center);
		R.Fill = Brushes.White;
		R.Stretch = Stretch.Uniform;
		return R;
	}

	public static Icon Invert(Svg Svg){
		var R = FromSvg(Svg);
		R.RenderTransform = new ScaleTransform{ScaleY = -1};
		return R;
	}

	public static implicit operator Icon(str s){
		Svg svg = s;
		return svg.ToIcon();
	}
}
