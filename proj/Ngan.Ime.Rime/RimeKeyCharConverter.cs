using Ngan.Ime.Core.Keys;
using Rime.Api;
using Tsinswreng.CsCore;
using KS = Ngan.Ime.Core.Keys.KeyChars;

namespace Ngan.Ime.Rime;

[Doc($$"""
把 Core 的按鍵事件轉成 librime 的 `(keycode, mask)`。

*兩套鍵碼*：可打印鍵用其字符的 ASCII 碼（`a`→0x61、`!`→0x21），
功能鍵用 X11 keysym（`BackSpace`→0xFF08 等）——這是 librime 的約定，不是本專案的發明。
故功能鍵的常量集中在 {{nameof(RimeKeysym)}}，不要在映射表裡散寫魔法數字。

*Shift 的兩層表達*：{{nameof(KeyEvent)}} 只說「當時按著哪些鍵」，
故本類要①把 mask 加上 `kShiftMask`；②把可打印鍵換成它的上檔字符（`a`→`A`、`1`→`!`），
否則 librime 會收到「shift+a」而按 `a` 查表。
導航鍵等非打印鍵保持原鍵，只靠 mask 表達組合語義。

*未知鍵*：宿主可能送來未登記的鍵名（例如未來的功能鍵）；此時退回「單字符 ASCII」，
仍不行就回 keycode 0（librime 視為無效鍵）並記一次 Warn——不拋例外，一顆未知鍵不該中斷整批。

*AOT*：映射表是靜態初始化好的字典，不含反射；`KeyChar` 的值語義保證按名字命中。

#Eg[
	```cs
	RimeKey Key = RimeKeyCharConverter.Inst.Convert(Event);
	rime.process_key(Session, Key.Code, Key.Mask);
	```
]
""")]
public class RimeKeyCharConverter {

	[Doc("全域單例；本類無狀態，映射表在靜態建構時建好。")]
	public static RimeKeyCharConverter Inst => field ??= new RimeKeyCharConverter();

	[Doc("轉換結果：librime 的鍵碼與修飾鍵掩碼。")]
	public readonly struct RimeKey{
		[Doc("librime 鍵碼：可打印鍵用 ASCII，功能鍵用 X11 keysym。")]
		public i32 Code{get;init;}

		[Doc("修飾鍵掩碼；按下與擡起由 `kReleaseMask` 區分。")]
		public i32 Mask{get;init;}

		[Doc("建立一個轉換結果。")]
		public RimeKey(i32 Code, i32 Mask){
			this.Code = Code;
			this.Mask = Mask;
		}
	}

	[Doc("把一個按鍵事件轉成 librime 的鍵碼與掩碼。")]
	public RimeKey Convert(IKeyEvent KeyEvent){
		var Mask = KeyEvent.KeyState.IsKeyDown ? 0 : RimeModifier.kReleaseMask;
		Mask |= GetModifierMask(KeyEvent.KeyBoardState);
		var EffectiveKey = ApplyShiftToKeyChar(KeyEvent.KeyChar, KeyEvent.KeyBoardState);
		if(!KeyCodes.TryGetValue(EffectiveKey, out var Code)){
			Code = FallbackCode(EffectiveKey);
		}
		return new RimeKey(Code, Mask);
	}

	[Doc("未知鍵的兜底：單字符 ASCII 用其碼，其餘回 0（librime 視為無效鍵）並節流記 Warn。")]
	static i32 FallbackCode(IKeyChar KeyChar){
		var Name = KeyChar.Name;
		if(Name.Length == 1 && Name[0] < 0x80){
			return Name[0];
		}
		WarnUnknownKey(Name);
		return 0;
	}

	[Doc("未知鍵告警的節流時間戳（毫秒）；避免長按未知鍵時刷屏。")]
	static long LastUnknownWarnTick;

	[Doc("未知鍵名只記一次 Warn（一秒內合併）。")]
	static void WarnUnknownKey(str Name){
		var Now = Environment.TickCount64;
		if(Now - LastUnknownWarnTick < 1000){
			return;
		}
		LastUnknownWarnTick = Now;
		AppLog.Warn($"[Rime] 未登記的按鍵名 `{Name}`，已當作無效鍵送出");
	}

