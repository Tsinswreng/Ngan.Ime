using Ngan.Ime.Core.Voice;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Ngan.Ime.Windows.Voice;

public sealed partial class WindowsAudioInputSource{
	[Doc($$"""
	建構子刻意不碰 COM：NAudio 的 WASAPI 在 NativeAOT 下無法建立（`[ComImport]` 類介面），
	若在這裡就 new，桌面模式一建界面就會崩。真正的建立延到第一次錄音。
	""")]
	public partial WindowsAudioInputSource(){
	}

	[Doc($$"""
	取得（必要時建立）端點枚舉器。
	AOT 版本直接給出可讀的錯誤，而不是讓 `InvalidProgramException` 冒到界面上。
	""")]
	MMDeviceEnumerator EnsureEnumerator(){
		if(!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported){
			throw new VoiceRecognitionException(
				EVoiceRecognitionErrorKind.AudioDevice,
				"此版本以 NativeAOT 發佈，不支援 WASAPI 的 COM 互操作，故無法錄音；請改用非 AOT 版本。");
		}
		return DeviceEnumerator ??= new MMDeviceEnumerator();
	}

	[Doc($$"""
	打开默认通信麦克风，建立转换链路并在所有资源就绪后开始采集。
	""")]
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
			// 先把「本版本能不能用 WASAPI」這件事講清楚，再碰 NAudio。
			Device = EnsureEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
			Capture = new WasapiCapture(Device);
			SourceBuffer = new BufferedWaveProvider(Capture.WaveFormat){
				DiscardOnBufferOverflow = false,
				// 空輸入必須返回 0；預設補零會使重採樣器無限產生虛假的 PCM。
				ReadFully = false,
				BufferDuration = TimeSpan.FromMilliseconds(Options.ChunkDurationMs * Options.MaxBufferedChunks),
			};
			var targetFormat = new WaveFormat(Options.SampleRate, Options.BitsPerSample, Options.Channels);
			Resampler = new MediaFoundationResampler(SourceBuffer, targetFormat){ResamplerQuality = 60};
			TargetBuffer = new BufferedWaveProvider(targetFormat){
				DiscardOnBufferOverflow = false,
				ReadFully = false,
				BufferDuration = TimeSpan.FromMilliseconds(Options.ChunkDurationMs * Options.MaxBufferedChunks),
			};
			var session = new WindowsAudioInputSession(Device, Capture, SourceBuffer, Resampler, TargetBuffer, Options, Ct);
			session.Start();
			await Task.CompletedTask;
			return session;
		}catch(VoiceRecognitionException){
			throw;
		}catch(Exception Error){
			Resampler?.Dispose();
			Capture?.Dispose();
			Device?.Dispose();
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.AudioDevice, "無法開啓 Windows 麥克風", InnerException: Error);
		}
	}

	[Doc($$"""
	火山首版只支持常规 PCM，约束格式使转换后的字节长度和协议一致。
	""")]
	private partial void ValidateOptions(AudioInputOptions Options){
		if(Options.SampleRate <= 0 || Options.Channels is < 1 or > 2 || Options.BitsPerSample != 16 || Options.ChunkDurationMs is < 50 or > 1000 || Options.MaxBufferedChunks <= 0){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Configuration, "語音音頻配置無效；首版要求 16-bit PCM、50~1000ms 分包及正數緩衝包數");
		}
	}
}
