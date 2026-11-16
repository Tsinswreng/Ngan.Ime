namespace Ngan.Ime.Core.Keys;


[Doc($$"""
一次按鍵發生時，鍵盤上「還有哪些鍵被按著」。

*為何是集合而不是修飾位*：合成鍵盤會一次送出 Shift+鍵 這種組合，
而下游（Rime、Android、Windows）各自需要的掩碼不同，故抽象層只保存「按下了哪些鍵」，
由各實現自己轉成自己的掩碼表示。

#Eg[
	```cs
	var State = KeyBoardState.Mk(KeyChars.Shift_L);
	bool Shifted = State.IsKeyDown(KeyChars.Shift_L); // true
	```
]
""")]
public interface IKeyBoardState{
	[Doc("指定的鍵此刻是否被按下。按鍵名比較，不要求是同一個實例。")]
	public bool IsKeyDown(IKeyChar Key);

	[Doc("此刻被按下的所有鍵；永不為 null（未初始化時為空集合）。")]
	public ISet<IKeyChar> AllDownKeys{get;set;}
}