	[Doc("由鍵盤狀態推出 librime 的修飾鍵掩碼；null 表示沒有修飾鍵。")]
	static i32 GetModifierMask(IKeyBoardState? KeyBoardState){
		if(KeyBoardState is null){
			return 0;
		}
		i32 Mask = 0;
		if(KeyBoardState.IsKeyDown(KS.Ctrl_L) || KeyBoardState.IsKeyDown(KS.Ctrl_R)){
			Mask |= RimeModifier.kControlMask;
		}
		if(KeyBoardState.IsKeyDown(KS.Shift_L) || KeyBoardState.IsKeyDown(KS.Shift_R)){
			Mask |= RimeModifier.kShiftMask;
		}
		if(KeyBoardState.IsKeyDown(KS.Alt_L) || KeyBoardState.IsKeyDown(KS.Alt_R)){
			Mask |= RimeModifier.kAltMask;
		}
		if(KeyBoardState.IsKeyDown(KS.Meta_L)){
			Mask |= RimeModifier.kSuperMask;
		}
		return Mask;
	}

	[Doc("Shift 按下時，把可打印鍵換成它的上檔字符；不在映射表裡的鍵原樣返回。")]
	static IKeyChar ApplyShiftToKeyChar(IKeyChar KeyChar, IKeyBoardState? KeyBoardState){
		if(KeyBoardState is null){
			return KeyChar;
		}
		if(!(KeyBoardState.IsKeyDown(KS.Shift_L) || KeyBoardState.IsKeyDown(KS.Shift_R))){
			return KeyChar;
		}
		return ShiftKeyMap.GetValueOrDefault(KeyChar, KeyChar);
	}

	[Doc("下檔鍵 → 上檔鍵。只列鍵盤上確實有上檔的鍵；字母大小寫在 ASCII 裡本來就不同碼。")]
	static readonly IReadOnlyDictionary<IKeyChar, IKeyChar> ShiftKeyMap = new Dictionary<IKeyChar, IKeyChar>{
		{KS.LowerA, KS.A}, {KS.LowerB, KS.B}, {KS.LowerC, KS.C}, {KS.LowerD, KS.D}, {KS.LowerE, KS.E},
		{KS.LowerF, KS.F}, {KS.LowerG, KS.G}, {KS.LowerH, KS.H}, {KS.LowerI, KS.I}, {KS.LowerJ, KS.J},
		{KS.LowerK, KS.K}, {KS.LowerL, KS.L}, {KS.LowerM, KS.M}, {KS.LowerN, KS.N}, {KS.LowerO, KS.O},
		{KS.LowerP, KS.P}, {KS.LowerQ, KS.Q}, {KS.LowerR, KS.R}, {KS.LowerS, KS.S}, {KS.LowerT, KS.T},
		{KS.LowerU, KS.U}, {KS.LowerV, KS.V}, {KS.LowerW, KS.W}, {KS.LowerX, KS.X}, {KS.LowerY, KS.Y},
		{KS.LowerZ, KS.Z},
		{KS.D1, KS.Exclamation}, {KS.D2, KS.At}, {KS.D3, KS.HashTag}, {KS.D4, KS.Dollar},
		{KS.D5, KS.Percent}, {KS.D6, KS.Caret}, {KS.D7, KS.Ampersand}, {KS.D8, KS.Asterisk},
		{KS.D9, KS.Paren_L}, {KS.D0, KS.Paren_R},
		{KS.Minus, KS.Underscore}, {KS.Equal, KS.Plus},
		{KS.SquareBracket_L, KS.Braces_L}, {KS.SquareBracket_R, KS.Braces_R},
		{KS.Semicolon, KS.Colon}, {KS.Apostrophe, KS.Quote},
		{KS.Comma, KS.Less}, {KS.Period, KS.Greater}, {KS.Slash, KS.Question}, {KS.Grave, KS.Tilde},
	};

	[Doc("鍵 → librime 鍵碼。可打印鍵用 ASCII，功能鍵用 X11 keysym（見 RimeKeysym）。")]
	static readonly IReadOnlyDictionary<IKeyChar, i32> KeyCodes = MkkKeyCodes();

