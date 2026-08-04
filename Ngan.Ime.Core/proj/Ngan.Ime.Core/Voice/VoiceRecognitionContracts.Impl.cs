namespace Ngan.Ime.Core.Voice;

public partial class VoiceRecognitionException{
	/// 保存统一分类和诊断数据，并把底层异常保留在标准异常链中。
	public partial VoiceRecognitionException(
		EVoiceRecognitionErrorKind Kind
		, str Message
		, str ProviderCode
		, str TraceId
		, Exception? InnerException
	) : base(Message, InnerException){
		this.Kind = Kind;
		this.ProviderCode = ProviderCode;
		this.TraceId = TraceId;
	}
}
