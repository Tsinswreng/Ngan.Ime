namespace Ngan.Ime.Core.Keys;

using Tsinswreng.CsCore;

using IK = Ngan.Ime.Core.Keys.IKeyChar;

[Doc($$"""
全專案的按鍵登記表。

*這是唯一的鍵名出處*。任何地方要引用一個鍵，一律用這裡的靜態字段，
不得再寫 `"Backspace"` 這類字符串字面量——跨進程（TSF 管道）與跨層（Android／Windows／Rime）
都靠 {{nameof(IKeyChar.Name)}} 對齊，字面量寫錯不會編譯錯，只會在執行期靜默失效。

*命名*：字母鍵分 `A`～`Z`（上檔／大寫）與 `LowerA`～`LowerZ`（下檔／小寫）；
其餘鍵按鍵帽上的符號或功能命名。`nameof(KeyChars.X)` 產生的就是鍵名字符串。

*AOT*：{{nameof(ByName)}} 是手寫登記表的投影，不靠反射列舉靜態成員，
故 NativeAOT 裁剪後仍完整（TSF 宿主端就靠它把管道裡的名字還原成鍵）。

#Eg[
	```cs
	Ctx.InputSafely([KeyEvent.Mk(KeyChars.LowerA, KeyStates.Down)]);
	IKeyChar? Enter = KeyChars.ByName.GetValueOrDefault("Enter");
	```
]
""")]
public static class KeyChars {

	#region 數字與其 shift 上檔
	[Doc("波浪號 `~`（`1` 的上檔）。")]
	public static readonly IK Tilde = KeyChar.Mk("~");
	[Doc("重音符 ``` `` （`1` 的下檔）。")]
	public static readonly IK Grave = KeyChar.Mk("`");

	[Doc("驚嘆號 `!`（`1` 的上檔）。")]
	public static readonly IK Exclamation = KeyChar.Mk("!");
	[Doc("數字 1。")]
	public static readonly IK D1 = KeyChar.Mk("1");

	[Doc("小老鼠 `@`（`2` 的上檔）。")]
	public static readonly IK At = KeyChar.Mk("@");
	[Doc("數字 2。")]
	public static readonly IK D2 = KeyChar.Mk("2");

	[Doc("井號 `#`（`3` 的上檔）。")]
	public static readonly IK HashTag = KeyChar.Mk("#");
	[Doc("數字 3。")]
	public static readonly IK D3 = KeyChar.Mk("3");

	[Doc("錢號 `$`（`4` 的上檔）。")]
	public static readonly IK Dollar = KeyChar.Mk("$");
	[Doc("數字 4。")]
	public static readonly IK D4 = KeyChar.Mk("4");

	[Doc("百分號 `%`（`5` 的上檔）。")]
	public static readonly IK Percent = KeyChar.Mk("%");
	[Doc("數字 5。")]
	public static readonly IK D5 = KeyChar.Mk("5");

	[Doc("脫字符 `^`（`6` 的上檔）。")]
	public static readonly IK Caret = KeyChar.Mk("^");
	[Doc("數字 6。")]
	public static readonly IK D6 = KeyChar.Mk("6");

	[Doc("與號 `&`（`7` 的上檔）。")]
	public static readonly IK Ampersand = KeyChar.Mk("&");
	[Doc("數字 7。")]
	public static readonly IK D7 = KeyChar.Mk("7");

	[Doc("星號 `*`（`8` 的上檔）。")]
	public static readonly IK Asterisk = KeyChar.Mk("*");
	[Doc("數字 8。")]
	public static readonly IK D8 = KeyChar.Mk("8");

	[Doc("左圓括號 `(`（`9` 的上檔）。")]
	public static readonly IK Paren_L = KeyChar.Mk("(");
	[Doc("數字 9。")]
	public static readonly IK D9 = KeyChar.Mk("9");

	[Doc("右圓括號 `)`（`0` 的上檔）。")]
	public static readonly IK Paren_R = KeyChar.Mk(")");
	[Doc("數字 0。")]
	public static readonly IK D0 = KeyChar.Mk("0");

