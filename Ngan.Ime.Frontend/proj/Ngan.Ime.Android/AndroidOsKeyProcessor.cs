using Android.Views.InputMethods;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.Core.Keys;

namespace Ngan.Ime.Android;

public class AndroidOsKeyProcessor : IOsKeyProcessor
{
	readonly Func<IInputConnection?> _getInputConnection;

	public AndroidOsKeyProcessor(Func<IInputConnection?> getInputConnection){
		_getInputConnection = getInputConnection;
	}

	public event ErrHandler? OnErr;

	public Task<IRespOnKeyEvent> OnKeyEvents(IEnumerable<IKeyEvent> keyEvents, CT Ct){
		var ic = _getInputConnection();
		if(ic is null) return Task.FromResult<IRespOnKeyEvent>(new RespOnKeyEvent());

		foreach(var keyEvent in keyEvents){
			var name = keyEvent.KeyChar.Name;
			var metaState = ToAndroidMetaState(keyEvent.KeyBoardState);
			AppLog.Debug($"[IME] OsKeyProcessor forwarding: {name} {(keyEvent.KeyState.IsKeyDown ? "Down" : "Up")}");
			var effectiveKey = ApplyShiftToKeyChar(keyEvent.KeyChar, keyEvent.KeyBoardState);
			var effectiveName = effectiveKey.Name;
			var androidKey = MapToAndroidKey(effectiveName);

			if(androidKey is not null && ShouldSendAsShiftNavigationSequence(keyEvent)){
				SendShiftNavigationSequence(ic, androidKey.Value, keyEvent.KeyState.IsKeyDown);
				continue;
			}


			// 直接上屏的字母和數字優先走 commitText。
			// UU遠程遠程控制輸入框不可靠支持 sendKeyEvent 的字符注入，
			// 但對 commitText 的文本提交語義支持更穩定。
			if(keyEvent.KeyState.IsKeyDown && ShouldCommitDirectText(effectiveName, metaState)){
				var text = KeyNameToText(effectiveName);
				if(text is not null){
					ic.CommitText(text, 1);
					continue;
				}
			}

			if(androidKey is not null && (metaState != global::Android.Views.MetaKeyStates.None || ShouldSendAsKeyEvent(effectiveName))){
				var action = keyEvent.KeyState.IsKeyDown
					? global::Android.Views.KeyEventActions.Down
					: global::Android.Views.KeyEventActions.Up;
				ic.SendKeyEvent(new global::Android.Views.KeyEvent(0, 0, action, androidKey.Value, 0, metaState));
			}else if(keyEvent.KeyState.IsKeyDown){
				var text = KeyNameToText(effectiveName);
				if(text is not null){
					ic.CommitText(text, 1);
				}
			}
		}
		return Task.FromResult<IRespOnKeyEvent>(new RespOnKeyEvent());
	}

	static bool ShouldCommitDirectText(string name, global::Android.Views.MetaKeyStates metaState){
		if(metaState != global::Android.Views.MetaKeyStates.None){
			return false;
		}

		return name.Length == 1 && char.IsAsciiLetterOrDigit(name[0]);
	}

	static global::Android.Views.MetaKeyStates ToAndroidMetaState(IKeyBoardState? keyBoardState){
		if(keyBoardState is null){
			return global::Android.Views.MetaKeyStates.None;
		}

		var metaState = global::Android.Views.MetaKeyStates.None;
		if(keyBoardState.IsKeyDown(KeyChars.Ctrl_L) || keyBoardState.IsKeyDown(KeyChars.Ctrl_R)){
			metaState |= global::Android.Views.MetaKeyStates.CtrlOn | global::Android.Views.MetaKeyStates.CtrlLeftOn;
		}
		if(keyBoardState.IsKeyDown(KeyChars.Shift_L) || keyBoardState.IsKeyDown(KeyChars.Shift_R)){
			metaState |= global::Android.Views.MetaKeyStates.ShiftOn;
		}
		if(keyBoardState.IsKeyDown(KeyChars.Alt_L) || keyBoardState.IsKeyDown(KeyChars.Alt_R)){
			metaState |= global::Android.Views.MetaKeyStates.AltOn;
		}
		if(keyBoardState.IsKeyDown(KeyChars.Meta_L)){
			metaState |= global::Android.Views.MetaKeyStates.MetaOn;
		}
		return metaState;
	}

