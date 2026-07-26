using Avalime.Backend.Voice.Volcengine;
using Avalime.Core.Infra;
using Avalime.Core.Voice;
using Avalime.UI.Views.VoiceInput;
using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCfg;

namespace Avalime.Windows.Voice;

public static partial class WindowsVoiceComposition{
	/// 从既有双源配置读取火山项，并把毫秒数转换为 TimeSpan。
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

	/// 音频选项与配置文件一一对应，作为单例快照注入 VM 和音频源。
	public static partial AudioInputOptions ReadAudioOptions(){
		return new AudioInputOptions{
			SampleRate = KeysCfg.VoiceInput.Audio.SampleRate.GetFrom(AppCfg.Inst),
			Channels = KeysCfg.VoiceInput.Audio.Channels.GetFrom(AppCfg.Inst),
			BitsPerSample = KeysCfg.VoiceInput.Audio.BitsPerSample.GetFrom(AppCfg.Inst),
			ChunkDurationMs = KeysCfg.VoiceInput.Audio.ChunkDurationMs.GetFrom(AppCfg.Inst),
			MaxBufferedChunks = KeysCfg.VoiceInput.Audio.MaxBufferedChunks.GetFrom(AppCfg.Inst),
		};
	}

	/// 平台层选择 Windows 音频实现和火山后端，UI 只请求 Core 抽象。
	public static partial IServiceCollection AddWindowsVoiceInput(IServiceCollection Services){
		var Options = ReadVolcengineOptions();
		var AudioOptions = ReadAudioOptions();
		Services.AddSingleton(Options);
		Services.AddSingleton(AudioOptions);
		Services.AddSingleton<IVoiceRecognitionProvider, VolcengineVoiceRecognitionProvider>();
		Services.AddSingleton<IAudioInputSource, WindowsAudioInputSource>();
		return Services;
	}

	/// 预留组合根校验；当前 DI 在 Main 统一 BuildServiceProvider 后才能完成验证。
	private static partial void ValidateRegistration(IServiceProvider Services){
		_ = Services.GetRequiredService<IVoiceRecognitionProvider>();
		_ = Services.GetRequiredService<IAudioInputSource>();
		_ = Services.GetRequiredService<AudioInputOptions>();
	}
}
