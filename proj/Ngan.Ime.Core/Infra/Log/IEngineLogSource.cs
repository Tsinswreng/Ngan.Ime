namespace Ngan.Ime.Infra.Log;

using Microsoft.Extensions.Logging;

[Doc($$"""
向界面暴露「輸入法引擎日誌流」的抽象。

*為何在 Core 而不是引擎層*：界面要顯示引擎日誌，但**不得引用任何具體引擎**
（`Ngan.Ime.UI` 只引用 Core）。故 Core 提供一個引擎中立的介面，具體引擎實現它
（現行是 `Ngan.Ime.Rime.RimeLogStore`）。換引擎時界面不用改，只是換一個實現。

*為何不是直接注入 {{nameof(ILogger)}}*：日誌頁需要「回放既有 200 行 + 持續追加」兩件事，
後者需要事件、前者需要快照；{{nameof(ILogger)}} 只有寫入，沒有讀回。

#Eg[
	```cs
	IEngineLogSource Src = Di.GetRSvc<IEngineLogSource>();
	foreach(var Line in Src.Snapshot()){ /* 回放 */ }
	```
]
""")]
public interface IEngineLogSource {
	[Doc("引擎產生新日誌時觸發；可能在任意線程。")]
	public event EventHandler<EngineLogEntryEventArgs>? OnLogAppended;

	[Doc("取得當前緩衝中的歷史日誌快照，供新建界面回放。")]
	public IReadOnlyList<str> Snapshot();
}

[Doc($$"""
一條引擎日誌事件。

*為何同時給 {{nameof(Message)}} 與 {{nameof(FormattedLine)}}*：前者供結構化處理（例如按等級過濾），
後者是已經排好版、可直接顯示的一行——界面不必自己再拼一次格式。
""")]
public class EngineLogEntryEventArgs : EventArgs {
	[Doc("日誌等級。")]
	public required LogLevel Level { get; init; }

	[Doc("原始訊息，不含等級前綴。")]
	public required str Message { get; init; }

	[Doc("已排版的一行，形如 `[Information] …`。")]
	public required str FormattedLine { get; init; }
}