	/// <summary>
	/// 與 RimeKeyCharConverter 保持同一套抽象：Shift 是否按下只來自 KeyBoardState.AllDownKeys。
	/// Android fallback 在需要輸出可打印鍵時，自行根據該狀態推導出對應的大寫/符號鍵值。
	/// </summary>
	static IKeyChar ApplyShiftToKeyChar(IKeyChar keyChar, IKeyBoardState? keyBoardState){
		if(keyBoardState is null){
			return keyChar;
		}
		if(!(keyBoardState.IsKeyDown(KeyChars.Shift_L) || keyBoardState.IsKeyDown(KeyChars.Shift_R))){
			return keyChar;
		}
		if(ShiftKeyMap.TryGetValue(keyChar, out var shifted)){
			return shifted;
		}
		return keyChar;
	}

	/// <summary>
	/// 某些 Android 宿主對「方向鍵/Home/End + metaState=Shift」不會產生選區語義，
	/// 必須翻譯成真實的修飾鍵事件序列。
	/// 上層抽象仍只傳遞 AllDownKeys；這裏是 Android 落地層把該抽象轉成宿主真正能吃的事件。
	/// </summary>
	static bool ShouldSendAsShiftNavigationSequence(IKeyEvent keyEvent){
		var keyBoardState = keyEvent.KeyBoardState;
		if(keyBoardState is null){
			return false;
		}
		if(!(keyBoardState.IsKeyDown(KeyChars.Shift_L) || keyBoardState.IsKeyDown(KeyChars.Shift_R))){
			return false;
		}
		return keyEvent.KeyChar.Name is "Home" or "End" or "Left" or "Right" or "Up" or "Down";
	}

	static void SendShiftNavigationSequence(IInputConnection inputConnection, global::Android.Views.Keycode navigationKey, bool isKeyDown){
		if(isKeyDown){
			inputConnection.SendKeyEvent(new global::Android.Views.KeyEvent(
				0, 0, global::Android.Views.KeyEventActions.Down, global::Android.Views.Keycode.ShiftLeft, 0
			));
			inputConnection.SendKeyEvent(new global::Android.Views.KeyEvent(
				0, 0, global::Android.Views.KeyEventActions.Down, navigationKey, 0
			));
			return;
		}

		inputConnection.SendKeyEvent(new global::Android.Views.KeyEvent(
			0, 0, global::Android.Views.KeyEventActions.Up, navigationKey, 0
		));
		inputConnection.SendKeyEvent(new global::Android.Views.KeyEvent(
			0, 0, global::Android.Views.KeyEventActions.Up, global::Android.Views.Keycode.ShiftLeft, 0
		));
	}

	static readonly IReadOnlyDictionary<IKeyChar, IKeyChar> ShiftKeyMap = new Dictionary<IKeyChar, IKeyChar>{
		{KeyChars.a, KeyChars.A}, {KeyChars.b, KeyChars.B}, {KeyChars.c, KeyChars.C}, {KeyChars.d, KeyChars.D}, {KeyChars.e, KeyChars.E}, {KeyChars.f, KeyChars.F}, {KeyChars.g, KeyChars.G}, {KeyChars.h, KeyChars.H}, {KeyChars.i, KeyChars.I}, {KeyChars.j, KeyChars.J}, {KeyChars.k, KeyChars.K}, {KeyChars.l, KeyChars.L}, {KeyChars.m, KeyChars.M},
		{KeyChars.n, KeyChars.N}, {KeyChars.o, KeyChars.O}, {KeyChars.p, KeyChars.P}, {KeyChars.q, KeyChars.Q}, {KeyChars.r, KeyChars.R}, {KeyChars.s, KeyChars.S}, {KeyChars.t, KeyChars.T}, {KeyChars.u, KeyChars.U}, {KeyChars.v, KeyChars.V}, {KeyChars.w, KeyChars.W}, {KeyChars.x, KeyChars.X}, {KeyChars.y, KeyChars.Y}, {KeyChars.z, KeyChars.Z},
		{KeyChars.D1, KeyChars.Exclamation}, {KeyChars.D2, KeyChars.At}, {KeyChars.D3, KeyChars.HashTag}, {KeyChars.D4, KeyChars.Dollar}, {KeyChars.D5, KeyChars.Percent}, {KeyChars.D6, KeyChars.Caret}, {KeyChars.D7, KeyChars.Ampersand}, {KeyChars.D8, KeyChars.Asterisk}, {KeyChars.D9, KeyChars.Paren_L}, {KeyChars.D0, KeyChars.Paren_R},
		{KeyChars.Minus, KeyChars.Underscore}, {KeyChars.Equal, KeyChars.Plus},
		{KeyChars.SquareBracket_L, KeyChars.Braces_L}, {KeyChars.SquareBracket_R, KeyChars.Braces_R},
		{KeyChars.Semicolon, KeyChars.Colon}, {KeyChars.Apostrophe, KeyChars.Quote},
		{KeyChars.Comma, KeyChars.Less}, {KeyChars.Period, KeyChars.Greater}, {KeyChars.Slash, KeyChars.Question}, {KeyChars.Grave, KeyChars.Tilde},
	};

