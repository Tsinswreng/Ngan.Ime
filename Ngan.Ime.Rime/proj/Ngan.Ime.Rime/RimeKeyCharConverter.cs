using Ngan.Ime.Core.Keys;
using Rime.Api;
using KS = Ngan.Ime.Core.Keys.KeyChars;
namespace Ngan.Ime.Rime;

public class RimeKeyCharConverter{
	public static RimeKeyCharConverter Inst => field??= new RimeKeyCharConverter();


	public (i32, i32) Convert(IKeyEvent keyEvent){
		i32 mask;
		if(keyEvent.KeyState.IsKeyDown){
			mask = RimeModifier.zero;
		}else{
			mask = RimeModifier.kReleaseMask;
		}
		mask |= GetModifierMask(keyEvent.KeyBoardState);
		var effectiveKey = ApplyShiftToKeyChar(keyEvent.KeyChar, keyEvent.KeyBoardState);
		var keyCode = (i32)Lower_KeyCode[effectiveKey];//TODO handle err
		return (keyCode, mask);
	}

	static i32 GetModifierMask(IKeyBoardState? keyBoardState){
		if(keyBoardState is null){
			return RimeModifier.zero;
		}

		i32 mask = RimeModifier.zero;
		if(keyBoardState.IsKeyDown(KS.Ctrl_L) || keyBoardState.IsKeyDown(KS.Ctrl_R)){
			mask |= RimeModifier.kControlMask;
		}
		if(keyBoardState.IsKeyDown(KS.Shift_L) || keyBoardState.IsKeyDown(KS.Shift_R)){
			mask |= RimeModifier.kShiftMask;
		}
		if(keyBoardState.IsKeyDown(KS.Alt_L) || keyBoardState.IsKeyDown(KS.Alt_R)){
			mask |= RimeModifier.kAltMask;
		}
		if(keyBoardState.IsKeyDown(KS.Meta_L)){
			mask |= RimeModifier.kSuperMask;
		}
		return mask;
	}

	/// <summary>
	/// UI 層只通過 KeyBoardState 表達當前有哪些修飾鍵處於按下狀態。
	/// 因此 Rime 轉碼時需要自行根據 Shift 狀態把可打印鍵映射到對應的大寫/符號 keycode。
	/// 導航鍵等非打印鍵保持原 keychar，由 mask 中的 Shift 位表達組合語義。
	/// </summary>
	static IKeyChar ApplyShiftToKeyChar(IKeyChar keyChar, IKeyBoardState? keyBoardState){
		if(keyBoardState is null){
			return keyChar;
		}
		if(!(keyBoardState.IsKeyDown(KS.Shift_L) || keyBoardState.IsKeyDown(KS.Shift_R))){
			return keyChar;
		}
		if(ShiftKeyMap.TryGetValue(keyChar, out var shifted)){
			return shifted;
		}
		return keyChar;
	}

	static readonly IReadOnlyDictionary<IKeyChar, IKeyChar> ShiftKeyMap = new Dictionary<IKeyChar, IKeyChar>{
		{KS.a, KS.A}, {KS.b, KS.B}, {KS.c, KS.C}, {KS.d, KS.D}, {KS.e, KS.E}, {KS.f, KS.F}, {KS.g, KS.G}, {KS.h, KS.H}, {KS.i, KS.I}, {KS.j, KS.J}, {KS.k, KS.K}, {KS.l, KS.L}, {KS.m, KS.M},
		{KS.n, KS.N}, {KS.o, KS.O}, {KS.p, KS.P}, {KS.q, KS.Q}, {KS.r, KS.R}, {KS.s, KS.S}, {KS.t, KS.T}, {KS.u, KS.U}, {KS.v, KS.V}, {KS.w, KS.W}, {KS.x, KS.X}, {KS.y, KS.Y}, {KS.z, KS.Z},
		{KS.D1, KS.Exclamation}, {KS.D2, KS.At}, {KS.D3, KS.HashTag}, {KS.D4, KS.Dollar}, {KS.D5, KS.Percent}, {KS.D6, KS.Caret}, {KS.D7, KS.Ampersand}, {KS.D8, KS.Asterisk}, {KS.D9, KS.Paren_L}, {KS.D0, KS.Paren_R},
		{KS.Minus, KS.Underscore}, {KS.Equal, KS.Plus},
		{KS.SquareBracket_L, KS.Braces_L}, {KS.SquareBracket_R, KS.Braces_R},
		{KS.Semicolon, KS.Colon}, {KS.Apostrophe, KS.Quote},
		{KS.Comma, KS.Less}, {KS.Period, KS.Greater}, {KS.Slash, KS.Question}, {KS.Grave, KS.Tilde},
	};


