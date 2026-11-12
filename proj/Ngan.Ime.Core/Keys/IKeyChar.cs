namespace Ngan.Ime.Core.Keys;

using Tsinswreng.CsCore;

[Doc($$"""
一個按鍵字符的身份。

*身份就是 {{nameof(Name)}}*：整個專案（含跨進程的 TSF 管道）只用這個字符串識別一個鍵，
故同名的兩個 {{nameof(KeyChar)}} 必須被視為同一個鍵——這由 {{nameof(KeyChar)}} 的值語義保證，
而不是靠大家碰巧共用同一個靜態實例。

#Eg[
	```cs
	IKeyChar A = KeyChars.A;
	bool Same = A.Equals(new KeyChar("A")); // true
	```
]
""")]
public interface IKeyChar{
	[Doc("鍵名。大小寫敏感（例如 `A` 與 `a` 是兩個不同的鍵），空串表示未指定。")]
	public str Name{get;set;}
}
