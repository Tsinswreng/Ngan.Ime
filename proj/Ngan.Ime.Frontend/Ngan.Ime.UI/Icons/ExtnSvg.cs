namespace Ngan.Ime.UI.Icons;

public static class ExtnSvg
{
	extension(Svg z){
		public Icon ToIcon(){
			return Icon.FromSvg(z);
		}
	}

	extension(Icon z){
		public Icon ToIcon(){
			return z;
		}
	}
}
