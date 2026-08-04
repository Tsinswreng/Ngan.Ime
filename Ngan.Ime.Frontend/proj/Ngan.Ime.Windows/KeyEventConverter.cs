using Ngan.Ime.Core.Keys;

namespace Ngan.Ime.Windows;

/*
TODO
慮 KeyChars中之上檔位
轉換當成 shift按+下檔按+下檔鬆+shift鬆

 */
public class KeyEventConverter{
	protected static KeyEventConverter? _inst = null;
	public static KeyEventConverter inst => _inst??= new KeyEventConverter();

	public i32 StateToDwFlags(IKeyState keyState){
		if(keyState.IsKeyDown){
			return 0;
		}else{
			return 2;
		}
	}

	public (u8, u8, i32, i32) ConvertKeyEvent(
		IKeyEvent keyEvent
	){
		u8 keyCode = 0;
		keyCode = (u8)Lower_KeyCode[keyEvent.KeyChar];
		var dwFlags = StateToDwFlags(keyEvent.KeyState);
		return (keyCode, 0, 0, dwFlags);
	}

	public IDictionary<IKeyChar, i64> Lower_KeyCode = new Dictionary<IKeyChar, i64>{
		 {KeyChars.a, 0x41}
		,{KeyChars.b, 0x42}
		,{KeyChars.c, 0x43}
		,{KeyChars.d, 0x44}
		,{KeyChars.e, 0x45}
		,{KeyChars.f, 0x46}
		,{KeyChars.g, 0x47}
		,{KeyChars.h, 0x48}
		,{KeyChars.i, 0x49}
		,{KeyChars.j, 0x4A}
		,{KeyChars.k, 0x4B}
		,{KeyChars.l, 0x4C}
		,{KeyChars.m, 0x4D}
		,{KeyChars.n, 0x4E}
		,{KeyChars.o, 0x4F}
		,{KeyChars.p, 0x50}
		,{KeyChars.q, 0x51}
		,{KeyChars.r, 0x52}
		,{KeyChars.s, 0x53}
		,{KeyChars.t, 0x54}
		,{KeyChars.u, 0x55}
		,{KeyChars.v, 0x56}
		,{KeyChars.w, 0x57}
		,{KeyChars.x, 0x58}
		,{KeyChars.y, 0x59}
		,{KeyChars.z, 0x5A}
		// 導航鍵需要能被 Windows 宿主直接轉成虛擬鍵碼。
		,{KeyChars.Home, 0x24}
		,{KeyChars.End, 0x23}

	};

	public IDictionary<IKeyChar, i64> Fn_KeyCode = new Dictionary<IKeyChar, i64>{
		 {KeyChars.Backspace, 0x08}
		,{KeyChars.Left, 0x25}
		,{KeyChars.Up, 0x26}
		,{KeyChars.Right, 0x27}
		,{KeyChars.Down, 0x28}
	};

	// IDictionary<string ,int> test = new Dictionary<string ,int>{
	// 	{"a", 1}
	// };

}
