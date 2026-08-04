namespace Ngan.Ime.Core.Keys;


[Doc(@$"鍵盤狀態 記錄被按下的按鍵")]
public interface IKeyBoardState{//宜用單例
	[Doc(@$"當前某按鍵是否按下")]
	public bool IsKeyDown(IKeyChar key);

	[Doc(@$"當前所有被按下的按鍵")]
	public ISet<IKeyChar> AllDownKeys{get;set;}
}
