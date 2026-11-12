namespace Ngan.Ime.Core.Keys;

using Tsinswreng.CsCore;

[Doc($$"""
按鍵處理器的共同契約：能處理按鍵，且能回報錯誤。
具體角色由 {{nameof(IOsKeyProcessor)}}（發給作業系統）與 {{nameof(IImeKeyProcessor)}}（交給輸入法引擎）區分。
""")]
public interface IKeyProcessor
	:IKeyEventProcessor
	,I_OnErr
{
}