	[Doc("下劃線 `_`（`-` 的上檔）。")]
	public static readonly IK Underscore = KeyChar.Mk("_");
	[Doc("減號 `-`。")]
	public static readonly IK Minus = KeyChar.Mk("-");

	[Doc("加號 `+`（`=` 的上檔）。")]
	public static readonly IK Plus = KeyChar.Mk("+");
	[Doc("等號 `=`。")]
	public static readonly IK Equal = KeyChar.Mk("=");
	#endregion

	#region 編輯與控制鍵
	[Doc("退格。")]
	public static readonly IK Backspace = KeyChar.Mk(nameof(Backspace));
	[Doc("製表符。鍵名用 `Tab` 而不是 `\\t`：它是功能鍵，不是可打印字符。")]
	public static readonly IK Tab = KeyChar.Mk(nameof(Tab));
	[Doc("回車。")]
	public static readonly IK Enter = KeyChar.Mk(nameof(Enter));
	[Doc("大寫鎖定。")]
	public static readonly IK CapsLock = KeyChar.Mk(nameof(CapsLock));
	[Doc("空格。鍵名就是一個空格字符，故選詞、上屏的文本比對才直觀。")]
	public static readonly IK Space = KeyChar.Mk(" ");
	#endregion

	#region 字母上檔（大寫）
	[Doc("字母 A（上檔）。鍵名 `A`。")]
	public static readonly IK A = KeyChar.Mk(nameof(A));
	[Doc("字母 B（上檔）。鍵名 `B`。")]
	public static readonly IK B = KeyChar.Mk(nameof(B));
	[Doc("字母 C（上檔）。鍵名 `C`。")]
	public static readonly IK C = KeyChar.Mk(nameof(C));
	[Doc("字母 D（上檔）。鍵名 `D`。")]
	public static readonly IK D = KeyChar.Mk(nameof(D));
	[Doc("字母 E（上檔）。鍵名 `E`。")]
	public static readonly IK E = KeyChar.Mk(nameof(E));
	[Doc("字母 F（上檔）。鍵名 `F`。")]
	public static readonly IK F = KeyChar.Mk(nameof(F));
	[Doc("字母 G（上檔）。鍵名 `G`。")]
	public static readonly IK G = KeyChar.Mk(nameof(G));
	[Doc("字母 H（上檔）。鍵名 `H`。")]
	public static readonly IK H = KeyChar.Mk(nameof(H));
	[Doc("字母 I（上檔）。鍵名 `I`。")]
	public static readonly IK I = KeyChar.Mk(nameof(I));
	[Doc("字母 J（上檔）。鍵名 `J`。")]
	public static readonly IK J = KeyChar.Mk(nameof(J));
	[Doc("字母 K（上檔）。鍵名 `K`。")]
	public static readonly IK K = KeyChar.Mk(nameof(K));
	[Doc("字母 L（上檔）。鍵名 `L`。")]
	public static readonly IK L = KeyChar.Mk(nameof(L));
	[Doc("字母 M（上檔）。鍵名 `M`。")]
	public static readonly IK M = KeyChar.Mk(nameof(M));
	[Doc("字母 N（上檔）。鍵名 `N`。")]
	public static readonly IK N = KeyChar.Mk(nameof(N));
	[Doc("字母 O（上檔）。鍵名 `O`。")]
	public static readonly IK O = KeyChar.Mk(nameof(O));
	[Doc("字母 P（上檔）。鍵名 `P`。")]
	public static readonly IK P = KeyChar.Mk(nameof(P));
	[Doc("字母 Q（上檔）。鍵名 `Q`。")]
	public static readonly IK Q = KeyChar.Mk(nameof(Q));
	[Doc("字母 R（上檔）。鍵名 `R`。")]
	public static readonly IK R = KeyChar.Mk(nameof(R));
	[Doc("字母 S（上檔）。鍵名 `S`。")]
	public static readonly IK S = KeyChar.Mk(nameof(S));
	[Doc("字母 T（上檔）。鍵名 `T`。")]
	public static readonly IK T = KeyChar.Mk(nameof(T));
	[Doc("字母 U（上檔）。鍵名 `U`。")]
	public static readonly IK U = KeyChar.Mk(nameof(U));
	[Doc("字母 V（上檔）。鍵名 `V`。")]
	public static readonly IK V = KeyChar.Mk(nameof(V));
	[Doc("字母 W（上檔）。鍵名 `W`。")]
	public static readonly IK W = KeyChar.Mk(nameof(W));
	[Doc("字母 X（上檔）。鍵名 `X`。")]
	public static readonly IK X = KeyChar.Mk(nameof(X));
	[Doc("字母 Y（上檔）。鍵名 `Y`。")]
	public static readonly IK Y = KeyChar.Mk(nameof(Y));
	[Doc("字母 Z（上檔）。鍵名 `Z`。")]
	public static readonly IK Z = KeyChar.Mk(nameof(Z));
	#endregion

