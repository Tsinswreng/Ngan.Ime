namespace Ngan.Ime.Core.Keys;


[Doc($$"""
把按鍵交給「輸入法引擎」處理的處理器。
Rime 實現見 `Ngan.Ime.Rime.RimeKeyProcessor`；宿主不得直接實現本介面，
否則輸入法邏輯會從引擎層漏到界面層。
""")]
public interface IImeKeyProcessor
	:IKeyProcessor
{
}
