namespace Ngan.Ime.Core.Keys;

using Tsinswreng.CsCore;

[Doc($$"""
{{nameof(IKeyBoardState)}} 的默認實現。

*為何字段必須初始化*：{{nameof(IsKeyDown)}} 直接查 {{nameof(AllDownKeys)}}；
舊版把它留成 null，任何 `new KeyBoardState()` 之後查一次就 NRE。
這裡在字段初始化器裡給空集合，故「不設定任何按下鍵」也是合法狀態。

#Eg[
	```cs
	var Empty = new KeyBoardState();
	Empty.IsKeyDown(KeyChars.Shift_L); // false，不會拋
	```
]
""")]
public class KeyBoardState : IKeyBoardState {

	[Doc("被按下的鍵集合；默認空集合，故不設定也表示「沒有修飾鍵」。")]
	public ISet<IKeyChar> AllDownKeys{get;set;} = new HashSet<IKeyChar>();

	[Doc($$"""
	查某鍵是否被按下；比較用按鍵名（見 {{nameof(KeyChar.Equals)}}），故不同實例也能命中。
	#Prm[{{nameof(Key)}}][要查的鍵；null 視為沒按下（協議端可能送來未知鍵）。]
	#Rtn[被按下時 true。]
	""")]
	public bool IsKeyDown(IKeyChar Key){
		return Key is not null && AllDownKeys.Contains(Key);
	}

	[Doc($$"""
	用「被按下的鍵」建一個鍵盤狀態。
	#Prm[{{nameof(DownKeys)}}][同時按著的鍵；省略表示沒有修飾鍵。]
	#Rtn[新的 {{nameof(KeyBoardState)}}。]
	#Eg[
		```cs
		var CtrlC = KeyBoardState.Mk(KeyChars.Ctrl_L, KeyChars.LowerC);
		```
	]
	""")]
	public static KeyBoardState Mk(params IKeyChar[] DownKeys){
		// HashSet 的去重依賴 KeyChar 的值語義，故同名鍵不會重複。
		return new KeyBoardState{
			AllDownKeys = new HashSet<IKeyChar>(DownKeys),
		};
	}
}
