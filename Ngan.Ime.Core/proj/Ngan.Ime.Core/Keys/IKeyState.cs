namespace Ngan.Ime.Core.Keys;

using Tsinswreng.CsCore;

[Doc(@$"按鍵狀態：某個鍵此刻是否被按下。實現是無變化的值，共享實例見 {nameof(KeyStates)}。")]
public interface IKeyState{
	[Doc("true 表示按下；false 表示擡起。")]
	public bool IsKeyDown{get;set;}
}
