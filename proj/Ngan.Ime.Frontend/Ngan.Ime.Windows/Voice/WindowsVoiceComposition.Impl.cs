using Ngan.Ime.Backend.Voice.Volcengine;
using Ngan.Ime.Core.Voice;
using Ngan.Ime.UI.Views.VoiceInput;
using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCfg;

namespace Ngan.Ime.Windows.Voice;

public static partial class WindowsVoiceComposition{
	[Doc($$"""
	从既有双源配置读取火山项，并把毫秒数转换为 TimeSpan。
	""")]
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

	[Doc($$"""
	音频选项与配置文件一一对应，作为单例快照注入 VM 和音频源。
	""")]
	public static partial AudioInputOptions ReadAudioOptions(){
		return new AudioInputOptions{
			SampleRate = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Audio.SampleRate),
			Channels = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Audio.Channels),
			BitsPerSample = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Audio.BitsPerSample),
			ChunkDurationMs = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Audio.ChunkDurationMs),
			MaxBufferedChunks = AppCfg.Inst.Get(KeysAppCfg.VoiceInput.Audio.MaxBufferedChunks),
		};
	}

	[Doc($$"""
	平台层选择 Windows 音频实现和火山后端，UI 只请求 Core 抽象。
	""")]
	public static partial IServiceCollection AddWindowsVoiceInput(IServiceCollection Services){
		var options = ReadVolcengineOptions();
		var audioOptions = ReadAudioOptions();
		Services.AddSingleton(options);
		Services.AddSingleton(audioOptions);
		Services.AddSingleton<IVoiceRecognitionProvider, VolcengineVoiceRecognitionProvider>();
		Services.AddSingleton<IAudioInputSource, WindowsAudioInputSource>();
		return Services;
	}

	[Doc($$"""
	预留组合根校验；当前 DI 在 Main 统一 BuildServiceProvider 后才能完成验证。
	""")]
	private static partial void ValidateRegistration(IServiceProvider Services){
		_ = Services.GetRequiredService<IVoiceRecognitionProvider>();
		_ = Services.GetRequiredService<IAudioInputSource>();
		_ = Services.GetRequiredService<AudioInputOptions>();
	}
}
