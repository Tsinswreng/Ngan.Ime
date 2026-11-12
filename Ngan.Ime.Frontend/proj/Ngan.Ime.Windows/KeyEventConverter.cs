namespace Ngan.Ime.Windows;

using Ngan.Ime.Core.Keys;
using Tsinswreng.CsCore;
using static Ngan.Ime.Core.Keys.KeyChars;

[Doc($$"""
把 Core 的按鍵事件轉成 Win32 的 `keybd_event` 參數。

*為何要有這張表*：Win32 的虛擬鍵碼（VK）與字符不是同一個體系——字母是 `0x41`（`A`）、
數字是 `0x30`（`0`）、標點是 `VK_OEM_*`、功能鍵各自有值。`KeyChars` 只保證「鍵名唯一」，
故必須有一份從鍵到 VK 的映射。

*舊版的兩個 bug*（本檔已修）：
1. 只收了 a-z 與 Home/End，其餘鍵直接 `KeyNotFoundException`；
2. 回傳的四元組順序與 `keybd_event(bVk, bScan, dwFlags, dwExtraInfo)` 不符，
   導致 `dwFlags` 恆為 0（只按下、從不擡起）。

*AOT*：映射是靜態字典，無反射。

#Eg[
	```cs
	WinKey Key = KeyEventConverter.Inst.Convert(Event);
	keybd_event(Key.Vk, Key.Scan, Key.Flags, Key.ExtraInfo);
	```
]
""")]
public class KeyEventConverter{

	[Doc("全域單例；本類無狀態。")]
	static readonly KeyEventConverter InstField = new();

	[Doc("取單例。")]
	public static KeyEventConverter Inst => InstField;

	[Doc("一次按鍵轉換的結果，欄位順序即 `keybd_event` 的參數順序。")]
	public readonly struct WinKey{
		[Doc("虛擬鍵碼。")]
		public byte Vk{get;init;}
		[Doc("掃描碼；通常為 0。")]
		public byte Scan{get;init;}
		[Doc("`dwFlags`：0=按下，2=擡起。")]
		public i32 Flags{get;init;}
		[Doc("附加資訊；通常為 0。")]
		public i32 ExtraInfo{get;init;}
	}

	[Doc("按下時的 `dwFlags`。")]
	public const i32 KeyDownFlags = 0;

	[Doc("擡起時的 `dwFlags`（`KEYEVENTF_KEYUP`）。")]
	public const i32 KeyUpFlags = 2;

	[Doc($$"""
	把一個按鍵事件轉成 `keybd_event` 參數；未知鍵回 VK=0（系統會忽略）並記一次 Warn。
	#Prm[{{nameof(KeyEvent)}}][Core 的按鍵事件。]
	#Rtn[可直接交給 `keybd_event` 的四個參數。]
	""")]
	public WinKey Convert(IKeyEvent KeyEvent){
		var Flags = KeyEvent.KeyState.IsKeyDown ? KeyDownFlags : KeyUpFlags;
		var Vk = VirtualKeys.GetValueOrDefault(KeyEvent.KeyChar, (byte)0);
		if(Vk == 0){
			Vk = FallbackVk(KeyEvent.KeyChar);
		}
		return new WinKey{ Vk = Vk, Scan = 0, Flags = Flags, ExtraInfo = 0 };
	}

	[Doc("未知鍵的兜底：單字符 ASCII 用其大寫碼；仍不行就 0 並節流記 Warn。")]
	static byte FallbackVk(IKeyChar KeyChar){
		var Name = KeyChar.Name;
		if(Name.Length == 1 && Name[0] < 0x80){
			return char.ToUpperInvariant(Name[0]) is var C && C < 0x80 ? (byte)C : (byte)0;
		}
		WarnUnknown(Name);
		return 0;
	}

	[Doc("未知鍵告警的節流時間戳（毫秒）。")]
	static long LastUnknownWarnTick;

	[Doc("未知鍵名一秒內只記一條 Warn。")]
	static void WarnUnknown(str Name){
		var Now = Environment.TickCount64;
		if(Now - LastUnknownWarnTick < 1000){
			return;
		}
		LastUnknownWarnTick = Now;
		AppLog.Warn($"[Windows] 未登記的按鍵名 `{Name}`，已當作無效鍵送出");
	}

