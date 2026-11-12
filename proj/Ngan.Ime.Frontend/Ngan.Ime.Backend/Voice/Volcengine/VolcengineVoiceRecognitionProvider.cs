using Ngan.Ime.Core.Language;
using Ngan.Ime.Core.Voice;

namespace Ngan.Ime.Backend.Voice.Volcengine;

/// 火山引擎流式语音识别 Provider。
/// 负责配置校验、鉴权握手、语言映射及会话创建；不负责麦克风采集和 UI 状态。
public sealed partial class VolcengineVoiceRecognitionProvider : IVoiceRecognitionProvider{
	/// 本 Provider 的不可变配置快照。
	public VolcengineVoiceRecognitionOptions Options{get;}

	/// 火山当前多语种模型目录；契约统一使用 IReadOnlyList&lt;INormLang&gt;。
	public IReadOnlyList<INormLang> SupportedLanguages{get;}

	/// 建立 Provider 并创建稳定的语言目录快照。
	public partial VolcengineVoiceRecognitionProvider(VolcengineVoiceRecognitionOptions Options);

	/// 校验配置和语言，建立带火山鉴权请求头的 v3 WebSocket 会话并发送首个 JSON 请求。
	public partial Task<IVoiceRecognitionSession> StartSession(VoiceRecognitionRequest Request, CT Ct);

	/// 建立火山 full client request DTO。
	private partial VolcengineStartRequest MkStartRequest(VoiceRecognitionRequest Request);

	/// 按新版 API Key 或旧版 AppId/AccessToken 配置握手请求头。
	private partial void SetAuthorizationHeaders(System.Net.WebSockets.ClientWebSocket Socket, str RequestId);

	/// 使用連接超時建立 WebSocket，並把握手異常映射爲統一語音異常。
	private partial Task<System.Net.WebSockets.ClientWebSocket> Connect(str RequestId, CT Ct);

	/// 在會話接收循環啓動前發送 full client request 首包。
	private partial Task<nil> SendStartRequest(
		System.Net.WebSockets.ClientWebSocket Socket
		, VolcengineStartRequest Request
		, CT Ct
	);

	/// 從握手響應頭讀取 X-Tt-Logid；服務端未返回時使用請求 ID 作爲最低限度追蹤標識。
	private partial str GetTraceId(System.Net.WebSockets.ClientWebSocket Socket, str RequestId);

	/// 將連接、握手和超時異常映射爲 VoiceRecognitionException。
	private partial VoiceRecognitionException MapConnectionException(Exception Error, str RequestId);

	/// 验证端点、资源、凭据和超时参数，在发起网络连接前给出可读配置错误。
	private partial void ValidateOptions();

	/// 验证语言是否位于 SupportedLanguages，并返回规范化 BCP 47 代码。
	private partial str ValidateLanguage(INormLang Language);

	/// 创建火山官方支持语言的不可变目录，常用语言排在前面。
	private static partial IReadOnlyList<INormLang> MkSupportedLanguages();
}