	[Doc("建鍵碼表；分成「字符鍵」與「功能鍵」兩段，便於對照鍵盤與 keysym。")]
	static Dictionary<IKeyChar, i32> MkkKeyCodes(){
		var Map = new Dictionary<IKeyChar, i32>{
			// ===== 字母與數字：ASCII =====
			[KS.A] = 'A', [KS.B] = 'B', [KS.C] = 'C', [KS.D] = 'D', [KS.E] = 'E', [KS.F] = 'F',
			[KS.G] = 'G', [KS.H] = 'H', [KS.I] = 'I', [KS.J] = 'J', [KS.K] = 'K', [KS.L] = 'L',
			[KS.M] = 'M', [KS.N] = 'N', [KS.O] = 'O', [KS.P] = 'P', [KS.Q] = 'Q', [KS.R] = 'R',
			[KS.S] = 'S', [KS.T] = 'T', [KS.U] = 'U', [KS.V] = 'V', [KS.W] = 'W', [KS.X] = 'X',
			[KS.Y] = 'Y', [KS.Z] = 'Z',
			[KS.LowerA] = 'a', [KS.LowerB] = 'b', [KS.LowerC] = 'c', [KS.LowerD] = 'd', [KS.LowerE] = 'e',
			[KS.LowerF] = 'f', [KS.LowerG] = 'g', [KS.LowerH] = 'h', [KS.LowerI] = 'i', [KS.LowerJ] = 'j',
			[KS.LowerK] = 'k', [KS.LowerL] = 'l', [KS.LowerM] = 'm', [KS.LowerN] = 'n', [KS.LowerO] = 'o',
			[KS.LowerP] = 'p', [KS.LowerQ] = 'q', [KS.LowerR] = 'r', [KS.LowerS] = 's', [KS.LowerT] = 't',
			[KS.LowerU] = 'u', [KS.LowerV] = 'v', [KS.LowerW] = 'w', [KS.LowerX] = 'x', [KS.LowerY] = 'y',
			[KS.LowerZ] = 'z',
			[KS.D0] = '0', [KS.D1] = '1', [KS.D2] = '2', [KS.D3] = '3', [KS.D4] = '4',
			[KS.D5] = '5', [KS.D6] = '6', [KS.D7] = '7', [KS.D8] = '8', [KS.D9] = '9',

			// ===== 可打印符號：ASCII =====
			[KS.Space] = ' ',
			[KS.Grave] = '`', [KS.Tilde] = '~',
			[KS.Exclamation] = '!', [KS.At] = '@', [KS.HashTag] = '#', [KS.Dollar] = '$',
			[KS.Percent] = '%', [KS.Caret] = '^', [KS.Ampersand] = '&', [KS.Asterisk] = '*',
			[KS.Paren_L] = '(', [KS.Paren_R] = ')',
			[KS.Minus] = '-', [KS.Underscore] = '_', [KS.Equal] = '=', [KS.Plus] = '+',
			[KS.SquareBracket_L] = '[', [KS.Braces_L] = '{', [KS.SquareBracket_R] = ']', [KS.Braces_R] = '}',
			[KS.BackSlash] = '\\', [KS.Pipe] = '|',
			[KS.Semicolon] = ';', [KS.Colon] = ':', [KS.Apostrophe] = '\'', [KS.Quote] = '"',
			[KS.Comma] = ',', [KS.Less] = '<', [KS.Period] = '.', [KS.Greater] = '>',
			[KS.Slash] = '/', [KS.Question] = '?',

			// ===== 功能鍵：X11 keysym =====
			[KS.Backspace] = RimeKeysym.BackSpace,
			[KS.Escape] = RimeKeysym.Escape,
			[KS.PageUp] = RimeKeysym.Prior,
			[KS.PageDown] = RimeKeysym.Next,
			[KS.Delete] = RimeKeysym.Delete,
			[KS.Insert] = RimeKeysym.Insert,
			[KS.Tab] = RimeKeysym.Tab,
			[KS.Enter] = RimeKeysym.Return,
			[KS.CapsLock] = RimeKeysym.Caps_Lock,
			[KS.Shift_L] = RimeKeysym.Shift_L,
			[KS.Shift_R] = RimeKeysym.Shift_R,
			[KS.Ctrl_L] = RimeKeysym.Control_L,
			[KS.Ctrl_R] = RimeKeysym.Control_R,
			[KS.Alt_L] = RimeKeysym.Alt_L,
			[KS.Alt_R] = RimeKeysym.Alt_R,
			[KS.Meta_L] = RimeKeysym.Super_L,
			[KS.Fn] = RimeKeysym.Menu,
			[KS.Home] = RimeKeysym.Home,
			[KS.End] = RimeKeysym.End,
			[KS.Up] = RimeKeysym.Up,
			[KS.Down] = RimeKeysym.Down,
			[KS.Left] = RimeKeysym.Left,
			[KS.Right] = RimeKeysym.Right,
			[KS.F1] = RimeKeysym.F1,
			[KS.F2] = RimeKeysym.F2,
			[KS.F3] = RimeKeysym.F3,
			[KS.F4] = RimeKeysym.F4,
			[KS.F5] = RimeKeysym.F5,
			[KS.F6] = RimeKeysym.F6,
			[KS.F7] = RimeKeysym.F7,
			[KS.F8] = RimeKeysym.F8,
			[KS.F9] = RimeKeysym.F9,
			[KS.F10] = RimeKeysym.F10,
			[KS.F11] = RimeKeysym.F11,
			[KS.F12] = RimeKeysym.F12,
		};
		return Map;
	}
}

