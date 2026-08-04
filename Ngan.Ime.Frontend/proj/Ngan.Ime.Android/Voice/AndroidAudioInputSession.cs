using System.Threading.Channels;
using Android.Media;
using Ngan.Ime.Core.Voice;

namespace Ngan.Ime.Android.Voice;

/// 一次 Android AudioRecord 採集會話。
/// 專用後台循環執行阻塞式讀取，有界 Channel 把設備節奏與 WebSocket 發送節奏解耦。
public sealed partial class AndroidAudioInputSession : IAudioInputSession{
	/// Android 原生錄音器；會話獨佔並在停止時釋放。
	readonly AudioRecord Recorder;

	/// 單一消費者 PCM 隊列；滿載時會話失敗，不靜默丟音頻。
	readonly Channel<ReadOnlyMemory<byte>> Chunks;

	/// 合併調用方取消和本會話停止信號。
	readonly CancellationTokenSource SessionCts;

	/// 後台阻塞讀取循環，用於等待停止和傳播設備錯誤。
	readonly Task CaptureTask;

	/// 固定 PCM 分片字節數。
	readonly i32 ChunkByteLength;

	/// 防止 Stop 與 Dispose 重複釋放 AudioRecord。
	i32 IsStopped;

	/// 建立、啓動 AudioRecord 並啓動唯一採集循環。
	public partial AndroidAudioInputSession(AudioInputOptions Options, CT Ct);

	/// 單次消費按時間順序產生的 PCM 分片。
	public partial IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunks(CT Ct);

	/// 停止阻塞讀取、結束隊列並等待後台循環退出；可重複調用。
	public partial Task<nil> Stop(CT Ct);

	/// 釋放會話；與 Stop 使用同一冪等路徑。
	public partial ValueTask DisposeAsync();

	/// 從 AudioRecord 讀滿固定分片並寫入有界隊列。
	private partial Task Capture(CT Ct);

	/// 把 Android 錄音錯誤轉換成跨平台語音錯誤。
	private static partial VoiceRecognitionException MkAudioError(str Message, i32 ErrorCode = 0, Exception? Error = null);
}
