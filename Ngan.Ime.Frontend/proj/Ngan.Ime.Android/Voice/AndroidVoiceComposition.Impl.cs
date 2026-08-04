using Android.Content;
using Ngan.Ime.Backend.Voice.Volcengine;
using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Voice;
using Ngan.Ime.UI.Views.VoiceInput;
using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCfg;

namespace Ngan.Ime.Android.Voice;

public static partial class AndroidVoiceComposition{
	/// 配置映射與 Windows 保持一致，Android 只替換平台音頻來源。
	public static partial VolcengineVoiceRecognitionOptions ReadVolcengineOptions(){
		return new VolcengineVoiceRecognitionOptions{
			ApiKey = KeysCfg.VoiceInput.Volcengine.ApiKey.GetFrom(AppCfg.Inst) ?? "",
			AppId = KeysCfg.VoiceInput.Volcengine.AppId.GetFrom(AppCfg.Inst) ?? "",
			AccessToken = KeysCfg.VoiceInput.Volcengine.AccessToken.GetFrom(AppCfg.Inst) ?? "",
			Endpoint = KeysCfg.VoiceInput.Volcengine.Endpoint.GetFrom(AppCfg.Inst) ?? "",
			ResourceId = KeysCfg.VoiceInput.Volcengine.ResourceId.GetFrom(AppCfg.Inst) ?? "",
			ModelName = KeysCfg.VoiceInput.Volcengine.ModelName.GetFrom(AppCfg.Inst) ?? "",
			ConnectTimeout = TimeSpan.FromMilliseconds(KeysCfg.VoiceInput.Volcengine.ConnectTimeoutMs.GetFrom(AppCfg.Inst)),
			FinalResultTimeout = TimeSpan.FromMilliseconds(KeysCfg.VoiceInput.Volcengine.FinalResultTimeoutMs.GetFrom(AppCfg.Inst)),
			MaxServerMessageBytes = checked((i32)KeysCfg.VoiceInput.Volcengine.MaxServerMessageBytes.GetFrom(AppCfg.Inst)),
			EnablePunctuation = KeysCfg.VoiceInput.Volcengine.EnablePunctuation.GetFrom(AppCfg.Inst),
			EnableInverseTextNormalization = KeysCfg.VoiceInput.Volcengine.EnableInverseTextNormalization.GetFrom(AppCfg.Inst),
			ShowUtterances = KeysCfg.VoiceInput.Volcengine.ShowUtterances.GetFrom(AppCfg.Inst),
		};
	}

	/// Android AudioRecord 直接產生配置要求的 PCM，不做平台重採樣。
	public static partial AudioInputOptions ReadAudioOptions(){
		return new AudioInputOptions{
			SampleRate = KeysCfg.VoiceInput.Audio.SampleRate.GetFrom(AppCfg.Inst),
			Channels = KeysCfg.VoiceInput.Audio.Channels.GetFrom(AppCfg.Inst),
			BitsPerSample = KeysCfg.VoiceInput.Audio.BitsPerSample.GetFrom(AppCfg.Inst),
			ChunkDurationMs = KeysCfg.VoiceInput.Audio.ChunkDurationMs.GetFrom(AppCfg.Inst),
			MaxBufferedChunks = KeysCfg.VoiceInput.Audio.MaxBufferedChunks.GetFrom(AppCfg.Inst),
		};
	}

	/// 所有服務均使用可靜態分析的顯式泛型註冊，避免 NativeAOT 依賴程序集掃描。
	public static partial IServiceCollection AddAndroidVoiceInput(IServiceCollection Services, Context Context){
		ArgumentNullException.ThrowIfNull(Services);
		ArgumentNullException.ThrowIfNull(Context);
		var Options = ReadVolcengineOptions();
		var AudioOptions = ReadAudioOptions();
		var AppContext = Context.ApplicationContext ?? Context;
		Services.AddSingleton(Options);
		Services.AddSingleton(AudioOptions);
		Services.AddSingleton<IVoiceRecognitionProvider, VolcengineVoiceRecognitionProvider>();
		Services.AddSingleton<IAudioInputSource>(_ => new AndroidAudioInputSource(AppContext));
		Services.AddTransient<VmVoiceInput>();
		Services.AddTransient<VmVoiceInputLangSelector>();
		return Services;
	}
}
