namespace Avalime.Windows.Voice;

/// 火山引擎流式語音識別配置。
/// 實現階段從 Windows JSON 配置文件綁定；密鑰禁止硬編碼進源碼或提交到版本庫。
public sealed class VolcengineVoiceRecognitionOptions{
	/// 火山引擎控制台分配的 AppId。
	public str AppId{get;set;} = "";

	/// 火山引擎控制台分配的 Access Token。
	public str AccessToken{get;set;} = "";

	/// 流式識別 WebSocket 完整地址，可由配置覆蓋以便切換區域或測試端點。
	public str Endpoint{get;set;} = "wss://openspeech.bytedance.com/api/v2/asr";

	/// 服務端使用的資源標識；不同模型/產品線可能要求不同值。
	public str ResourceId{get;set;} = "volc.bigasr.sauc.duration";

	/// 單次會話建立和握手的超時時間。
	public TimeSpan ConnectTimeout{get;set;} = TimeSpan.FromSeconds(10);
}
