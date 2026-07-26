using System.Net.WebSockets;
using Avalime.Core.Language;
using Avalime.Core.Voice;

namespace Avalime.Backend.Voice.Volcengine;

public sealed partial class VolcengineVoiceRecognitionProvider{
	/// 建立配置快照和固定语言目录，避免运行中修改可见能力集合。
	public partial VolcengineVoiceRecognitionProvider(VolcengineVoiceRecognitionOptions Options){
		this.Options = Options ?? throw new ArgumentNullException(nameof(Options));
		SupportedLanguages = MkSupportedLanguages();
	}

	/// 依次校验、连接、发送首包，并把已握手连接交给会话对象。
	public async partial Task<IVoiceRecognitionSession> StartSession(VoiceRecognitionRequest Request, CT Ct){
		ArgumentNullException.ThrowIfNull(Request);
		ValidateOptions();
		var Language = ValidateLanguage(Request.Language);
		var RequestId = Guid.NewGuid().ToString();
		ClientWebSocket? Socket = null;

		try{
			Socket = await Connect(RequestId, Ct);
			var StartRequest = MkStartRequest(Request);
			StartRequest.audio.language = Language;
			await SendStartRequest(Socket, StartRequest, Ct);
			var TraceId = GetTraceId(Socket, RequestId);
			return new VolcengineVoiceRecognitionSession(Socket, TraceId, Options, Ct);
		}catch(Exception Error) when(Error is not VoiceRecognitionException){
			Socket?.Dispose();
			throw MapConnectionException(Error, RequestId);
		}
	}

	/// 将 Core 请求的 PCM 格式和配置映射为火山首包。
	private partial VolcengineStartRequest MkStartRequest(VoiceRecognitionRequest Request){
		return new VolcengineStartRequest{
			user = new(){uid = Guid.NewGuid().ToString()},
			audio = new(){
				// 這是裸 16-bit PCM 分片；火山的 format 名稱為 pcm，raw 僅是 codec 的預設值。
				format = "pcm",
				codec = "raw",
				rate = Request.Audio.SampleRate,
				bits = Request.Audio.BitsPerSample,
				channel = Request.Audio.Channels,
				language = Request.Language.Code,
			},
			request = new(){
				model_name = Options.ModelName,
				enable_itn = Options.EnableInverseTextNormalization,
				enable_punc = Options.EnablePunctuation,
				show_utterances = Options.ShowUtterances,
			},
		};
	}

	/// 新旧控制台使用不同认证头；资源和关联标识两种模式共用。
	private partial void SetAuthorizationHeaders(ClientWebSocket Socket, str RequestId){
		Socket.Options.SetRequestHeader("X-Api-Resource-Id", Options.ResourceId);
		Socket.Options.SetRequestHeader("X-Api-Connect-Id", RequestId);
		// 火山 WebSocket 握手只接受其文档列出的认证与连接标识头。
		// Request-Id / Sequence 属于其他历史协议，不应混入 v3 握手而影响网关鉴权。
		if(!str.IsNullOrWhiteSpace(Options.ApiKey)){
			Socket.Options.SetRequestHeader("X-Api-Key", Options.ApiKey);
			return;
		}
		Socket.Options.SetRequestHeader("X-Api-App-Key", Options.AppId);
		Socket.Options.SetRequestHeader("X-Api-Access-Key", Options.AccessToken);
	}

	/// 将配置超时和调用取消合并，避免连接永久悬挂。
	private async partial Task<ClientWebSocket> Connect(str RequestId, CT Ct){
		var Socket = new ClientWebSocket();
		SetAuthorizationHeaders(Socket, RequestId);
		using var TimeoutCts = new CancellationTokenSource(Options.ConnectTimeout);
		using var LinkedCts = CancellationTokenSource.CreateLinkedTokenSource(Ct, TimeoutCts.Token);
		try{
			await Socket.ConnectAsync(new Uri(Options.Endpoint), LinkedCts.Token);
			return Socket;
		}catch{
			Socket.Dispose();
			throw;
		}
	}

	/// 首包必须在任何音频包之前完整发送。
	private async partial Task<nil> SendStartRequest(ClientWebSocket Socket, VolcengineStartRequest Request, CT Ct){
		var Frame = VolcengineProtocolCodec.EncodeStartRequest(Request);
		await Socket.SendAsync(Frame, WebSocketMessageType.Binary, true, Ct);
		return NIL;
	}

