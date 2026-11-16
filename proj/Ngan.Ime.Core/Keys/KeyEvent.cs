namespace Ngan.Ime.Core.Keys;


[Doc($$"""
{{nameof(IKeyEvent)}} 的默認實現。
{{nameof(KeyChar)}} 與 {{nameof(KeyState)}} 標成 `required`：忘記設定會在編譯期被擋下，
而不是在執行期變成 null 傳給下游。

#Eg[
	```cs
	new KeyEvent{ KeyChar = KeyChars.Enter, KeyState = KeyStates.Down };
	```
]
""")]
public class KeyEvent : IKeyEvent {
	[Doc("按了哪個鍵。")]
	public required IKeyChar KeyChar{get;set;}

	[Doc("按下還是擡起。")]
	public required IKeyState KeyState{get;set;}

	[Doc("當時鍵盤上還按著哪些鍵；null 表示沒有修飾鍵。")]
	public IKeyBoardState? KeyBoardState{get;set;}
}
