namespace Ngan.Ime.Backend.Voice.Volcengine;

/// 火山引擎流式語音識別配置。
/// 配置由 Ngan.Ime 的雙源 JSON 配置系統提供；任何密鑰均不得硬編碼在實現或日誌中。
public sealed class VolcengineVoiceRecognitionOptions{
	/// 新版火山控制台生成的 API Key；存在時優先使用 `X-Api-Key` 鑒權。
	public str ApiKey{get;set;} = "";

	/// 舊版控制台的 App ID；僅在 ApiKey 為空時配合 AccessToken 使用。
	public str AppId{get;set;} = "";

	/// 舊版控制台的 Access Token；僅在 ApiKey 為空時配合 AppId 使用。
	public str AccessToken{get;set;} = "";

	/// WebSocket v3 端點。
	/// 默認使用流式輸入模式，在尾包後返回最終結果，優先保障本機實測可用性與最終準確率。
	/// 若後續確認帳號側的雙向流式即時出字能力可用，可由本機配置切換其他 v3 端點。
	public str Endpoint{get;set;} = "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_nostream";

	/// 火山計費資源標識；默認為豆包流式語音識別模型 1.0 小時版。
	public str ResourceId{get;set;} = "volc.bigasr.sauc.duration";

	/// full client request 中的模型名稱。
	public str ModelName{get;set;} = "bigmodel";

	/// 建立 WebSocket 連接的最長等待時間。
	public TimeSpan ConnectTimeout{get;set;} = TimeSpan.FromSeconds(10);

	/// 停止錄音後等待服務端最終結果的最長時間。
	public TimeSpan FinalResultTimeout{get;set;} = TimeSpan.FromSeconds(15);

	/// 單條服務端 WebSocket 消息允許的最大字節數，防止錯誤端點或異常響應耗盡內存。
	public i32 MaxServerMessageBytes{get;set;} = 1024 * 1024;

	/// 是否要求服務端做標點恢復。
	public bool EnablePunctuation{get;set;} = true;

	/// 是否要求服務端做數字、日期等文本規整。
	public bool EnableInverseTextNormalization{get;set;} = true;

	/// 是否要求服務端返回 utterance 分句，以區分穩定段和臨時段。
	public bool ShowUtterances{get;set;} = true;
}
