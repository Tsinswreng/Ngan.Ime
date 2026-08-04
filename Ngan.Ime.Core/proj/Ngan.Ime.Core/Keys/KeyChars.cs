using static Ngan.Ime.Core.Keys.KeyChar;
using IK = Ngan.Ime.Core.Keys.IKeyChar;
namespace Ngan.Ime.Core.Keys;
using KS = KeyChars;


[Doc(@"按鍵枚舉。區分了大小寫與shift狀態下纔能輸出的符號 保留備用")]
public static class KeyChars{
	#region numbers
	public static IK Tilde{get;} = K("~");
	public static IK Grave{get;} = K("`");

	public static IK Exclamation{get;} = K("!");
	public static IK D1{get;} = K("1");

	public static IK At{get;} = K("@");
	public static IK D2{get;} = K("2");

	public static IK HashTag{get;} = K("#");
	public static IK D3{get;} = K("3");

	public static IK Dollar{get;} = K("$");
	public static IK D4{get;} = K("4");

	public static IK Percent{get;} = K("%");
	public static IK D5{get;} = K("5");

	public static IK Caret{get;} = K("^");
	public static IK D6{get;} = K("6");

	public static IK Ampersand{get;} = K("&");
	public static IK D7{get;} = K("7");

	public static IK Asterisk{get;} = K("*");
	public static IK D8{get;} = K("8");

	public static IK Paren_L{get;} = K("(");
	public static IK D9{get;} = K("9");

	public static IK Paren_R{get;} = K(")");
	public static IK D0{get;} = K("0");

	public static IK Underscore{get;} = K("_");
	public static IK Minus{get;} = K("-");

	public static IK Plus{get;} = K("+");
	public static IK Equal{get;} = K("=");
	public static IK Backspace{get;} = K(nameof(KS.Backspace));
	#endregion numbers

	#region qwer
	public static IK Tab{get;} = K("\t");
	public static IK Q{get;} = K(nameof(KS.Q));
	public static IK q{get;} = K(nameof(KS.q));
	public static IK W{get;} = K(nameof(KS.W));
	public static IK w{get;} = K(nameof(KS.w));
	public static IK E{get;} = K(nameof(KS.E));
	public static IK e{get;} = K(nameof(KS.e));
	public static IK R{get;} = K(nameof(KS.R));
	public static IK r{get;} = K(nameof(KS.r));
	public static IK T{get;} = K(nameof(KS.T));
	public static IK t{get;} = K(nameof(KS.t));
	public static IK Y{get;} = K(nameof(KS.Y));
	public static IK y{get;} = K(nameof(KS.y));
	public static IK U{get;} = K(nameof(KS.U));
	public static IK u{get;} = K(nameof(KS.u));
	public static IK I{get;} = K(nameof(KS.I));
	public static IK i{get;} = K(nameof(KS.i));
	public static IK O{get;} = K(nameof(KS.O));
	public static IK o{get;} = K(nameof(KS.o));
	public static IK P{get;} = K(nameof(KS.P));
	public static IK p{get;} = K(nameof(KS.p));
	public static IK Braces_L{get;} = K("{");
	public static IK SquareBracket_L{get;} = K("[");
	public static IK Braces_R{get;} = K("}");
	public static IK SquareBracket_R{get;} = K("]");
	public static IK BackSlash{get;} = K("\\");
	public static IK Pipe{get;} = K("|");
	#endregion qwer

	#region asdf
	public static IK CapsLock{get;} = K(nameof(KS.CapsLock));
	public static IK A{get;} = K(nameof(KS.A));
	public static IK a{get;} = K(nameof(KS.a));
	public static IK S{get;} = K(nameof(KS.S));
	public static IK s{get;} = K(nameof(KS.s));
	public static IK D{get;} = K(nameof(KS.D));
	public static IK d{get;} = K(nameof(KS.d));
	public static IK F{get;} = K(nameof(KS.F));
	public static IK f{get;} = K(nameof(KS.f));
	public static IK G{get;} = K(nameof(KS.G));
	public static IK g{get;} = K(nameof(KS.g));
	public static IK H{get;} = K(nameof(KS.H));
	public static IK h{get;} = K(nameof(KS.h));
	public static IK J{get;} = K(nameof(KS.J));
	public static IK j{get;} = K(nameof(KS.j));
	public static IK K{get;} = K(nameof(KS.K));
	public static IK k{get;} = K(nameof(KS.k));
	public static IK L{get;} = K(nameof(KS.L));
	public static IK l{get;} = K(nameof(KS.l));

	public static IK Colon{get;} = K(":");
	public static IK Semicolon{get;} = K(";");

	public static IK Apostrophe{get;} = K("'");
	public static IK Quote{get;} = K("\"");

	public static IK Enter{get;} = K(nameof(KS.Enter));

	#endregion asdf
	#region zxcv
	public static IK Shift_L{get;} = K(nameof(KS.Shift_L));

	public static IK Z{get;} = K(nameof(KS.Z));
	public static IK z{get;} = K(nameof(KS.z));

	public static IK X{get;} = K(nameof(KS.X));
	public static IK x{get;} = K(nameof(KS.x));

	public static IK C{get;} = K(nameof(KS.C));
	public static IK c{get;} = K(nameof(KS.c));

	public static IK V{get;} = K(nameof(KS.V));
	public static IK v{get;} = K(nameof(KS.v));

	public static IK B{get;} = K(nameof(KS.B));
	public static IK b{get;} = K(nameof(KS.b));

	public static IK N{get;} = K(nameof(KS.N));
	public static IK n{get;} = K(nameof(KS.n));

	public static IK M{get;} = K(nameof(KS.M));
	public static IK m{get;} = K(nameof(KS.m));

	public static IK Less{get;} = K("<");
	public static IK Comma{get;} = K(",");

	public static IK Greater{get;} = K(">");

	public static IK Period{get;} = K(".");

	public static IK Question{get;} = K("?");
	public static IK Slash{get;} = K("/");
	public static IK Shift_R{get;} = K(nameof(KS.Shift_R));

	#endregion zxcv

	#region Ctrl
	public static IK Ctrl_L{get;} = K(nameof(KS.Ctrl_L));
	public static IK Meta_L{get;} = K(nameof(KS.Meta_L));
	public static IK Alt_L{get;} = K(nameof(KS.Alt_L));
	public static IK Space{get;} = K(" ");
	public static IK Alt_R{get;} = K(nameof(KS.Alt_R));
	public static IK Fn{get;} = K(nameof(KS.Fn));
	public static IK Ctrl_R{get;} = K(nameof(KS.Ctrl_R));
	#endregion Ctrl

	#region Arrows
	/// <summary>
	/// 行首導航鍵，供虛擬鍵盤手勢與宿主鍵盤轉發共用。
	/// </summary>
	public static IK Home{get;} = K(nameof(KS.Home));
	/// <summary>
	/// 行尾導航鍵，供虛擬鍵盤手勢與宿主鍵盤轉發共用。
	/// </summary>
	public static IK End{get;} = K(nameof(KS.End));
	public static IK Up{get;} = K(nameof(KS.Up));
	public static IK Down{get;} = K(nameof(KS.Down));
	public static IK Left{get;} = K(nameof(KS.Left));
	public static IK Right{get;} = K(nameof(KS.Right));
	#endregion Arrows

}