	#region 字母下檔（小寫）
	[Doc("字母 a（下檔）。鍵名 `a`。")]
	public static readonly IK LowerA = KeyChar.Mk("a");
	[Doc("字母 b（下檔）。鍵名 `b`。")]
	public static readonly IK LowerB = KeyChar.Mk("b");
	[Doc("字母 c（下檔）。鍵名 `c`。")]
	public static readonly IK LowerC = KeyChar.Mk("c");
	[Doc("字母 d（下檔）。鍵名 `d`。")]
	public static readonly IK LowerD = KeyChar.Mk("d");
	[Doc("字母 e（下檔）。鍵名 `e`。")]
	public static readonly IK LowerE = KeyChar.Mk("e");
	[Doc("字母 f（下檔）。鍵名 `f`。")]
	public static readonly IK LowerF = KeyChar.Mk("f");
	[Doc("字母 g（下檔）。鍵名 `g`。")]
	public static readonly IK LowerG = KeyChar.Mk("g");
	[Doc("字母 h（下檔）。鍵名 `h`。")]
	public static readonly IK LowerH = KeyChar.Mk("h");
	[Doc("字母 i（下檔）。鍵名 `i`。")]
	public static readonly IK LowerI = KeyChar.Mk("i");
	[Doc("字母 j（下檔）。鍵名 `j`。")]
	public static readonly IK LowerJ = KeyChar.Mk("j");
	[Doc("字母 k（下檔）。鍵名 `k`。")]
	public static readonly IK LowerK = KeyChar.Mk("k");
	[Doc("字母 l（下檔）。鍵名 `l`。")]
	public static readonly IK LowerL = KeyChar.Mk("l");
	[Doc("字母 m（下檔）。鍵名 `m`。")]
	public static readonly IK LowerM = KeyChar.Mk("m");
	[Doc("字母 n（下檔）。鍵名 `n`。")]
	public static readonly IK LowerN = KeyChar.Mk("n");
	[Doc("字母 o（下檔）。鍵名 `o`。")]
	public static readonly IK LowerO = KeyChar.Mk("o");
	[Doc("字母 p（下檔）。鍵名 `p`。")]
	public static readonly IK LowerP = KeyChar.Mk("p");
	[Doc("字母 q（下檔）。鍵名 `q`。")]
	public static readonly IK LowerQ = KeyChar.Mk("q");
	[Doc("字母 r（下檔）。鍵名 `r`。")]
	public static readonly IK LowerR = KeyChar.Mk("r");
	[Doc("字母 s（下檔）。鍵名 `s`。")]
	public static readonly IK LowerS = KeyChar.Mk("s");
	[Doc("字母 t（下檔）。鍵名 `t`。")]
	public static readonly IK LowerT = KeyChar.Mk("t");
	[Doc("字母 u（下檔）。鍵名 `u`。")]
	public static readonly IK LowerU = KeyChar.Mk("u");
	[Doc("字母 v（下檔）。鍵名 `v`。")]
	public static readonly IK LowerV = KeyChar.Mk("v");
	[Doc("字母 w（下檔）。鍵名 `w`。")]
	public static readonly IK LowerW = KeyChar.Mk("w");
	[Doc("字母 x（下檔）。鍵名 `x`。")]
	public static readonly IK LowerX = KeyChar.Mk("x");
	[Doc("字母 y（下檔）。鍵名 `y`。")]
	public static readonly IK LowerY = KeyChar.Mk("y");
	[Doc("字母 z（下檔）。鍵名 `z`。")]
	public static readonly IK LowerZ = KeyChar.Mk("z");
	#endregion

