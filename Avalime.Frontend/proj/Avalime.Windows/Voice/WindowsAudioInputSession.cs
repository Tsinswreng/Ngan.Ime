using System.Threading.Channels;
using Avalime.Core.Voice;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Avalime.Windows.Voice;

/// 一次 Windows WASAPI 麥克風採集會話。
/// NAudio 回調只負責複製短分片並寫入 Channel，異步消費者負責網絡發送，避免阻塞音頻線程。
public sealed partial class WindowsAudioInputSession : IAudioInputSession{
	/// Windows 默認通信麥克風設備。
	readonly MMDevice Device;

	/// WASAPI 捕獲器；只在當前會話生命週期內持有設備。
	readonly WasapiCapture Capture;

	/// 捕獲格式到識別格式的流式轉換輸入緩衝。
	readonly BufferedWaveProvider SourceBuffer;

	/// 把設備格式重採樣爲目標 PCM 格式的轉換器。
	readonly MediaFoundationResampler Resampler;

	/// 暫存重採樣後不足一個目標分片的 PCM，確保對外只輸出固定時長分片。
	readonly BufferedWaveProvider TargetBuffer;

	/// 向單一消費者輸出 PCM 分片的有界隊列。
	readonly Channel<ReadOnlyMemory<byte>> Chunks;

	/// 控制採集會話生命週期。
	readonly CancellationTokenSource SessionCts;

	/// 目標音頻格式和分片時長。
	readonly AudioInputOptions Options;

	/// 單個目標分片的字節數。
	readonly i32 ChunkByteLength;

	/// 是否已經停止或釋放。
	public bool IsDisposed{get;private set;}

	/// 建立會話、初始化格式轉換鏈路並訂閱 NAudio 事件；StartRecording 由 Start 調用。
	public partial WindowsAudioInputSession(
		MMDevice Device
		, WasapiCapture Capture
		, BufferedWaveProvider SourceBuffer
		, MediaFoundationResampler Resampler
		, BufferedWaveProvider TargetBuffer
		, AudioInputOptions Options
		, CT Ct
	);

	/// 啓動底層捕獲設備；構造與啓動分開以便完整訂閱事件後再接收回調。
	public partial void Start();

	/// 單次消費轉換後的 PCM 分片隊列。
	public partial IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunks(CT Ct);

	/// 停止設備、結束隊列並解除事件訂閱；可重複調用。
	public partial Task<nil> Stop(CT Ct);

	/// 釋放 Windows 音頻資源，與 Stop 保持冪等。
	public partial ValueTask DisposeAsync();

	/// NAudio 音頻回調：複製設備數據到轉換緩衝，並儘量產出完整目標分片。
	private partial void OnDataAvailable(obj? Sender, WaveInEventArgs E);

	/// NAudio 停止回調：把設備異常傳播給異步枚舉消費者並結束隊列。
	private partial void OnRecordingStopped(obj? Sender, StoppedEventArgs E);

	/// 從重採樣器讀取所有當前可用的完整分片並寫入 Channel。
	private partial void DrainResampler();
}