[Doc($$"""
librime 使用 X11 keysym 表示功能鍵。這些數字是 X11 的既定值，不是本專案自訂；
集中在此以便對照 keysym 表，避免映射表裡出現裸數字。

#Eg[
	```cs
	i32 EnterCode = RimeKeysym.Return; // 0xFF0D
	```
]
""")]
public static class RimeKeysym {
	[Doc("退格 0xFF08。")]
	public const i32 BackSpace = 0xFF08;
	[Doc("Tab 0xFF09。")]
	public const i32 Tab = 0xFF09;
	[Doc("回車 0xFF0D。")]
	public const i32 Return = 0xFF0D;
	[Doc("Escape 0xFF1B。")]
	public const i32 Escape = 0xFF1B;
	[Doc("Home 0xFF50。")]
	public const i32 Home = 0xFF50;
	[Doc("左 0xFF51。")]
	public const i32 Left = 0xFF51;
	[Doc("上 0xFF52。")]
	public const i32 Up = 0xFF52;
	[Doc("右 0xFF53。")]
	public const i32 Right = 0xFF53;
	[Doc("下 0xFF54。")]
	public const i32 Down = 0xFF54;
	[Doc("End 0xFF57。")]
	public const i32 End = 0xFF57;
	[Doc("Page Up 0xFF55（X11 名為 Prior）。")]
	public const i32 Prior = 0xFF55;
	[Doc("Page Down 0xFF56（X11 名為 Next）。")]
	public const i32 Next = 0xFF56;
	[Doc("Delete 0xFFFF。")]
	public const i32 Delete = 0xFFFF;
	[Doc("Insert 0xFF63。")]
	public const i32 Insert = 0xFF63;
	[Doc("左 Shift 0xFFE1。")]
	public const i32 Shift_L = 0xFFE1;
	[Doc("右 Shift 0xFFE2。")]
	public const i32 Shift_R = 0xFFE2;
	[Doc("左 Control 0xFFE3。")]
	public const i32 Control_L = 0xFFE3;
	[Doc("右 Control 0xFFE4。")]
	public const i32 Control_R = 0xFFE4;
	[Doc("Caps Lock 0xFFE5。")]
	public const i32 Caps_Lock = 0xFFE5;
	[Doc("左 Alt 0xFFE9。")]
	public const i32 Alt_L = 0xFFE9;
	[Doc("右 Alt 0xFFEA。")]
	public const i32 Alt_R = 0xFFEA;
	[Doc("左 Super（Windows 鍵）0xFFEB。")]
	public const i32 Super_L = 0xFFEB;
	[Doc("Menu／Fn 0xFF67。")]
	public const i32 Menu = 0xFF67;
	[Doc("F1 0xFFBE（X11 的 F 鍵從 0xFFBE 起連續排列）。")]
	public const i32 F1 = 0xFFBE;
	[Doc("F2 0xFFBF。")]
	public const i32 F2 = 0xFFBF;
	[Doc("F3 0xFFC0。")]
	public const i32 F3 = 0xFFC0;
	[Doc("F4 0xFFC1。")]
	public const i32 F4 = 0xFFC1;
	[Doc("F5 0xFFC2。")]
	public const i32 F5 = 0xFFC2;
	[Doc("F6 0xFFC3。")]
	public const i32 F6 = 0xFFC3;
	[Doc("F7 0xFFC4。")]
	public const i32 F7 = 0xFFC4;
	[Doc("F8 0xFFC5。")]
	public const i32 F8 = 0xFFC5;
	[Doc("F9 0xFFC6。")]
	public const i32 F9 = 0xFFC6;
	[Doc("F10 0xFFC7。")]
	public const i32 F10 = 0xFFC7;
	[Doc("F11 0xFFC8。")]
	public const i32 F11 = 0xFFC8;
	[Doc("F12 0xFFC9。")]
	public const i32 F12 = 0xFFC9;
}
