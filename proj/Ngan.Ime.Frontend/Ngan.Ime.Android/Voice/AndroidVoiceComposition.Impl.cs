using Android.Content;
using Ngan.Ime.Backend.Voice.Volcengine;
using Ngan.Ime.Core.Voice;
using Ngan.Ime.UI.Views.VoiceInput;
using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCfg;

namespace Ngan.Ime.Android.Voice;

public static partial class AndroidVoiceComposition{
	/// 配置映射與 Windows 保持一致，Android 只替換平台音頻來源。
	public static partial VolcengineVoiceRecognitionOptions ReadVolcengineOptions(){
		return new VolcengineVoiceRecognitionOptions{
			ApiKey = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.ApiKey) ?? "",
			AppId = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.AppId) ?? "",
			AccessToken = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.AccessToken) ?? "",
			Endpoint = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.Endpoint) ?? "",
			ResourceId = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.ResourceId) ?? "",
			ModelName = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.ModelName) ?? "",
			ConnectTimeout = TimeSpan.FromMilliseconds(AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.ConnectTimeoutMs)),
			FinalResultTimeout = TimeSpan.FromMilliseconds(AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.FinalResultTimeoutMs)),
			MaxServerMessageBytes = checked((i32)AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.MaxServerMessageBytes)),
			EnablePunctuation = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.EnablePunctuation),
			EnableInverseTextNormalization = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.EnableInverseTextNormalization),
			ShowUtterances = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Volcengine.ShowUtterances),
		};
	}

	/// Android AudioRecord 直接產生配置要求的 PCM，不做平台重採樣。
	public static partial AudioInputOptions ReadAudioOptions(){
		return new AudioInputOptions{
			SampleRate = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Audio.SampleRate),
			Channels = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Audio.Channels),
			BitsPerSample = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Audio.BitsPerSample),
			ChunkDurationMs = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Audio.ChunkDurationMs),
			MaxBufferedChunks = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Audio.MaxBufferedChunks),
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
