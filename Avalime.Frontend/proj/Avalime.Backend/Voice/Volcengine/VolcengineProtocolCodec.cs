namespace Avalime.Backend.Voice.Volcengine;

/// 火山 v3 WebSocket 二進制幀編解碼器。
/// 所有整數都按官方協議使用大端；JSON 和音頻負載均可使用 GZip 壓縮。
public static partial class VolcengineProtocolCodec{
	/// 構造包含 JSON 參數的 full client request 幀。
	/// 返回新建的完整消息存儲，所有權移交給調用方，因此使用 `byte[]`。
	public static partial byte[] EncodeStartRequest(VolcengineStartRequest Request);

	/// 構造一個 audio-only request 幀；IsLast=true 時使用最後一包標誌。
	public static partial byte[] EncodeAudio(ReadOnlySpan<byte> PcmChunk, bool IsLast);

	/// 解析完整 WebSocket 二進制消息，驗證頭長、負載長度及壓縮格式。
	public static partial VolcengineProtocolMessage Decode(ReadOnlyMemory<byte> Message);

	/// 將原始負載壓縮為 GZip；返回獨立數組供 WebSocket 在異步發送期間持有。
	private static partial byte[] Compress(ReadOnlySpan<byte> Payload);

	/// 將 GZip 負載解壓為獨立數組；非壓縮消息原樣複製。
	private static partial byte[] Decompress(ReadOnlySpan<byte> Payload, EVolcengineCompression Compression);

	/// 建立固定 4 字節協議頭。
	private static partial byte[] MkHeader(
		EVolcengineMessageType MessageType
		, EVolcengineMessageFlags Flags
		, EVolcengineSerialization Serialization
		, EVolcengineCompression Compression
	);

	/// 讀取大端有符號 32 位整數，並推進當前位置。
	private static partial i32 ReadInt32(ReadOnlySpan<byte> Source, ref i32 Offset);

	/// 讀取大端無符號 32 位整數，並推進當前位置。
	private static partial u32 ReadUInt32(ReadOnlySpan<byte> Source, ref i32 Offset);

	/// 向目標緩衝寫入大端無符號 32 位整數。
	private static partial void WriteUInt32(Span<byte> Destination, u32 Value);
}
