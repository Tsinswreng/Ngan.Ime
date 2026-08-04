using Microsoft.Extensions.Logging;

namespace Ngan.Ime.Core.Infra.Log;

/// <summary>
/// 向 UI 暴露 Rime 日誌流的抽象。
/// UI 只能依賴此介面，不直接引用 Ngan.Ime.Rime 的具體實現。
/// </summary>
public interface IRimeLogSource
{
	/// <summary>
	/// Rime 產生新日誌時觸發。
	/// </summary>
	public event EventHandler<RimeLogEntryEventArgs>? OnLogAppended;

	/// <summary>
	/// 取得當前緩衝中的歷史日誌快照，供新建 UI 回放。
	/// </summary>
	public IReadOnlyList<str> Snapshot();
}

/// <summary>
/// 單條 Rime 日誌事件數據。
/// </summary>
public class RimeLogEntryEventArgs : EventArgs
{
	public required LogLevel Level { get; init; }
	public required str Message { get; init; }
	public required str FormattedLine { get; init; }
}
