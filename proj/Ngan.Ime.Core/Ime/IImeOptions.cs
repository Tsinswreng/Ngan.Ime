namespace Ngan.Ime.Core.Ime;

using Tsinswreng.CsCore;

[Doc($$"""
輸入法的可切換選項。與 {{nameof(IImeState)}} 分開，因為切換是「動作」，讀取是「狀態」；
只讀的視圖不該拿到切換能力，切換的地方（工具欄）也不需要讀全部狀態。

#Eg[
	```cs
	await Options.ToggleAsciiModeAsy(Ct);
	```
]
""")]
public interface IImeOptions{
	[Doc("切換中英（ASCII）模式；失敗時拋。")]
	public Task<nil> ToggleAsciiModeAsy(CT Ct = default);

	[Doc("切換簡繁輸出；失敗時拋。")]
	public Task<nil> ToggleSimplificationAsy(CT Ct = default);
}
