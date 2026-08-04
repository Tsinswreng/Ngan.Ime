namespace Ngan.Ime.Core.Keys;


public interface IRespOnKeyEvent{
	public IList<string> Commits{get;set;}
	/// Rime 未處理的按鍵，應轉發給 OS
	public IList<IKeyEvent> UnhandledKeys{get;set;}
}

public class RespOnKeyEvent:IRespOnKeyEvent{
	public IList<string> Commits{get;set;} = [];
	/// Rime 未處理的按鍵，應轉發給 OS
	public IList<IKeyEvent> UnhandledKeys{get;set;} = [];
}



[Doc("輸入法 按鍵事件處理器")]
public interface IKeyEventProcessor{
	public Task<IRespOnKeyEvent> OnKeyEvents(
		IEnumerable<IKeyEvent> keyEvents, CT Ct
	);
}
