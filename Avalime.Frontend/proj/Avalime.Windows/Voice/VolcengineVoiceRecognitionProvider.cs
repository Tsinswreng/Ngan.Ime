using Avalime.Core.Language;
using Avalime.Core.Voice;

namespace Avalime.Windows.Voice;

/// 火山引擎流式語音識別 Provider 聲明。
/// 對外只暴露供應商無關的 IVoiceRecognitionProvider；WebSocket 握手頭、JSON 分包和錯誤碼留在 Impl。
public sealed partial class VolcengineVoiceRecognitionProvider : IVoiceRecognitionProvider{
	/// 本 Provider 的不可變配置快照。
	public VolcengineVoiceRecognitionOptions Options{get;}

	/// 當前火山流式模型支持的語言目錄，供語言選擇頁直接展示。
	public IReadOnlyList<NormLangDetail> SupportedLanguages{get;}

	/// 使用配置建立 Provider 並初始化支持語言目錄。
	public partial VolcengineVoiceRecognitionProvider(VolcengineVoiceRecognitionOptions Options);

	/// 建立火山 WebSocket 流式識別會話；語言和臨時結果策略在握手時固定。
	public partial Task<IVoiceRecognitionSession> StartSession(VoiceRecognitionRequest Request, CT Ct);
}
