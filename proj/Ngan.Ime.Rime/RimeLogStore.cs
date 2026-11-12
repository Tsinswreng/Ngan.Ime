namespace Ngan.Ime.Rime;

using Microsoft.Extensions.Logging;
using Ngan.Ime.Infra.Log;
using Tsinswreng.CsCore;

[Doc($$"""
保存最近一段 Rime 日誌，供稍後纔建立的界面直接回放。

*為何要有它*：librime 在鍵盤界面出現之前就開始初始化，那些日誌若只進 ILogger 就再也看不到；
故這裡留一份固定長度的環形緩衝，界面建立時先回放再訂閱。

*為何固定 200 行*：日誌頁只是「最近發生了什麼」的窗口，不是歸檔；
真正的歸檔在檔案 sink（`FileLogger`）。上限同時保護記憶體。

*AOT*：只用 List<str> 與鎖，不反射。

#Eg[
	```cs
	foreach(var Line in RimeLogStore.Inst.Snapshot()){ /* 回放 */ }
	```
]
""")]
public class RimeLogStore : IEngineLogSource {

	[Doc("最多保留的行數。")]
	public const i32 MaxLines = 200;

	[Doc("環形緩衝的鎖；引擎回調線程與界面線程都會碰它。")]
	static readonly Lock StoreLock = new();

	[Doc("緩衝本體；只在 StoreLock 內增刪。")]
	static readonly List<str> Lines = [];

	[Doc("全局單例；引擎與界面共用同一份緩衝。")]
	public static RimeLogStore Inst { get; } = new();

	[Doc("新日誌追加事件。")]
	public event EventHandler<EngineLogEntryEventArgs>? OnLogAppended;

	[Doc($$"""
	收下一條新日誌，並維持固定長度的環形緩衝。
	#Prm[{{nameof(Level)}}][等級。]
	#Prm[{{nameof(Message)}}][原始訊息。]
	""")]
	public void Append(LogLevel Level, str Message){
		var Line = $"[{Level}] {Message}";
		lock(StoreLock){
			Lines.Add(Line);
			// 一次可能超出多行（例如批量回放），故用 while 而不是 if。
			while(Lines.Count > MaxLines){
				Lines.RemoveAt(0);
			}
		}
		OnLogAppended?.Invoke(this, new EngineLogEntryEventArgs{
			Level = Level,
			Message = Message,
			FormattedLine = Line,
		});
	}

	[Doc("取得當前日誌快照（副本）；界面拿到後可安全遍歷。")]
	public IReadOnlyList<str> Snapshot(){
		lock(StoreLock){
			return [.. Lines];
		}
	}
}
