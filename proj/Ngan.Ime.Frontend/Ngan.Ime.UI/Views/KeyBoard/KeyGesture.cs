namespace Ngan.Ime.UI.Views.KeyBoard;

using Ngan.Ime.Core.Keys;

[Doc($$"""
一個鍵位手勢要做什麼。

*為何不是直接放委派*：鍵盤布局要能被當成資料檢視（哪些鍵、哪些手勢），
若每個手勢都是一個 lambda，布局就變回「代碼」而無法枚舉或序列化。
故手勢只描述意圖（送哪個鍵、切哪個模式），由 {{nameof(VmKeyBoard)}} 在執行時翻譯成動作。

#Eg[
	```cs
	KeyGesture CtrlC = KeyGesture.CtrlChord(KeyChars.LowerC);
	KeyGesture Up = KeyGesture.Send(KeyChars.A);
	```
]
""")]
public sealed class KeyGesture{

	[Doc("手勢種類。")]
	public EGestureKind Kind{get;init;}

	[Doc("要送出的鍵；`SendTextThenSpace` 為依次送出的鍵序列，其餘種類可能為空。")]
	public IReadOnlyList<IKeyChar> Keys{get;init;} = [];

	[Doc("手勢參數：目前只有「切到哪一層」用得到。")]
	public bool NumLayout{get;init;}

	[Doc("送一個鍵（按下＋擡起）。")]
	public static KeyGesture Send(IKeyChar Key){
		return new KeyGesture{ Kind = EGestureKind.SendKey, Keys = [Key] };
	}

	[Doc("送一組 Ctrl + 鍵（按下 Ctrl、按鍵、放鍵、放 Ctrl）。")]
	public static KeyGesture CtrlChord(IKeyChar Key){
		return new KeyGesture{ Kind = EGestureKind.SendCtrlChord, Keys = [Key] };
	}

	[Doc("依次送出若干鍵，最後補一個空格（用於 `$m,` 這類快捷輸入）。")]
	public static KeyGesture SendTextThenSpace(params IKeyChar[] Keys){
		return new KeyGesture{ Kind = EGestureKind.SendTextThenSpace, Keys = Keys };
	}

	[Doc("切換中英模式。")]
	public static KeyGesture ToggleAscii(){
		return new KeyGesture{ Kind = EGestureKind.ToggleAscii };
	}

	[Doc("切換 Shift 鎖定。")]
	public static KeyGesture ToggleShiftLock(){
		return new KeyGesture{ Kind = EGestureKind.ToggleShiftLock };
	}

	[Doc("隱藏鍵盤（目前只在實驗路徑使用）。")]
	public static KeyGesture HideKeyboard(){
		return new KeyGesture{ Kind = EGestureKind.HideKeyboard };
	}

	[Doc("切換到數字層／字母層。")]
	public static KeyGesture SetLayout(bool NumLayout){
		return new KeyGesture{ Kind = EGestureKind.SetLayout, NumLayout = NumLayout };
	}
}

[Doc("手勢種類。")]
public enum EGestureKind{
	[Doc("送一個鍵。")]
	SendKey,
	[Doc("送 Ctrl + 鍵。")]
	SendCtrlChord,
	[Doc("依次送多個鍵，最後補空格。")]
	SendTextThenSpace,
	[Doc("切換中英。")]
	ToggleAscii,
	[Doc("切換 Shift 鎖定。")]
	ToggleShiftLock,
	[Doc("隱藏鍵盤。")]
	HideKeyboard,
	[Doc("切換鍵盤層。")]
	SetLayout,
}

[Doc("手勢可以掛在鍵的哪個位置。")]
public enum EGestureSlot{
	[Doc("點擊（不是手勢槽，但為了讓 View 用同一個入口分派，也放進來）。")]
	Click,
	[Doc("長按。")]
	LongClick,
	[Doc("上滑。")]
	SwipeUp,
	[Doc("下滑。")]
	SwipeDown,
	[Doc("左滑。")]
	SwipeLeft,
	[Doc("右滑。")]
	SwipeRight,
}
