namespace Ngan.Ime.Core.Keys;

public class RespInput{

}

[Obsolete(@$"use {nameof(ISvcIme)}")]
public interface IImeState{

	public IDictionary<object, object?> Cfg{get;set;}
	public object? GetOption();
	public object SetOption();
	public IOsKeyProcessor OsKeyProcessor{get;set;}
	public IImeKeyProcessor ImeKeyProcessor{get;set;}

	/// <summary>
	/// test
	/// </summary>
	/// <param name="KeyChars"></param>
	/// <returns></returns>
	public RespInput Input(IEnumerable<IKeyChar> KeyChars);

	public event EventHandler<object?>? OnInput;


}
