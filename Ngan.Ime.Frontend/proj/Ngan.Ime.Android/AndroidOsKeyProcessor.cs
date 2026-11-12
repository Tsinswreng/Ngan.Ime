using Android.Views;
using Android.Views.InputMethods;
using Ngan.Ime.Core.Keys;
using Tsinswreng.CsCore;
using AKey = Android.Views.Keycode;

namespace Ngan.Ime.Android;


[Doc($$"""
把「引擎未處理的按鍵」送進 Android 的 `InputConnection`。

*三條路徑*（依宿主實際行為擇一）：
1. Shift ＋ 導航鍵 → 翻譯成真實的 Shift 按下／導航／Shift 擡起序列（某些宿主不認 metaState 的選區語義）；
2. 英數字符 → 直接 `CommitText`（部分遠端輸入框不支援 `sendKeyEvent` 注入字符，但接受文字提交）；
3. 其餘有 Keycode 的鍵 → `sendKeyEvent`；最後才退回文字提交。

*鍵名*：一律比對 {{nameof(KeyChars)}} 的實例（值語義，按名字相等），不再寫 `"Backspace"` 這類字面量；
改名時編譯器會抓到。

*AOT*：映射是靜態字典，無反射。

#Eg[
	```cs
	await Processor.OnKeyEvents(Unhandled, Ct);
	```
]
""")]
public class AndroidOsKeyProcessor : IOsKeyProcessor{

	[Doc("取當前輸入連接；null 表示沒有活動編輯框（什麼都不送）。")]
	readonly Func<IInputConnection?> GetInputConnection;

	[Doc("錯誤事件；本實現目前不主動觸發，保留供宿主統一掛接。")]
	public event ErrHandler? OnErr;

	[Doc("建立處理器。")]
	public AndroidOsKeyProcessor(Func<IInputConnection?> GetInputConnection){
		this.GetInputConnection = GetInputConnection;
	}

