using Avalime.Backend.Voice.Volcengine;
using Microsoft.Extensions.DependencyInjection;

namespace Avalime.Windows.Voice;

/// Windows 語音輸入的配置讀取與依賴注入組合根。
/// 下一階段只需在 Impl 中填寫配置映射和服務註冊，不再新增後端類型或方法聲明。
public static partial class WindowsVoiceComposition{
	/// 從 Avalime.Rw.jsonc 的 VoiceInput.Volcengine 節點讀取火山配置。
	public static partial VolcengineVoiceRecognitionOptions ReadVolcengineOptions();

	/// 從 VoiceInput.Audio 節點讀取跨平臺 PCM 格式。
	public static partial Avalime.Core.Voice.AudioInputOptions ReadAudioOptions();

	/// 註冊 Windows WASAPI 音頻源、火山 Provider 和語音輸入 ViewModel 所需依賴。
	public static partial IServiceCollection AddWindowsVoiceInput(IServiceCollection Services);

	/// 確認組合根能解析跨平臺契約，避免運行到點擊錄音時才發現漏註冊。
	private static partial void ValidateRegistration(IServiceProvider Services);
}
