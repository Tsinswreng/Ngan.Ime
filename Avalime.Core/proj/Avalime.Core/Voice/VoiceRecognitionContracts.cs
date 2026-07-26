using Avalime.Core.Language;

namespace Avalime.Core.Voice;

/// 一次語音識別請求的供應商無關參數。
/// 語言在會話建立時固定，識別期間切換語言必須先結束當前會話。
public sealed class VoiceRecognitionRequest{
	/// 本輪使用的 BCP 47 語言標籤。
	public INormLang Language{get;set;} = new NormLangDetail();

	/// 是否要求服務端返回臨時結果；首版固定為 true 以支持流式展示。
	public bool EnableInterimResults{get;set;} = true;
}

/// 供 UI 消費的一次識別文本快照。
/// FinalizedText 是完整穩定段，InterimText 是可被下一包替換的臨時尾段。
public sealed class VoiceRecognitionResult{
	/// 至今已確認且不再改寫的完整文本。
	public str FinalizedText{get;set;} = "";

	/// 當前尚未確認的臨時尾句。
	public str InterimText{get;set;} = "";

	/// 是否代表服務端已發出本輪最終結果。
	public bool IsCompleted{get;set;}
}

/// 供應商無關的流式識別會話。
/// 實現需保證音頻發送、完成通知和結果讀取支持並發安全的會話時序。
public interface IVoiceRecognitionSession : IAsyncDisposable{
	/// 把一個 PCM 音頻分片送入服務端。
	public Task<nil> SendAudio(ReadOnlyMemory<byte> PcmChunk, CT Ct);

	/// 通知服務端音頻已結束，等待最後結果。
	public Task<nil> CompleteAudio(CT Ct);

	/// 讀取服務端返回的流式文本快照；只允許單一消費者。
	public IAsyncEnumerable<VoiceRecognitionResult> ReadResults(CT Ct);

	/// 主動取消遠端會話並釋放網絡資源。
	public Task<nil> Stop(CT Ct);
}

/// 向特定供應商建立流式語音識別會話的抽象。
/// UI 和音頻採集層只依賴此接口，不直接引用 WebSocket 或供應商 DTO。
public interface IVoiceRecognitionProvider{
	/// 返回供應商當前模型實際支持的語言目錄，順序由 Provider 決定。
	public IReadOnlyList<INormLang> SupportedLanguages{get;}

	/// 使用指定語言建立一次新的遠端識別會話。
	public Task<IVoiceRecognitionSession> StartSession(VoiceRecognitionRequest Request, CT Ct);
}
