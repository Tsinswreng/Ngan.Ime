namespace Ngan.Ime.Core.Keys;

public class KeyEvent
	:IKeyEvent
{
	public IKeyChar KeyChar{get;set;}
	public IKeyState KeyState{get;set;}
	public IKeyBoardState? KeyBoardState{get;set;}
}

