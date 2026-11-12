namespace Ngan.Ime.Backend.Voice.Volcengine;

using Tsinswreng.CsCore;

[Doc($$"""
火山 v3 WebSocket 二進制幀的編解碼器。

*協議要點*：4 字節頭（版本／消息型別／序列化／壓縮），其後可選 4 字節序號或錯誤碼，
再其後是 4 字節負載長度與負載本體；整數一律大端。JSON 與音頻負載都可用 GZip。

*為何全部是靜態純函數*：編解碼沒有狀態，也不碰網絡；這樣才能被單測覆蓋
（協議改版時，這裡是唯一的防線）。所有權一律移交呼叫方，故回傳 `byte[]`。

*解壓上限*：{{nameof(Decode)}} 必須帶 `MaxPayloadBytes`，否則一個 gzip 炸彈就能把行程的記憶體打爆；
`MaxServerMessageBytes` 只約束壓縮前的大小。

#Eg[
	```cs
	var Msg = VolcengineProtocolCodec.Decode(Raw, Options.MaxServerMessageBytes);
	var Frame = VolcengineProtocolCodec.EncodeAudio(Pcm, IsLast: false);
	```
]
""")]
public static partial class VolcengineProtocolCodec{

	[Doc("構造包含 JSON 參數的 full client request 幀；回傳新數組，所有權歸呼叫方。")]
	public static partial byte[] EncodeStartRequest(VolcengineStartRequest Request);

	[Doc("構造一個 audio-only request 幀；IsLast=true 時帶最後一包標誌。")]
	public static partial byte[] EncodeAudio(ReadOnlySpan<byte> PcmChunk, bool IsLast);

	[Doc($$"""
	解析一條完整的 WebSocket 二進制消息，驗證頭長、負載長度與壓縮方式。
	#Prm[{{nameof(Message)}}][完整消息。]
	#Prm[{{nameof(MaxPayloadBytes)}}][解壓後負載的上限；超過即視為協議錯誤。]
	#Rtn[解析結果；負載已解壓。]
	#Throw[VoiceRecognitionException][頭、長度或壓縮方式不合法時。]
	""")]
	public static partial VolcengineProtocolMessage Decode(ReadOnlyMemory<byte> Message, i32 MaxPayloadBytes);

	[Doc("把原始負載壓縮成 GZip；回傳獨立數組供 WebSocket 在異步發送期間持有。")]
	private static partial byte[] Compress(ReadOnlySpan<byte> Payload);

	[Doc("把 GZip 負載解壓成獨立數組；非壓縮消息原樣複製。超過上限時拋協議錯誤。")]
	private static partial byte[] Decompress(ReadOnlySpan<byte> Payload, EVolcengineCompression Compression, i32 MaxPayloadBytes);

	[Doc("建立固定 4 字節協議頭。")]
	private static partial byte[] MkHeader(
		EVolcengineMessageType MessageType
		, EVolcengineMessageFlags Flags
		, EVolcengineSerialization Serialization
		, EVolcengineCompression Compression
	);

	[Doc("讀取大端有符號 32 位整數並推進游標；不足 4 字節時拋協議錯誤。")]
	private static partial i32 ReadInt32(ReadOnlySpan<byte> Source, ref i32 Offset);

	[Doc("讀取大端無符號 32 位整數並推進游標；不足 4 字節時拋協議錯誤。")]
	private static partial u32 ReadUInt32(ReadOnlySpan<byte> Source, ref i32 Offset);

	[Doc("向目標緩衝寫入大端無符號 32 位整數。")]
	private static partial void WriteUInt32(Span<byte> Destination, u32 Value);
}
