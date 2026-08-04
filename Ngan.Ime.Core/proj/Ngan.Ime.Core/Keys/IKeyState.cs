namespace Ngan.Ime.Core.Keys;

[Doc(@$"按鍵狀態 記錄某按鍵是否被按下")]
public interface IKeyState{
	[Doc(@$"是否被按下")]
	public bool IsKeyDown{get;set;}
}