	static bool ShouldSendAsKeyEvent(string name){
		return name is "Backspace" or "Enter" or "Tab" or "Home" or "End" or "Left" or "Right" or "Up" or "Down"
			or "Shift_L" or "Shift_R" or "Ctrl_L" or "Ctrl_R" or "Alt_L" or "Alt_R" or "Meta_L"
			|| name.Length == 1 && char.IsAsciiLetterOrDigit(name[0]);
	}

	// 將 IKeyChar.Name 映射到 Android Keycode
	static global::Android.Views.Keycode? MapToAndroidKey(string name) => name switch {
		"A" or "a" => global::Android.Views.Keycode.A,
		"B" or "b" => global::Android.Views.Keycode.B,
		"C" or "c" => global::Android.Views.Keycode.C,
		"D" or "d" => global::Android.Views.Keycode.D,
		"E" or "e" => global::Android.Views.Keycode.E,
		"F" or "f" => global::Android.Views.Keycode.F,
		"G" or "g" => global::Android.Views.Keycode.G,
		"H" or "h" => global::Android.Views.Keycode.H,
		"I" or "i" => global::Android.Views.Keycode.I,
		"J" or "j" => global::Android.Views.Keycode.J,
		"K" or "k" => global::Android.Views.Keycode.K,
		"L" or "l" => global::Android.Views.Keycode.L,
		"M" or "m" => global::Android.Views.Keycode.M,
		"N" or "n" => global::Android.Views.Keycode.N,
		"O" or "o" => global::Android.Views.Keycode.O,
		"P" or "p" => global::Android.Views.Keycode.P,
		"Q" or "q" => global::Android.Views.Keycode.Q,
		"R" or "r" => global::Android.Views.Keycode.R,
		"S" or "s" => global::Android.Views.Keycode.S,
		"T" or "t" => global::Android.Views.Keycode.T,
		"U" or "u" => global::Android.Views.Keycode.U,
		"V" or "v" => global::Android.Views.Keycode.V,
		"W" or "w" => global::Android.Views.Keycode.W,
		"X" or "x" => global::Android.Views.Keycode.X,
		"Y" or "y" => global::Android.Views.Keycode.Y,
		"Z" or "z" => global::Android.Views.Keycode.Z,
		"0" => global::Android.Views.Keycode.Num0,
		"1" => global::Android.Views.Keycode.Num1,
		"2" => global::Android.Views.Keycode.Num2,
		"3" => global::Android.Views.Keycode.Num3,
		"4" => global::Android.Views.Keycode.Num4,
		"5" => global::Android.Views.Keycode.Num5,
		"6" => global::Android.Views.Keycode.Num6,
		"7" => global::Android.Views.Keycode.Num7,
		"8" => global::Android.Views.Keycode.Num8,
		"9" => global::Android.Views.Keycode.Num9,
		"Backspace" => global::Android.Views.Keycode.Del,
		"Enter" => global::Android.Views.Keycode.Enter,
		"Tab" => global::Android.Views.Keycode.Tab,
		"Shift_L" => global::Android.Views.Keycode.ShiftLeft,
		"Shift_R" => global::Android.Views.Keycode.ShiftRight,
		"Ctrl_L" => global::Android.Views.Keycode.CtrlLeft,
		"Ctrl_R" => global::Android.Views.Keycode.CtrlRight,
		"Alt_L" => global::Android.Views.Keycode.AltLeft,
		"Alt_R" => global::Android.Views.Keycode.AltRight,
		"Meta_L" => global::Android.Views.Keycode.MetaLeft,
		"Home" => global::Android.Views.Keycode.MoveHome,
		"End" => global::Android.Views.Keycode.MoveEnd,
		"Left" => global::Android.Views.Keycode.DpadLeft,
		"Right" => global::Android.Views.Keycode.DpadRight,
		"Up" => global::Android.Views.Keycode.DpadUp,
		"Down" => global::Android.Views.Keycode.DpadDown,
		_ => null
	};

	// 將 IKeyChar.Name 轉換為要 commit 的文字
	static string? KeyNameToText(string name) => name switch {
		"" => null,
		" " => " ",
		"Backspace" => null,
		"Enter" => null,
		"Tab" => null,
		"Shift_L" => null,
		"Shift_R" => null,
		"Ctrl_L" => null,
		"Ctrl_R" => null,
		"Alt_L" => null,
		"Alt_R" => null,
		"Meta_L" => null,
		"Fn" => null,
		"CapsLock" => null,
		"Home" => null,
		"End" => null,
		"Left" => null,
		"Right" => null,
		"Up" => null,
		"Down" => null,
		_ => name
	};
}