	/// X-Tt-Logid 是服务端排查所需标识，缺失时保留本地请求 ID。
	private partial str GetTraceId(ClientWebSocket Socket, str RequestId){
		if(Socket.HttpResponseHeaders?.TryGetValue("X-Tt-Logid", out var Values) == true){
			var TraceId = Values.FirstOrDefault();
			if(!str.IsNullOrWhiteSpace(TraceId)){
				return TraceId;
			}
		}
		return RequestId;
	}

	/// 取消保持原语义；其余异常按连接失败统一提供可读诊断。
	private partial VoiceRecognitionException MapConnectionException(Exception Error, str RequestId){
		if(Error is OperationCanceledException){
			return new VoiceRecognitionException(EVoiceRecognitionErrorKind.Cancelled, "語音識別連接已取消", TraceId: RequestId, InnerException: Error);
		}
		return new VoiceRecognitionException(EVoiceRecognitionErrorKind.Network, "無法連接火山語音識別服務", TraceId: RequestId, InnerException: Error);
	}

	/// 在网络访问前尽早暴露空凭据、错误端点及非法限制值。
	private partial void ValidateOptions(){
		if(str.IsNullOrWhiteSpace(Options.ApiKey) && (str.IsNullOrWhiteSpace(Options.AppId) || str.IsNullOrWhiteSpace(Options.AccessToken))){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Configuration, "請在 VoiceInput.Volcengine 配置 ApiKey，或同時配置 AppId 與 AccessToken");
		}
		if(!Uri.TryCreate(Options.Endpoint, UriKind.Absolute, out var Endpoint) || Endpoint.Scheme != Uri.UriSchemeWss){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Configuration, "火山識別 Endpoint 必須是 wss 地址");
		}
		if(str.IsNullOrWhiteSpace(Options.ResourceId) || str.IsNullOrWhiteSpace(Options.ModelName) || Options.ConnectTimeout <= TimeSpan.Zero || Options.FinalResultTimeout <= TimeSpan.Zero || Options.MaxServerMessageBytes <= 0){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Configuration, "火山識別配置不完整或超時/消息大小無效");
		}
	}

	/// Provider 只接受实际目录中的 BCP 47 标签，防止把前端任意文本送入第三方协议。
	private partial str ValidateLanguage(INormLang Language){
		ArgumentNullException.ThrowIfNull(Language);
		if(Language.Type != ELangIdentType.Bcp47 || str.IsNullOrWhiteSpace(Language.Code)){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.UnsupportedLanguage, "語音識別語言必須是受支持的 BCP 47 代碼");
		}
		foreach(var Candidate in SupportedLanguages){
			if(str.Equals(Candidate.Code, Language.Code, StringComparison.OrdinalIgnoreCase)){
				return Candidate.Code;
			}
		}
		throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.UnsupportedLanguage, $"火山流式識別不支持語言：{Language.Code}");
	}

	/// 首轮目录覆盖用户已知语言；增加语言仅需扩展此目录，不改变 UI。
	private static partial IReadOnlyList<INormLang> MkSupportedLanguages(){
		return [
			new NormLangDetail{Code = "zh-CN", NativeName = "中文（中國）", EnglishName = "Chinese (China)", Weight = 100},
			new NormLangDetail{Code = "en-US", NativeName = "English (United States)", EnglishName = "English (United States)", Weight = 90},
			new NormLangDetail{Code = "ja-JP", NativeName = "日本語（日本）", EnglishName = "Japanese (Japan)", Weight = 80},
			new NormLangDetail{Code = "es-MX", NativeName = "Español (México)", EnglishName = "Spanish (Mexico)", Weight = 70},
			new NormLangDetail{Code = "it-IT", NativeName = "Italiano (Italia)", EnglishName = "Italian (Italy)", Weight = 60},
			new NormLangDetail{Code = "fr-FR", NativeName = "Français (France)", EnglishName = "French (France)", Weight = 50},
			new NormLangDetail{Code = "de-DE", NativeName = "Deutsch (Deutschland)", EnglishName = "German (Germany)", Weight = 40},
			new NormLangDetail{Code = "zh-TW", NativeName = "中文（臺灣）", EnglishName = "Chinese (Taiwan)", Weight = 30},
		];
	}
}