	#region 標點與括號
	[Doc("左大括號 `{`（`[` 的上檔）。")]
	public static readonly IK Braces_L = KeyChar.Mk("{");
	[Doc("左方括號 `[`。")]
	public static readonly IK SquareBracket_L = KeyChar.Mk("[");
	[Doc("右大括號 `}`（`]` 的上檔）。")]
	public static readonly IK Braces_R = KeyChar.Mk("}");
	[Doc("右方括號 `]`。")]
	public static readonly IK SquareBracket_R = KeyChar.Mk("]");
	[Doc("反斜線 `\\`。")]
	public static readonly IK BackSlash = KeyChar.Mk("\\");
	[Doc("豎線 `|`（`\\` 的上檔）。")]
	public static readonly IK Pipe = KeyChar.Mk("|");
	[Doc("冒號 `:`（`;` 的上檔）。")]
	public static readonly IK Colon = KeyChar.Mk(":");
	[Doc("分號 `;`。")]
	public static readonly IK Semicolon = KeyChar.Mk(";");
	[Doc("單引號 `'`。")]
	public static readonly IK Apostrophe = KeyChar.Mk("'");
	[Doc("雙引號 `\"`（`'` 的上檔）。")]
	public static readonly IK Quote = KeyChar.Mk("\"");
	[Doc("小於 `<`（`,` 的上檔）。")]
	public static readonly IK Less = KeyChar.Mk("<");
	[Doc("逗號 `,`。")]
	public static readonly IK Comma = KeyChar.Mk(",");
	[Doc("大於 `>`（`.` 的上檔）。")]
	public static readonly IK Greater = KeyChar.Mk(">");
	[Doc("句點 `.`。")]
	public static readonly IK Period = KeyChar.Mk(".");
	[Doc("問號 `?`（`/` 的上檔）。")]
	public static readonly IK Question = KeyChar.Mk("?");
	[Doc("斜線 `/`。")]
	public static readonly IK Slash = KeyChar.Mk("/");
	#endregion

	#region 修飾鍵
	[Doc("左 Shift。")]
	public static readonly IK Shift_L = KeyChar.Mk(nameof(Shift_L));
	[Doc("右 Shift。")]
	public static readonly IK Shift_R = KeyChar.Mk(nameof(Shift_R));
	[Doc("左 Control。")]
	public static readonly IK Ctrl_L = KeyChar.Mk(nameof(Ctrl_L));
	[Doc("右 Control。")]
	public static readonly IK Ctrl_R = KeyChar.Mk(nameof(Ctrl_R));
	[Doc("左 Alt。")]
	public static readonly IK Alt_L = KeyChar.Mk(nameof(Alt_L));
	[Doc("右 Alt。")]
	public static readonly IK Alt_R = KeyChar.Mk(nameof(Alt_R));
	[Doc("Meta／Windows 鍵（左右不分；目前鍵盤只有一個）。")]
	public static readonly IK Meta_L = KeyChar.Mk(nameof(Meta_L));
	[Doc("功能鍵（合成鍵盤上的 Fn）。")]
	public static readonly IK Fn = KeyChar.Mk(nameof(Fn));
	#endregion

	#region 功能鍵
	[Doc("F1。")]
	public static readonly IK F1 = KeyChar.Mk(nameof(F1));
	[Doc("F2。")]
	public static readonly IK F2 = KeyChar.Mk(nameof(F2));
	[Doc("F3。")]
	public static readonly IK F3 = KeyChar.Mk(nameof(F3));
	[Doc("F4；Rime 的默認方案選單熱鍵。")]
	public static readonly IK F4 = KeyChar.Mk(nameof(F4));
	[Doc("F5。")]
	public static readonly IK F5 = KeyChar.Mk(nameof(F5));
	[Doc("F6。")]
	public static readonly IK F6 = KeyChar.Mk(nameof(F6));
	[Doc("F7。")]
	public static readonly IK F7 = KeyChar.Mk(nameof(F7));
	[Doc("F8。")]
	public static readonly IK F8 = KeyChar.Mk(nameof(F8));
	[Doc("F9。")]
	public static readonly IK F9 = KeyChar.Mk(nameof(F9));
	[Doc("F10。")]
	public static readonly IK F10 = KeyChar.Mk(nameof(F10));
	[Doc("F11。")]
	public static readonly IK F11 = KeyChar.Mk(nameof(F11));
	[Doc("F12。")]
	public static readonly IK F12 = KeyChar.Mk(nameof(F12));
	#endregion

