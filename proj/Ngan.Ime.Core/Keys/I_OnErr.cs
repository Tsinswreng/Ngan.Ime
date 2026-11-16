namespace Ngan.Ime.Core.Keys;


[Doc("錯誤處理委託：回傳值目前不使用，統一寫成 object? 以便將來放處理結果（如「已重試」）。")]
public delegate obj? ErrHandler(obj Sender, obj? Arg);

[Doc($$"""
可回報錯誤的組件。

*為何是事件而不是拋*：按鍵管線跑在背景消費迴圈裡，拋出去只會被最外層吞掉；
用事件讓「哪一層出錯、怎麼補救」在宿主端決定（例如 Android 記 logcat 並震動）。
""")]
public interface I_OnErr{
	[Doc("出錯時觸發；參數依次為來源與錯誤物件。")]
	public event ErrHandler? OnErr;
}
