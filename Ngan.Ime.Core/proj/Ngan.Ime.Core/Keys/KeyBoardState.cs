namespace Ngan.Ime.Core.Keys;

public class KeyBoardState : IKeyBoardState
{
	public ISet<IKeyChar> AllDownKeys{get;set;}

	public bool IsKeyDown(IKeyChar key){
		return AllDownKeys.Contains(key);
	}

	public static KeyBoardState Mk(params IKeyChar[] downKeys){
		return new KeyBoardState{
			AllDownKeys = new HashSet<IKeyChar>(downKeys)
		};
	}
}
