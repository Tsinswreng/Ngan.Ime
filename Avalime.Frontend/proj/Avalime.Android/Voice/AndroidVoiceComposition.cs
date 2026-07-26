using Android.Content;
using Avalime.Backend.Voice.Volcengine;
using Avalime.Core.Voice;
using Microsoft.Extensions.DependencyInjection;

namespace Avalime.Android.Voice;

/// Android 語音輸入組合根。
/// 普通 Activity 與 InputMethodService 必須共同調用，避免兩套 DI 容器產生功能差異。
public static partial class AndroidVoiceComposition{
	/// 從跨平台配置節點建立火山 Provider 配置快照。
	public static partial VolcengineVoiceRecognitionOptions ReadVolcengineOptions();

	/// 從跨平台配置節點建立 Android PCM 配置快照。
	public static partial AudioInputOptions ReadAudioOptions();

	/// 註冊火山後端、Android AudioRecord 來源及語音頁 ViewModel 所需依賴。
	public static partial IServiceCollection AddAndroidVoiceInput(IServiceCollection Services, Context Context);
}
