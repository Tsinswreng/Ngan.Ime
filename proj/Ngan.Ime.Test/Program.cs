namespace Ngan.Ime.Test;

using Avalonia.Controls;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Voice;
using Ngan.Ime.Rime;
using global::Rime.Api;
using Ngan.Ime.Core.Keys;
using Ngan.Ime.UI;
using Ngan.Ime.UI.Views.KeyBoard;
using Ngan.Ime.Backend.Voice.Volcengine;
using Tsinswreng.CsCore;

[Doc($$"""
測試入口：先跑「純邏輯」的單元測試。

*為何不是 CsTreeTest*：本倉庫的測試框架有兩個不相容的版本（NuGet `0.1.1-alpha` 與本地 `0.2.0-alpha`），
選哪一個會影響整個倉庫的測試寫法，屬需要拍板的架構決策。在拍板前先用零依賴的 runner，
把「最該測、且現在就能測」的純函數覆蓋起來；接框架時把 {{nameof(Check)}} 換成框架斷言即可。

*覆蓋範圍*：按鍵登記表、按鍵值語義、Rime 鍵碼轉換、鍵盤布局資料、火山協議編解碼、
界面共享狀態機。這些都不需要 UI 線程、不需要容器、不碰真檔案。

#Eg[
	```sh
	dotnet run --project proj/Ngan.Ime.Frontend/Ngan.Ime.Test
	```
]
""")]
internal static class Program{

	[Doc("已通過的用例數。")]
	static i32 Passed;

	[Doc("失敗的用例數。")]
	static i32 Failed;

	[Doc("跑全部用例；有失敗時退出碼為 1。")]
	static i32 Main(str[] Args){
		TestKeyChars();
		TestKeyCharValueSemantics();
		TestRimeKeyCharConverter();
		TestKeyLayout();
		TestVolcengineCodec();
		TestImeUiState();

		Console.WriteLine($"Ngan.Ime.Test: {Passed} passed, {Failed} failed.");
		return Failed == 0 ? 0 : 1;
	}

	[Doc("斷言並記錄結果。")]
	static void Check(bool Ok, str Name){
		if(Ok){
			Passed++;
			return;
		}
		Failed++;
		Console.WriteLine($"FAIL: {Name}");
	}

	[Doc("斷言兩個值相等。")]
	static void Eq<T>(T Actual, T Expected, str Name){
		Check(EqualityComparer<T>.Default.Equals(Actual, Expected), $"{Name}（實際 {Actual}，期望 {Expected}）");
	}

	[Doc("按鍵登記表：名字唯一、索引與全表一致、查找可用。")]
	static void TestKeyChars(){
		var Names = new HashSet<str>(StringComparer.Ordinal);
		foreach(var Key in KeyChars.All){
			Check(!string.IsNullOrEmpty(Key.Name), "KeyChars.All 不得有空鍵名");
			Check(Names.Add(Key.Name), $"KeyChars 鍵名重複：{Key.Name}");
		}
		Eq(KeyChars.ByName.Count, KeyChars.All.Count, "ByName 數量等於 All");
		Check(ReferenceEquals(KeyChars.Find("Enter"), KeyChars.Enter), "Find(\"Enter\") 回同一個實例");
		Check(KeyChars.Find("NotAKey") is null, "Find 未登記鍵回 null");
	}

	[Doc("按鍵值語義：同名即相等，可作為字典鍵。")]
	static void TestKeyCharValueSemantics(){
		var Copy = new KeyChar("a");
		Check(Copy.Equals(KeyChars.LowerA), "new KeyChar(\"a\") 等於 KeyChars.LowerA");
		Check(!Copy.Equals(KeyChars.A), "\"a\" 不等於 \"A\"");
		var Map = new Dictionary<IKeyChar, i32>{ [KeyChars.LowerA] = 1 };
		Eq(Map.GetValueOrDefault(new KeyChar("a")), 1, "字典以鍵名命中");
	}

	[Doc("Rime 鍵碼：字母 ASCII、功能鍵 keysym、Shift 上檔、未知鍵回 0。")]
	static void TestRimeKeyCharConverter(){
		var Conv = RimeKeyCharConverter.Inst;
		Eq(Conv.Convert(Event(KeyChars.LowerA, KeyStates.Down)).Code, (i32)'a', "a → 0x61");
		Eq(Conv.Convert(Event(KeyChars.Enter, KeyStates.Down)).Code, RimeKeysym.Return, "Enter → keysym");
		var Up = Conv.Convert(Event(KeyChars.LowerA, KeyStates.Up));
		Eq(Up.Mask & RimeModifier.kReleaseMask, RimeModifier.kReleaseMask, "擡起帶 release mask");
		var Shifted = Conv.Convert(new KeyEvent{
			KeyChar = KeyChars.LowerA,
			KeyState = KeyStates.Down,
			KeyBoardState = KeyBoardState.Mk(KeyChars.Shift_L),
		});
		Eq(Shifted.Code, (i32)'A', "Shift+a → 0x41");
		Check((Shifted.Mask & RimeModifier.kShiftMask) != 0, "Shift 進 mask");
		Eq(Conv.Convert(new KeyEvent{
			KeyChar = new KeyChar("NotAKey"),
			KeyState = KeyStates.Down,
		}).Code, 0, "未知鍵回 0");
	}

