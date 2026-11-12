namespace Ngan.Ime.Core.Keys;

using Tsinswreng.CsCore;

[Doc($$"""
一次按鍵事件：按了哪個鍵、是按下還是擡起、當時鍵盤上還按著什麼。

#Eg[
	```cs
	var Down = new KeyEvent{ KeyChar = KeyChars.LowerA, KeyState = KeyStates.Down };
	```
]
""")]
public interface IKeyEvent{
	[Doc("按了哪個鍵。")]
	public IKeyChar KeyChar{get;set;}

	[Doc("按下還是擡起。")]
	public IKeyState KeyState{get;set;}

	[Doc("當時鍵盤上還按著哪些鍵；null 表示「沒有修飾鍵」與「未知」兩種情況共用（下游一律視為無修飾）。")]
	public IKeyBoardState? KeyBoardState{get;set;}
}
