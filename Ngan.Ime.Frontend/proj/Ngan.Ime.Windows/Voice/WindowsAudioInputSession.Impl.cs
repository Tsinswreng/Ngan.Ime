using System.Threading.Channels;
using Ngan.Ime.Core.Voice;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Ngan.Ime.Windows.Voice;

public sealed partial class WindowsAudioInputSession{
	/// 设置有界队列并订阅回调，所有设备回调都在 Start 前完成绑定。
	public partial WindowsAudioInputSession(
		MMDevice Device
		, WasapiCapture Capture
		, BufferedWaveProvider SourceBuffer
		, MediaFoundationResampler Resampler
		, BufferedWaveProvider TargetBuffer
		, AudioInputOptions Options
		, CT Ct
	){
		this.Device = Device;
		this.Capture = Capture;
		this.SourceBuffer = SourceBuffer;
		this.Resampler = Resampler;
		this.TargetBuffer = TargetBuffer;
		this.Options = Options;
		SessionCts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		ChunkByteLength = checked(Options.SampleRate * Options.Channels * (Options.BitsPerSample / 8) * Options.ChunkDurationMs / 1000);
		Chunks = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(Options.MaxBufferedChunks){
			SingleReader = true,
			SingleWriter = true,
			FullMode = BoundedChannelFullMode.Wait,
		});
		Capture.DataAvailable += OnDataAvailable;
		Capture.RecordingStopped += OnRecordingStopped;
	}

	/// 采集开始后 NAudio 回调才会向 Channel 填入完整 PCM 包。
	public partial void Start(){
		if(IsDisposed){
			throw new ObjectDisposedException(nameof(WindowsAudioInputSession));
		}
		Capture.StartRecording();
	}

	/// 单一消费者按音频原始顺序读取分片。
	public async partial IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunks(CT Ct){
		using var LinkedCts = CancellationTokenSource.CreateLinkedTokenSource(Ct, SessionCts.Token);
		await foreach(var Chunk in Chunks.Reader.ReadAllAsync(LinkedCts.Token)){
			yield return Chunk;
		}
	}

	/// 先结束录音回调，再关闭队列和转换链路。
	public async partial Task<nil> Stop(CT Ct){
		if(IsDisposed){
			return NIL;
		}
		IsDisposed = true;
		Capture.DataAvailable -= OnDataAvailable;
		Capture.RecordingStopped -= OnRecordingStopped;
		try{
			Capture.StopRecording();
		}catch(InvalidOperationException){
			// 设备已经自行停止时，继续执行本地资源清理。
		}
		Chunks.Writer.TryComplete();
		await Task.CompletedTask;
		return NIL;
	}

	/// 停止后再释放 COM 和媒体转换器，避免回调访问已释放对象。
	public async partial ValueTask DisposeAsync(){
		await Stop(CancellationToken.None);
		Resampler.Dispose();
		Capture.Dispose();
		Device.Dispose();
		SessionCts.Dispose();
	}

	/// 回调不阻塞等待网络；队列已满说明消费者或网络长期落后，结束会话防止静默丢音。
	private partial void OnDataAvailable(obj? Sender, WaveInEventArgs E){
		if(IsDisposed || E.BytesRecorded <= 0){
			return;
		}
		try{
			SourceBuffer.AddSamples(E.Buffer, 0, E.BytesRecorded);
			DrainResampler();
		}catch(Exception Error){
			Chunks.Writer.TryComplete(new VoiceRecognitionException(EVoiceRecognitionErrorKind.AudioDevice, "Windows 麥克風採集失敗", InnerException: Error));
		}
	}

	/// 设备异常必须显式传给异步消费者，不能仅停留在 NAudio 回调线程。
	private partial void OnRecordingStopped(obj? Sender, StoppedEventArgs E){
		if(E.Exception is not null){
			Chunks.Writer.TryComplete(new VoiceRecognitionException(EVoiceRecognitionErrorKind.AudioDevice, "Windows 麥克風設備已停止", InnerException: E.Exception));
			return;
		}
		Chunks.Writer.TryComplete();
	}

	/// 先将重采样输出累计到目标缓冲，再逐包取出固定时长 PCM。
	private partial void DrainResampler(){
		var ResampleBuffer = new byte[ChunkByteLength];
		while(Resampler.Read(ResampleBuffer, 0, ResampleBuffer.Length) is var Read && Read > 0){
			TargetBuffer.AddSamples(ResampleBuffer, 0, Read);
			// 必須在每次重採樣讀取後立即排出完整分片。
			// 若累積到迴圈結束才排出，長回調可先撐爆 TargetBuffer，儘管下游其實可正常消費。
			while(TargetBuffer.BufferedBytes >= ChunkByteLength){
				var Buffer = new byte[ChunkByteLength];
				var Copied = TargetBuffer.Read(Buffer, 0, ChunkByteLength);
				if(Copied != ChunkByteLength){
					throw new InvalidOperationException("目標音頻緩衝沒有返回完整分片");
				}
				if(!Chunks.Writer.TryWrite(Buffer)){
					Chunks.Writer.TryComplete(new VoiceRecognitionException(EVoiceRecognitionErrorKind.Network, "語音網絡發送過慢，麥克風緩衝已滿"));
					_ = Stop(CancellationToken.None);
					return;
				}
			}
		}
	}
}