	[Doc("鍵 → Win32 虛擬鍵碼；覆蓋 KeyChars 全表。")]
	static readonly IReadOnlyDictionary<IKeyChar, byte> VirtualKeys = MkVirtualKeys();

	[Doc("建立映射表；字母與數字用 ASCII 值，其餘用 VK 常量。")]
	static Dictionary<IKeyChar, byte> MkVirtualKeys(){
		return new Dictionary<IKeyChar, byte>{
			// ===== 字母：VK = 大寫 ASCII =====
			[A] = 0x41, [B] = 0x42, [C] = 0x43, [D] = 0x44, [E] = 0x45, [F] = 0x46, [G] = 0x47,
			[H] = 0x48, [I] = 0x49, [J] = 0x4A, [K] = 0x4B, [L] = 0x4C, [M] = 0x4D, [N] = 0x4E,
			[O] = 0x4F, [P] = 0x50, [Q] = 0x51, [R] = 0x52, [S] = 0x53, [T] = 0x54, [U] = 0x55,
			[V] = 0x56, [W] = 0x57, [X] = 0x58, [Y] = 0x59, [Z] = 0x5A,
			[LowerA] = 0x41, [LowerB] = 0x42, [LowerC] = 0x43, [LowerD] = 0x44, [LowerE] = 0x45,
			[LowerF] = 0x46, [LowerG] = 0x47, [LowerH] = 0x48, [LowerI] = 0x49, [LowerJ] = 0x4A,
			[LowerK] = 0x4B, [LowerL] = 0x4C, [LowerM] = 0x4D, [LowerN] = 0x4E, [LowerO] = 0x4F,
			[LowerP] = 0x50, [LowerQ] = 0x51, [LowerR] = 0x52, [LowerS] = 0x53, [LowerT] = 0x54,
			[LowerU] = 0x55, [LowerV] = 0x56, [LowerW] = 0x57, [LowerX] = 0x58, [LowerY] = 0x59,
			[LowerZ] = 0x5A,

			// ===== 數字與功能鍵 =====
			[D0] = 0x30, [D1] = 0x31, [D2] = 0x32, [D3] = 0x33, [D4] = 0x34,
			[D5] = 0x35, [D6] = 0x36, [D7] = 0x37, [D8] = 0x38, [D9] = 0x39,
			[Backspace] = 0x08, [Tab] = 0x09, [Enter] = 0x0D, [Space] = 0x20,
			[Shift_L] = 0xA0, [Shift_R] = 0xA1,
			[Ctrl_L] = 0xA2, [Ctrl_R] = 0xA3,
			[Alt_L] = 0xA4, [Alt_R] = 0xA5,
			[Meta_L] = 0x5B, [Fn] = 0xFF,
			[CapsLock] = 0x14,
			[Home] = 0x24, [End] = 0x23, [Up] = 0x26, [Down] = 0x28, [Left] = 0x25, [Right] = 0x27,

			// ===== 標點（OEM 鍵）=====
			[Semicolon] = 0xBA, [Colon] = 0xBA,
			[Equal] = 0xBB, [Plus] = 0xBB,
			[Comma] = 0xBC, [Less] = 0xBC,
			[Minus] = 0xBD, [Underscore] = 0xBD,
			[Period] = 0xBE, [Greater] = 0xBE,
			[Slash] = 0xBF, [Question] = 0xBF,
			[Grave] = 0xC0, [Tilde] = 0xC0,
			[SquareBracket_L] = 0xDB, [Braces_L] = 0xDB,
			[BackSlash] = 0xDC, [Pipe] = 0xDC,
			[SquareBracket_R] = 0xDD, [Braces_R] = 0xDD,
			[Apostrophe] = 0xDE, [Quote] = 0xDE,
		};
	}
}

[Doc($$"""
Win32 虛擬鍵碼常量。

*為何另立一個類*：VK 是作業系統的既定值，不是本專案自訂；集中在此便於對照微軟文檔，
也避免映射表裡出現裸數字。只列本專案用得到的鍵。

#Eg[
	```cs
	byte Enter = WinVk.Return; // 0x0D
	```
]
""")]
public static class WinVk{
	[Doc("退格 0x08。")]
	public const byte Back = 0x08;
	[Doc("Tab 0x09。")]
	public const byte Tab = 0x09;
	[Doc("回車 0x0D。")]
	public const byte Return = 0x0D;
	[Doc("空格 0x20。")]
	public const byte Space = 0x20;
}
