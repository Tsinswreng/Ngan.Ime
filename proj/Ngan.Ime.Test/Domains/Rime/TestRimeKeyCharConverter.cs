using Ngan.Ime.Core.Keys;
using Ngan.Ime.Rime;
using global::Rime.Api;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Rime;

[Doc($$"""
被測對象：`{{nameof(RimeKeyCharConverter)}}`，把本項目的按鍵事件翻成 Rime 的鍵碼與修飾位。

測四類情形：字母走 ASCII、功能鍵走 keysym、擡起帶 release 位、Shift 出上檔並置 Shift 位，
外加未登記鍵一律回 0。
""")]
public class TestRimeKeyCharConverter : Tester{
	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(Convert_Letter_Should_ReturnAscii), Convert_Letter_Should_ReturnAscii!);
		this.AddTestCase(nameof(Convert_FunctionKey_Should_ReturnKeysym), Convert_FunctionKey_Should_ReturnKeysym!);
		this.AddTestCase(nameof(Convert_KeyUp_Should_SetReleaseMask), Convert_KeyUp_Should_SetReleaseMask!);
		this.AddTestCase(nameof(Convert_WithShift_Should_ReturnUppercase), Convert_WithShift_Should_ReturnUppercase!);
		this.AddTestCase(nameof(Convert_UnknownKey_Should_ReturnZero), Convert_UnknownKey_Should_ReturnZero!);
	}

	[Doc("""
	小寫字母直接取其 ASCII 碼。
	""")]
	public async Task<nil> Convert_Letter_Should_ReturnAscii(obj? O){
		var T = Assert.IsTrue;
		var Code = RimeKeyCharConverter.Inst.Convert(Event(KeyChars.LowerA, KeyStates.Down)).Code;
		T(Code == (i32)'a', $"a 應轉為 0x61，實際 {Code}");
		return NIL;
	}

	[Doc("""
	功能鍵（Enter）要映射到 Rime 的 keysym，而不是 ASCII。
	""")]
	public async Task<nil> Convert_FunctionKey_Should_ReturnKeysym(obj? O){
		var T = Assert.IsTrue;
		var Code = RimeKeyCharConverter.Inst.Convert(Event(KeyChars.Enter, KeyStates.Down)).Code;
		T(Code == RimeKeysym.Return, $"Enter 應轉為 keysym {RimeKeysym.Return}，實際 {Code}");
		return NIL;
	}

	[Doc("""
	擡起事件要帶 release 修飾位，否則 Rime 會把它當成又一次按下。
	""")]
	public async Task<nil> Convert_KeyUp_Should_SetReleaseMask(obj? O){
		var T = Assert.IsTrue;
		var Mask = RimeKeyCharConverter.Inst.Convert(Event(KeyChars.LowerA, KeyStates.Up)).Mask;
		T((Mask & RimeModifier.kReleaseMask) == RimeModifier.kReleaseMask, "擡起應帶 release mask");
		return NIL;
	}

	[Doc("""
	按住 Shift 敲字母時要出上檔字元，且置上 Shift 修飾位。
	""")]
	public async Task<nil> Convert_WithShift_Should_ReturnUppercase(obj? O){
		var T = Assert.IsTrue;
		var Shifted = RimeKeyCharConverter.Inst.Convert(new KeyEvent{
			KeyChar = KeyChars.LowerA,
			KeyState = KeyStates.Down,
			KeyBoardState = KeyBoardState.Mk(KeyChars.Shift_L),
		});
		T(Shifted.Code == (i32)'A', $"Shift+a 應出 0x41，實際 {Shifted.Code}");
		T((Shifted.Mask & RimeModifier.kShiftMask) != 0, "Shift 應進 mask");
		return NIL;
	}

	[Doc("""
	未登記的鍵名要回 0，避免把不認識的字元送進 Rime。
	""")]
	public async Task<nil> Convert_UnknownKey_Should_ReturnZero(obj? O){
		var T = Assert.IsTrue;
		var Code = RimeKeyCharConverter.Inst.Convert(new KeyEvent{
			KeyChar = new KeyChar("NotAKey"),
			KeyState = KeyStates.Down,
		}).Code;
		T(Code == 0, $"未知鍵應回 0，實際 {Code}");
		return NIL;
	}

	[Doc("""
	造一個只有鍵與狀態的簡單事件，供不需要鍵盤狀態的用例使用。

	#Prm[{nameof(Key)}][按下的鍵]
	#Prm[{nameof(State)}][按下或擡起]
	#Rtn[鍵事件]
	""")]
	static KeyEvent Event(IKeyChar Key, IKeyState State){
		return new KeyEvent{ KeyChar = Key, KeyState = State };
	}
}
