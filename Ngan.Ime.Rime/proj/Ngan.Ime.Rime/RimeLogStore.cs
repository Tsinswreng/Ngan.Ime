using Ngan.Ime.Core.Infra.Log;
using Microsoft.Extensions.Logging;

namespace Ngan.Ime.Rime;

/// 保存最近一段 Rime 日誌，供稍後纔建立的 UI 直接回放。
/// 這樣即使 librime 在鍵盤 UI 出現前已開始預熱，日誌頁仍能看到初始化過程。
public class RimeLogStore : IRimeLogSource {
	static readonly Lock StoreLock = new();
	static readonly List<string> Lines = [];
	const i32 MaxLines = 200;
	public static RimeLogStore Inst { get; } = new();

	public event EventHandler<RimeLogEntryEventArgs>? OnLogAppended;

	/// 收下一條新的 Rime 日誌，並維持固定長度的環形緩衝。
	public void Append(LogLevel Level, str Message) {
		var line = $"[{Level}] {Message}";
		lock (StoreLock) {
			Lines.Add(line);
			while (Lines.Count > MaxLines) {
				Lines.RemoveAt(0);
			}
		}
		OnLogAppended?.Invoke(this, new RimeLogEntryEventArgs {
			Level = Level,
			Message = Message,
			FormattedLine = line,
		});
	}

	/// 取得當前日誌快照，供 UI 初次建立時批量回放。
	public IReadOnlyList<str> Snapshot() {
		lock (StoreLock) {
			return Lines.ToArray();
		}
	}
}
