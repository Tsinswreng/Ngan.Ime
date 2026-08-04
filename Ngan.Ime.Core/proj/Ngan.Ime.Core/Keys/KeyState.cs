namespace Ngan.Ime.Core.Keys;

public class KeyState
	:IKeyState
{
	public bool IsKeyDown{get;set;}
}

[Doc(@$"強類型按鍵狀態枚舉")]
public static class KeyStates{
	public static IKeyState Down = new KeyState{IsKeyDown = true};
	public static IKeyState Up = new KeyState{IsKeyDown = false};
}