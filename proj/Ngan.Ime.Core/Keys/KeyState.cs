namespace Ngan.Ime.Core.Keys;


[Doc($$"""
{{nameof(IKeyState)}} 的默認實現。
按鍵狀態只有兩種可能，故實際上都用 {{nameof(KeyStates)}} 的兩個共享實例，不另行 new。
""")]
public class KeyState : IKeyState {
	[Doc("true 表示按下。")]
	public bool IsKeyDown{get;set;}
}

[Doc($$"""
按鍵狀態的兩個共享實例。

*為何共用*：狀態是無變化的值，每次 new 只是多一次配置；共用也讓必要時可用 `ReferenceEquals` 比較。
呼叫方不得修改這兩個實例本身（要改就 new 一個 {{nameof(KeyState)}}）。

#Eg[
	```cs
	new KeyEvent{ KeyChar = KeyChars.Enter, KeyState = KeyStates.Down };
	```
]
""")]
public static class KeyStates {
	[Doc("按下。")]
	public static readonly IKeyState Down = new KeyState{ IsKeyDown = true };

	[Doc("擡起。")]
	public static readonly IKeyState Up = new KeyState{ IsKeyDown = false };
}