	[Doc("鍵盤布局：兩層都有六排、每排鍵數不超過欄數、鍵都在登記表內。")]
	static void TestKeyLayout(){
		Eq(KeyLayout.MainRows.Count, 6, "字母層六排");
		Eq(KeyLayout.NumRows.Count, 6, "數字層六排");
		foreach(var Rows in new[]{ KeyLayout.MainRows, KeyLayout.NumRows }){
			foreach(var Row in Rows){
				var Cols = new ColumnDefinitions(Row.Cols).Count;
				Check(Row.Keys.Count <= Cols, $"一排放不下：{Row.Keys.Count} 鍵 / {Cols} 欄");
				foreach(var Key in Row.Keys){
					Check(KeyChars.ByName.ContainsKey(Key.Key.Name), $"布局用了未登記的鍵：{Key.Key.Name}");
				}
			}
		}
	}

	[Doc("火山協議：往返解碼、空淨荷 gzip、畸形頭、超限拒絕。")]
	static void TestVolcengineCodec(){
		var Frame = VolcengineProtocolCodec.EncodeAudio([1, 2, 3, 4], IsLast: false);
		var Msg = VolcengineProtocolCodec.Decode(Frame, 1024);
		Eq(Msg.MessageType, EVolcengineMessageType.AudioOnlyRequest, "消息型別是 audio-only");
		Eq(Msg.Payload.Length, 4, "音頻負載長度");
		Check(Msg.Payload.Span.SequenceEqual(new byte[]{ 1, 2, 3, 4 }), "音頻負載內容");

		var Last = VolcengineProtocolCodec.Decode(
			VolcengineProtocolCodec.EncodeAudio([], IsLast: true), 1024);
		Eq(Last.Payload.Length, 0, "尾包是可解壓的空負載");
		Check((Last.Flags & EVolcengineMessageFlags.LastPackage) != 0, "尾包帶 LastPackage 標誌");

		var Threw = false;
		try{
			_ = VolcengineProtocolCodec.Decode(new byte[]{ 0x11 }, 1024);
		}catch(VoiceRecognitionException){
			Threw = true;
		}
		Check(Threw, "不足 4 字節拋協議錯誤");

		Threw = false;
		try{
			_ = VolcengineProtocolCodec.Decode(Frame, 2);
		}catch(VoiceRecognitionException){
			Threw = true;
		}
		Check(Threw, "解壓後超過上限拋協議錯誤");
	}

	[Doc("界面共享狀態：body 頁互斥、首屏日誌生命週期、布局狀態共享。")]
	static void TestImeUiState(){
		var Ui = ImeUiState.Mk();
		Ui.ToggleClipboard();
		Check(Ui.IsClipboardVisible, "切換後剪貼板可見");
		Ui.ToggleVoiceInput();
		Check(Ui.IsVoiceInputVisible && !Ui.IsClipboardVisible, "語音頁與剪貼板互斥");
		Ui.ToggleRimeLog();
		Check(Ui.IsRimeLogVisible && !Ui.IsVoiceInputVisible, "日誌頁與語音頁互斥");

		Ui.BeginInitialRimeLog();
		Check(Ui.IsRimeLogVisible && !Ui.HasUserSelectedBodyPage, "首屏日誌打開且未標記用戶選擇");
		Ui.ToggleClipboard();
		Check(Ui.HasUserSelectedBodyPage, "用戶操作後標記已選擇 body 頁");
		Ui.CompleteInitialRimeLog(true);
		Check(Ui.IsClipboardVisible, "連接成功不再搶回日誌頁");

		Ui.IsNumLayout = true;
		Ui.IsShiftLocked = true;
		Check(Ui.IsNumLayout && Ui.IsShiftLocked, "布局狀態可共享讀寫");
	}

	[Doc("造一個簡化的按下事件。")]
	static KeyEvent Event(IKeyChar Key, IKeyState State){
		return new KeyEvent{ KeyChar = Key, KeyState = State };
	}
}
