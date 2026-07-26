using Android.Content;
using Android.Media;
using Avalime.Core.Voice;

namespace Avalime.Android.Voice;

public sealed partial class AndroidAudioInputSource{
	/// 保存進程級上下文，確保音頻服務不延長任何界面組件的生命週期。
	public partial AndroidAudioInputSource(Context Context){
		ArgumentNullException.ThrowIfNull(Context);
		this.Context = Context.ApplicationContext ?? Context;
	}

	/// 無權限時先打開權限 Activity，再以穩定錯誤契約結束本次會話。
	public partial Task<IAudioInputSession> Start(AudioInputOptions Options, CT Ct){
		ArgumentNullException.ThrowIfNull(Options);
		Ct.ThrowIfCancellationRequested();
		ValidateOptions(Options);
		if(!AndroidVoicePermission.IsGranted(Context)){
			AndroidVoicePermission.Request(Context);
			throw new VoiceRecognitionException(
				EVoiceRecognitionErrorKind.MicrophonePermission,
				"請授予 Avalime 麥克風權限，返回輸入法後再次點擊錄音"
			);
		}

		try{
			IAudioInputSession Session = new AndroidAudioInputSession(Options, Ct);
			return Task.FromResult(Session);
		}catch(Exception Error) when(Error is not VoiceRecognitionException){
			throw new VoiceRecognitionException(
				EVoiceRecognitionErrorKind.AudioDevice,
				"無法啓動 Android 麥克風",
				InnerException: Error
			);
		}
	}

	/// 固定格式使 Android 不需要平台重採樣器，也確保傳給後端的字節序與 Windows 一致。
	private static partial void ValidateOptions(AudioInputOptions Options){
		if(Options.SampleRate != 16000 || Options.Channels != 1 || Options.BitsPerSample != 16){
			throw new VoiceRecognitionException(
				EVoiceRecognitionErrorKind.Configuration,
				"Android 語音輸入目前只支持 16000 Hz、16-bit、單聲道 PCM"
			);
		}
		if(Options.ChunkDurationMs <= 0 || Options.MaxBufferedChunks <= 0){
			throw new VoiceRecognitionException(
				EVoiceRecognitionErrorKind.Configuration,
				"Android 語音分片時長和緩衝包數必須大於零"
			);
		}
	}
}
