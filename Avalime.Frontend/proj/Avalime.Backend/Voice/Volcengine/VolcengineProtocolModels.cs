using System.Text.Json.Serialization;

namespace Avalime.Backend.Voice.Volcengine;

/// 火山 v3 二進制協議的消息類型高四位值。
public enum EVolcengineMessageType : u8{
	FullClientRequest = 0x1,
	AudioOnlyRequest = 0x2,
	FullServerResponse = 0x9,
	ServerError = 0xF,
}

/// 火山 v3 消息類型補充標誌低四位值。
public enum EVolcengineMessageFlags : u8{
	None = 0x0,
	HasSequence = 0x1,
	LastPackage = 0x2,
	HasNegativeSequence = 0x3,
}

/// 火山 v3 負載序列化方式高四位值。
public enum EVolcengineSerialization : u8{
	None = 0x0,
	Json = 0x1,
}

/// 火山 v3 負載壓縮方式低四位值。
public enum EVolcengineCompression : u8{
	None = 0x0,
	Gzip = 0x1,
}

/// 已解析的火山二進制消息頭及負載。
public sealed class VolcengineProtocolMessage{
	/// 消息種類。
	public EVolcengineMessageType MessageType{get;set;}

	/// 消息補充標誌。
	public EVolcengineMessageFlags Flags{get;set;}

	/// 負載序列化方式。
	public EVolcengineSerialization Serialization{get;set;}

	/// 負載壓縮方式。
	public EVolcengineCompression Compression{get;set;}

	/// 服務端序號；消息不攜帶序號時為 null。
	public i32? Sequence{get;set;}

	/// 錯誤消息的服務端錯誤碼；非錯誤消息時為 null。
	public u32? ErrorCode{get;set;}

	/// 解壓後的原始負載。
	public ReadOnlyMemory<byte> Payload{get;set;}
}

/// full client request 的根 JSON 模型。
/// 成員名稱直接遵循火山 JSON 協議，不使用屬性映射維護第二套名稱。
public sealed class VolcengineStartRequest{
	/// 用戶追蹤資料。
	public VolcengineUserRequest user{get;set;} = new();

	/// 音頻格式與識別語言。
	public VolcengineAudioRequest audio{get;set;} = new();

	/// 模型及文本處理策略。
	public VolcengineRecognitionRequest request{get;set;} = new();
}

/// 火山請求中的用戶追蹤資料。
public sealed class VolcengineUserRequest{
	/// 本輪匿名用戶標識，只作服務端調用追蹤。
	public str uid{get;set;} = "";
}

/// 火山請求中的音頻格式與語言資料。
public sealed class VolcengineAudioRequest{
	/// 原始音頻容器格式。
	public str format{get;set;} = "raw";

	/// 原始 PCM 編解碼標識。
	public str codec{get;set;} = "raw";

	/// 每秒採樣數。
	public i32 rate{get;set;} = 16000;

	/// 每個採樣點的位數。
	public i32 bits{get;set;} = 16;

	/// 聲道數。
	public i32 channel{get;set;} = 1;

	/// BCP 47 語言代碼。
	public str language{get;set;} = "zh-CN";
}

/// 火山請求中的識別策略。
public sealed class VolcengineRecognitionRequest{
	/// 火山識別模型名稱。
	public str model_name{get;set;} = "bigmodel";

	/// 是否啓用數字、日期等逆文本規整。
	public bool enable_itn{get;set;}

	/// 是否啓用標點恢復。
	public bool enable_punc{get;set;}

	/// 是否返回分句及 definite 狀態。
	public bool show_utterances{get;set;}
}

/// full server response 的 JSON 根模型。
public sealed class VolcengineServerResponse{
	/// 當前完整識別結果；失敗響應可能不包含此字段。
	public VolcengineRecognitionResponse? result{get;set;}
}

/// 火山服務端當前完整識別結果。
public sealed class VolcengineRecognitionResponse{
	/// 當前整段音頻的完整識別文本。
	public str text{get;set;} = "";

	/// 按語音停頓劃分的分句集合。
	public IReadOnlyList<VolcengineUtterance> utterances{get;set;} = [];
}

/// 一個服務端分句；Definite=false 時該句仍可能在後續響應中被改寫。
public sealed class VolcengineUtterance{
	/// 分句文本。
	public str text{get;set;} = "";

	/// 分句開始時間，單位毫秒。
	public i32 start_time{get;set;}

	/// 分句結束時間，單位毫秒。
	public i32 end_time{get;set;}

	/// 是否已穩定且不會被後續響應改寫。
	public bool definite{get;set;}
}

/// AOT JSON 源生成上下文；後續 Impl 必須通過此上下文序列化，不使用運行時反射元數據。
[JsonSerializable(typeof(VolcengineStartRequest))]
[JsonSerializable(typeof(VolcengineServerResponse))]
public partial class VolcengineJsonContext : JsonSerializerContext{
}