	[Doc("逐鍵依三條路徑送出。")]
	public Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> KeyEvents, CT Ct){
		var Ic = GetInputConnection();
		if(Ic is null){
			return Task.FromResult<IRespOnKeyEvent>(new RespOnKeyEvent());
		}
		foreach(var One in KeyEvents){
			Ct.ThrowIfCancellationRequested();
			SendOne(Ic, One);
		}
		return Task.FromResult<IRespOnKeyEvent>(new RespOnKeyEvent());
	}

	[Doc("送一顆鍵：依鍵與修飾鍵狀態挑路徑。")]
	void SendOne(IInputConnection Ic, IKeyEvent One){
		var Meta = ToMetaState(One.KeyBoardState);
		var EffectiveKey = ApplyShift(One.KeyChar, One.KeyBoardState);
		var AndroidKey = MapToKeycode(EffectiveKey);
		var IsDown = One.KeyState.IsKeyDown;

		if(AndroidKey is not null && IsShiftNavigation(One)){
			SendShiftNavigation(Ic, AndroidKey.Value, IsDown);
			return;
		}

		// 英數字符優先走文字提交：對遠端輸入框最可靠。
		if(IsDown && Meta == MetaKeyStates.None && IsPlainText(EffectiveKey)){
			var Text = KeyText(EffectiveKey);
			if(Text is not null){
				Ic.CommitText(Text, 1);
				return;
			}
		}

		if(AndroidKey is not null && (Meta != MetaKeyStates.None || ShouldSendAsKeyEvent(EffectiveKey))){
			var Action = IsDown ? KeyEventActions.Down : KeyEventActions.Up;
			Ic.SendKeyEvent(new global::Android.Views.KeyEvent(0, 0, Action, AndroidKey.Value, 0, Meta));
			return;
		}

		if(IsDown){
			var Text = KeyText(EffectiveKey);
			if(Text is not null){
				Ic.CommitText(Text, 1);
			}
		}
	}

	[Doc("由鍵盤狀態推出 Android 的 metaState。")]
	static MetaKeyStates ToMetaState(IKeyBoardState? Board){
		if(Board is null){
			return MetaKeyStates.None;
		}
		var Meta = MetaKeyStates.None;
		if(Board.IsKeyDown(KeyChars.Ctrl_L) || Board.IsKeyDown(KeyChars.Ctrl_R)){
			Meta |= MetaKeyStates.CtrlOn | MetaKeyStates.CtrlLeftOn;
		}
		if(Board.IsKeyDown(KeyChars.Shift_L) || Board.IsKeyDown(KeyChars.Shift_R)){
			Meta |= MetaKeyStates.ShiftOn;
		}
		if(Board.IsKeyDown(KeyChars.Alt_L) || Board.IsKeyDown(KeyChars.Alt_R)){
			Meta |= MetaKeyStates.AltOn;
		}
		if(Board.IsKeyDown(KeyChars.Meta_L)){
			Meta |= MetaKeyStates.MetaOn;
		}
		return Meta;
	}

	[Doc("Shift 按下時把可打印鍵換成上檔字符；其餘原樣。")]
	static IKeyChar ApplyShift(IKeyChar Key, IKeyBoardState? Board){
		if(Board is null || !(Board.IsKeyDown(KeyChars.Shift_L) || Board.IsKeyDown(KeyChars.Shift_R))){
			return Key;
		}
		return ShiftMap.GetValueOrDefault(Key, Key);
	}

	[Doc("是否為「Shift ＋ 導航鍵」：這類組合必須翻譯成真實的修飾鍵序列。")]
	static bool IsShiftNavigation(IKeyEvent One){
		var Board = One.KeyBoardState;
		if(Board is null || !(Board.IsKeyDown(KeyChars.Shift_L) || Board.IsKeyDown(KeyChars.Shift_R))){
			return false;
		}
		var Key = One.KeyChar;
		return Key.Equals(KeyChars.Home) || Key.Equals(KeyChars.End)
			|| Key.Equals(KeyChars.Left) || Key.Equals(KeyChars.Right)
			|| Key.Equals(KeyChars.Up) || Key.Equals(KeyChars.Down);
	}

	[Doc("送出 Shift 按下 → 導航鍵 → Shift 擡起。")]
	static void SendShiftNavigation(IInputConnection Ic, AKey NavigationKey, bool IsDown){
		if(IsDown){
			Ic.SendKeyEvent(new global::Android.Views.KeyEvent(0, 0, KeyEventActions.Down, AKey.ShiftLeft, 0));
			Ic.SendKeyEvent(new global::Android.Views.KeyEvent(0, 0, KeyEventActions.Down, NavigationKey, 0));
			return;
		}
		Ic.SendKeyEvent(new global::Android.Views.KeyEvent(0, 0, KeyEventActions.Up, NavigationKey, 0));
		Ic.SendKeyEvent(new global::Android.Views.KeyEvent(0, 0, KeyEventActions.Up, AKey.ShiftLeft, 0));
	}

	[Doc("是否為可直接提交的英數字符。")]
	static bool IsPlainText(IKeyChar Key){
		var Text = KeyText(Key);
		return Text is not null && Text.Length == 1 && char.IsAsciiLetterOrDigit(Text[0]);
	}

	[Doc("是否應該用 sendKeyEvent 而非文字提交（功能鍵與修飾鍵）。")]
	static bool ShouldSendAsKeyEvent(IKeyChar Key){
		return Key.Equals(KeyChars.Backspace) || Key.Equals(KeyChars.Enter) || Key.Equals(KeyChars.Tab)
			|| Key.Equals(KeyChars.Home) || Key.Equals(KeyChars.End)
			|| Key.Equals(KeyChars.Left) || Key.Equals(KeyChars.Right)
			|| Key.Equals(KeyChars.Up) || Key.Equals(KeyChars.Down)
			|| Key.Equals(KeyChars.Shift_L) || Key.Equals(KeyChars.Shift_R)
			|| Key.Equals(KeyChars.Ctrl_L) || Key.Equals(KeyChars.Ctrl_R)
			|| Key.Equals(KeyChars.Alt_L) || Key.Equals(KeyChars.Alt_R)
			|| Key.Equals(KeyChars.Meta_L);
	}

	[Doc("鍵 → Android Keycode；沒有對應鍵時回 null。")]
	static AKey? MapToKeycode(IKeyChar Key){
		return Keycodes.GetValueOrDefault(Key);
	}

	[Doc("鍵 → 要提交的文字；功能鍵回 null。")]
	static str? KeyText(IKeyChar Key){
		return Texts.GetValueOrDefault(Key);
	}

	[Doc("下檔 → 上檔字符映射。")]
	static readonly IReadOnlyDictionary<IKeyChar, IKeyChar> ShiftMap = new Dictionary<IKeyChar, IKeyChar>{
		[KeyChars.LowerA] = KeyChars.A, [KeyChars.LowerB] = KeyChars.B, [KeyChars.LowerC] = KeyChars.C,
		[KeyChars.LowerD] = KeyChars.D, [KeyChars.LowerE] = KeyChars.E, [KeyChars.LowerF] = KeyChars.F,
		[KeyChars.LowerG] = KeyChars.G, [KeyChars.LowerH] = KeyChars.H, [KeyChars.LowerI] = KeyChars.I,
		[KeyChars.LowerJ] = KeyChars.J, [KeyChars.LowerK] = KeyChars.K, [KeyChars.LowerL] = KeyChars.L,
		[KeyChars.LowerM] = KeyChars.M, [KeyChars.LowerN] = KeyChars.N, [KeyChars.LowerO] = KeyChars.O,
		[KeyChars.LowerP] = KeyChars.P, [KeyChars.LowerQ] = KeyChars.Q, [KeyChars.LowerR] = KeyChars.R,
		[KeyChars.LowerS] = KeyChars.S, [KeyChars.LowerT] = KeyChars.T, [KeyChars.LowerU] = KeyChars.U,
		[KeyChars.LowerV] = KeyChars.V, [KeyChars.LowerW] = KeyChars.W, [KeyChars.LowerX] = KeyChars.X,
		[KeyChars.LowerY] = KeyChars.Y, [KeyChars.LowerZ] = KeyChars.Z,
		[KeyChars.D1] = KeyChars.Exclamation, [KeyChars.D2] = KeyChars.At, [KeyChars.D3] = KeyChars.HashTag,
		[KeyChars.D4] = KeyChars.Dollar, [KeyChars.D5] = KeyChars.Percent, [KeyChars.D6] = KeyChars.Caret,
		[KeyChars.D7] = KeyChars.Ampersand, [KeyChars.D8] = KeyChars.Asterisk,
		[KeyChars.D9] = KeyChars.Paren_L, [KeyChars.D0] = KeyChars.Paren_R,
		[KeyChars.Minus] = KeyChars.Underscore, [KeyChars.Equal] = KeyChars.Plus,
		[KeyChars.SquareBracket_L] = KeyChars.Braces_L, [KeyChars.SquareBracket_R] = KeyChars.Braces_R,
		[KeyChars.Semicolon] = KeyChars.Colon, [KeyChars.Apostrophe] = KeyChars.Quote,
		[KeyChars.Comma] = KeyChars.Less, [KeyChars.Period] = KeyChars.Greater,
		[KeyChars.Slash] = KeyChars.Question, [KeyChars.Grave] = KeyChars.Tilde,
	};

	[Doc("鍵 → Android Keycode 映射表。")]
	static readonly IReadOnlyDictionary<IKeyChar, AKey> Keycodes = MkKeycodes();

	[Doc("建立 Keycode 表。")]
	static Dictionary<IKeyChar, AKey> MkKeycodes(){
		return new Dictionary<IKeyChar, AKey>{
			[KeyChars.A] = AKey.A, [KeyChars.B] = AKey.B, [KeyChars.C] = AKey.C, [KeyChars.D] = AKey.D,
			[KeyChars.E] = AKey.E, [KeyChars.F] = AKey.F, [KeyChars.G] = AKey.G, [KeyChars.H] = AKey.H,
			[KeyChars.I] = AKey.I, [KeyChars.J] = AKey.J, [KeyChars.K] = AKey.K, [KeyChars.L] = AKey.L,
			[KeyChars.M] = AKey.M, [KeyChars.N] = AKey.N, [KeyChars.O] = AKey.O, [KeyChars.P] = AKey.P,
			[KeyChars.Q] = AKey.Q, [KeyChars.R] = AKey.R, [KeyChars.S] = AKey.S, [KeyChars.T] = AKey.T,
			[KeyChars.U] = AKey.U, [KeyChars.V] = AKey.V, [KeyChars.W] = AKey.W, [KeyChars.X] = AKey.X,
			[KeyChars.Y] = AKey.Y, [KeyChars.Z] = AKey.Z,
			[KeyChars.LowerA] = AKey.A, [KeyChars.LowerB] = AKey.B, [KeyChars.LowerC] = AKey.C,
			[KeyChars.LowerD] = AKey.D, [KeyChars.LowerE] = AKey.E, [KeyChars.LowerF] = AKey.F,
			[KeyChars.LowerG] = AKey.G, [KeyChars.LowerH] = AKey.H, [KeyChars.LowerI] = AKey.I,
			[KeyChars.LowerJ] = AKey.J, [KeyChars.LowerK] = AKey.K, [KeyChars.LowerL] = AKey.L,
			[KeyChars.LowerM] = AKey.M, [KeyChars.LowerN] = AKey.N, [KeyChars.LowerO] = AKey.O,
			[KeyChars.LowerP] = AKey.P, [KeyChars.LowerQ] = AKey.Q, [KeyChars.LowerR] = AKey.R,
			[KeyChars.LowerS] = AKey.S, [KeyChars.LowerT] = AKey.T, [KeyChars.LowerU] = AKey.U,
			[KeyChars.LowerV] = AKey.V, [KeyChars.LowerW] = AKey.W, [KeyChars.LowerX] = AKey.X,
			[KeyChars.LowerY] = AKey.Y, [KeyChars.LowerZ] = AKey.Z,
			[KeyChars.D0] = AKey.Num0, [KeyChars.D1] = AKey.Num1, [KeyChars.D2] = AKey.Num2,
			[KeyChars.D3] = AKey.Num3, [KeyChars.D4] = AKey.Num4, [KeyChars.D5] = AKey.Num5,
			[KeyChars.D6] = AKey.Num6, [KeyChars.D7] = AKey.Num7, [KeyChars.D8] = AKey.Num8,
			[KeyChars.D9] = AKey.Num9,
			[KeyChars.Backspace] = AKey.Del, [KeyChars.Enter] = AKey.Enter, [KeyChars.Tab] = AKey.Tab,
			[KeyChars.Space] = AKey.Space,
			[KeyChars.Shift_L] = AKey.ShiftLeft, [KeyChars.Shift_R] = AKey.ShiftRight,
			[KeyChars.Ctrl_L] = AKey.CtrlLeft, [KeyChars.Ctrl_R] = AKey.CtrlRight,
			[KeyChars.Alt_L] = AKey.AltLeft, [KeyChars.Alt_R] = AKey.AltRight,
			[KeyChars.Meta_L] = AKey.MetaLeft,
			[KeyChars.Home] = AKey.MoveHome, [KeyChars.End] = AKey.MoveEnd,
			[KeyChars.Left] = AKey.DpadLeft, [KeyChars.Right] = AKey.DpadRight,
			[KeyChars.Up] = AKey.DpadUp, [KeyChars.Down] = AKey.DpadDown,
		};
	}

	[Doc("鍵 → 提交文字；只有可打印鍵有值。")]
	static readonly IReadOnlyDictionary<IKeyChar, str> Texts = MkTexts();

	[Doc("建立文字表：直接用鍵名（空格就是一個空格），功能鍵不列。")]
	static Dictionary<IKeyChar, str> MkTexts(){
		var Map = new Dictionary<IKeyChar, str>();
		foreach(var Key in KeyChars.All){
			var Name = Key.Name;
			if(Name.Length == 1){
				Map[Key] = Name;
			}
		}
		return Map;
	}
}