	#region 導航
	[Doc("行首。")]
	public static readonly IK Home = KeyChar.Mk(nameof(Home));
	[Doc("行尾。")]
	public static readonly IK End = KeyChar.Mk(nameof(End));
	[Doc("上。")]
	public static readonly IK Up = KeyChar.Mk(nameof(Up));
	[Doc("下。")]
	public static readonly IK Down = KeyChar.Mk(nameof(Down));
	[Doc("左。")]
	public static readonly IK Left = KeyChar.Mk(nameof(Left));
	[Doc("右。")]
	public static readonly IK Right = KeyChar.Mk(nameof(Right));
	[Doc("上一頁（候選翻頁）。")]
	public static readonly IK PageUp = KeyChar.Mk(nameof(PageUp));
	[Doc("下一頁（候選翻頁）。")]
	public static readonly IK PageDown = KeyChar.Mk(nameof(PageDown));
	[Doc("刪除鍵（向右刪）。")]
	public static readonly IK Delete = KeyChar.Mk(nameof(Delete));
	[Doc("插入鍵。")]
	public static readonly IK Insert = KeyChar.Mk(nameof(Insert));
	[Doc("跳脫鍵；輸入法用它取消組字。")]
	public static readonly IK Escape = KeyChar.Mk(nameof(Escape));
	#endregion

	[Doc($$"""
	全表：所有已登記的按鍵，依上述分組順序排列。
	{{nameof(ByName)}} 與測試的完整性檢查都以它為來源；新增鍵時只需在此表與上面的字段各加一項。
	""")]
	public static readonly IReadOnlyList<IK> All = [
		Tilde, Grave, Exclamation, D1, At, D2, HashTag, D3, Dollar, D4,
		Percent, D5, Caret, D6, Ampersand, D7, Asterisk, D8, Paren_L, D9,
		Paren_R, D0, Underscore, Minus, Plus, Equal,
		Backspace, Tab, Enter, CapsLock, Space,
		A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
		LowerA, LowerB, LowerC, LowerD, LowerE, LowerF, LowerG, LowerH, LowerI, LowerJ,
		LowerK, LowerL, LowerM, LowerN, LowerO, LowerP, LowerQ, LowerR, LowerS, LowerT,
		LowerU, LowerV, LowerW, LowerX, LowerY, LowerZ,
		Braces_L, SquareBracket_L, Braces_R, SquareBracket_R, BackSlash, Pipe,
		Colon, Semicolon, Apostrophe, Quote, Less, Comma, Greater, Period, Question, Slash,
		Shift_L, Shift_R, Ctrl_L, Ctrl_R, Alt_L, Alt_R, Meta_L, Fn,
		F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
		Home, End, Up, Down, Left, Right,
		PageUp, PageDown, Delete, Insert, Escape,
	];

	[Doc($$"""
	鍵名 → 鍵的索引，由 {{nameof(All)}} 建立。
	跨進程協議（TSF 管道）與各平臺映射表用它把名字還原成鍵，取代原本的反射列舉（AOT 下會被裁剪）。
	""")]
	public static readonly IReadOnlyDictionary<str, IK> ByName =
		All.ToDictionary(K => K.Name, K => K, StringComparer.Ordinal);

	[Doc($$"""
	按鍵名取鍵；取不到時回 null（協議端送來未知鍵名屬正常情況，不應拋）。
	#Prm[{{nameof(Name)}}][鍵名。]
	#Rtn[對應的鍵；未登記時為 null。]
	""")]
	public static IK? Find(str Name){
		return ByName.GetValueOrDefault(Name);
	}
}
