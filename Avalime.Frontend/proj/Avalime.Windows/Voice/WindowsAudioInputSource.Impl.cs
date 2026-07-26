using Avalime.Core.Voice;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Avalime.Windows.Voice;

public sealed partial class WindowsAudioInputSource{
	/// 复用一个端点枚举器，避免每次录音创建 COM 枚举器。
	public partial WindowsAudioInputSource(){
		DeviceEnumerator = new MMDeviceEnumerator();
	}

	/// 打开默认通信麦克风，建立转换链路并在所有资源就绪后开始采集。
	public async partial Task<IAudioInputSession> Start(AudioInputOptions Options, CT Ct){
		Ct.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(Options);
		ValidateOptions(Options);
		MMDevice? Device = null;
		WasapiCapture? Capture = null;
		BufferedWaveProvider? SourceBuffer = null;
		MediaFoundationResampler? Resampler = null;
		BufferedWaveProvider? TargetBuffer = null;

		try{
			Device = DeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
			Capture = new WasapiCapture(Device);
			SourceBuffer = new BufferedWaveProvider(Capture.WaveFormat){
				DiscardOnBufferOverflow = false,
				// 空輸入必須返回 0；預設補零會使重採樣器無限產生虛假的 PCM。
				ReadFully = false,
				BufferDuration = TimeSpan.FromMilliseconds(Options.ChunkDurationMs * Options.MaxBufferedChunks),
			};
			var TargetFormat = new WaveFormat(Options.SampleRate, Options.BitsPerSample, Options.Channels);
			Resampler = new MediaFoundationResampler(SourceBuffer, TargetFormat){ResamplerQuality = 60};
			TargetBuffer = new BufferedWaveProvider(TargetFormat){
				DiscardOnBufferOverflow = false,
				ReadFully = false,
				BufferDuration = TimeSpan.FromMilliseconds(Options.ChunkDurationMs * Options.MaxBufferedChunks),
			};
			var Session = new WindowsAudioInputSession(Device, Capture, SourceBuffer, Resampler, TargetBuffer, Options, Ct);
			Session.Start();
			await Task.CompletedTask;
			return Session;
		}catch(VoiceRecognitionException){
			throw;
		}catch(Exception Error){
			Resampler?.Dispose();
			Capture?.Dispose();
			Device?.Dispose();
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.AudioDevice, "無法開啓 Windows 麥克風", InnerException: Error);
		}
	}

	/// 火山首版只支持常规 PCM，约束格式使转换后的字节长度和协议一致。
	private partial void ValidateOptions(AudioInputOptions Options){
		if(Options.SampleRate <= 0 || Options.Channels is < 1 or > 2 || Options.BitsPerSample != 16 || Options.ChunkDurationMs is < 50 or > 1000 || Options.MaxBufferedChunks <= 0){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Configuration, "語音音頻配置無效；首版要求 16-bit PCM、50~1000ms 分包及正數緩衝包數");
		}
	}
}
