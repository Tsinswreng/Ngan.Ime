namespace Ngan.Ime.Core.Keys;

[Doc("@$一次按鍵事件")]
public interface IKeyEvent{

	[Doc(@$"按了何按鍵")]
	public IKeyChar KeyChar{get;set;}

	[Doc(@$"按鍵狀態(按下/擡起)")]
	public IKeyState KeyState{get;set;}

	[Doc(@$"鍵盤狀態(還有哪些按鍵被按下了)")]
	public IKeyBoardState? KeyBoardState{get;set;}
}