	public IDictionary<IKeyChar, i64> Lower_KeyCode = new Dictionary<IKeyChar, i64>{
		{KS.A, 0x41}
		,{KS.B, 0x42}
		,{KS.C, 0x43}
		,{KS.D, 0x44}
		,{KS.E, 0x45}
		,{KS.F, 0x46}
		,{KS.G, 0x47}
		,{KS.H, 0x48}
		,{KS.I, 0x49}
		,{KS.J, 0x4A}
		,{KS.K, 0x4B}
		,{KS.L, 0x4C}
		,{KS.M, 0x4D}
		,{KS.N, 0x4E}
		,{KS.O, 0x4F}
		,{KS.P, 0x50}
		,{KS.Q, 0x51}
		,{KS.R, 0x52}
		,{KS.S, 0x53}
		,{KS.T, 0x54}
		,{KS.U, 0x55}
		,{KS.V, 0x56}
		,{KS.W, 0x57}
		,{KS.X, 0x58}
		,{KS.Y, 0x59}
		,{KS.Z, 0x5A}

		,{KS.D0, 0x30}
		,{KS.D1, 0x31}
		,{KS.D2, 0x32}
		,{KS.D3, 0x33}
		,{KS.D4, 0x34}
		,{KS.D5, 0x35}
		,{KS.D6, 0x36}
		,{KS.D7, 0x37}
		,{KS.D8, 0x38}
		,{KS.D9, 0x39}


		,{KS.a, 0x61} // a
		,{KS.b, 0x62} // b
		,{KS.c, 0x63} // c
		,{KS.d, 0x64} // d
		,{KS.e, 0x65} // e
		,{KS.f, 0x66} // f
		,{KS.g, 0x67} // g
		,{KS.h, 0x68} // h
		,{KS.i, 0x69} // i
		,{KS.j, 0x6A} // j
		,{KS.k, 0x6B} // k
		,{KS.l, 0x6C} // l
		,{KS.m, 0x6D} // m
		,{KS.n, 0x6E} // n
		,{KS.o, 0x6F} // o
		,{KS.p, 0x70} // p
		,{KS.q, 0x71} // q
		,{KS.r, 0x72} // r
		,{KS.s, 0x73} // s
		,{KS.t, 0x74} // t
		,{KS.u, 0x75} // u
		,{KS.v, 0x76} // v
		,{KS.w, 0x77} // w
		,{KS.x, 0x78} // x
		,{KS.y, 0x79} // y
		,{KS.z, 0x7A} // z



//using KS = Ngan.Ime.Core.keys.KeyChars;
		,{KS.Tilde, 0x7E}
		,{KS.Grave, 0x60}
		,{KS.Exclamation, 0x21}
		,{KS.At, 0x40}
		,{KS.HashTag, 0x23}
		,{KS.Dollar, 0x24}
		,{KS.Percent, 0x25}
		,{KS.Caret, 0x5E}
		,{KS.Ampersand, 0x26}
		,{KS.Asterisk, 0x2A}
		,{KS.Paren_L, 0x28}
		,{KS.Paren_R, 0x29}
		,{KS.Underscore, 0x5F}
		,{KS.Minus, 0x2D}
		,{KS.Plus, 0x2B}
		,{KS.Equal, 0x3D}
		,{KS.Backspace, 65288}

		,{KS.Tab, 65289}
		,{KS.Braces_L, 123}
		,{KS.SquareBracket_L, 91}
		,{KS.Braces_R, 125}
		,{KS.SquareBracket_R, 93}
		,{KS.BackSlash, 0x5C}
		,{KS.Pipe, 0x7C}
		,{KS.CapsLock, 65509}
		,{KS.Colon, 0x3A}
		,{KS.Semicolon, 0x3B}
		,{KS.Quote, 0x22}
		,{KS.Apostrophe, 0x27}
		,{KS.Enter, 65293}

		,{KS.Shift_L, 65505}
		,{KS.Less, 0x3C}
		,{KS.Comma, 0x2C}
		,{KS.Greater, 0x3E}
		,{KS.Period, 0x2E}
		,{KS.Question, 0x3F}
		,{KS.Slash, 0x2F}
		,{KS.Shift_R, 65506}

		,{KS.Ctrl_L, 65507}
		,{KS.Meta_L, 65511}
		,{KS.Alt_L, 65513}
		,{KS.Space, 32}
		,{KS.Alt_R, 65514}
		,{KS.Fn, 65383}
		,{KS.Ctrl_R, 65508}

		// Home/End 讓 Rime 與 OS fallback 都能識別行首/行尾導航。
		,{KS.Home, 65360}
		,{KS.End, 65367}
		,{KS.Up, 65362}
		,{KS.Down, 65364}
		,{KS.Left, 65361}
		,{KS.Right, 65363}
	};
}



