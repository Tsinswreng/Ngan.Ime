namespace Ngan.Ime.Backend.Voice.Volcengine;

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.Json;
using Ngan.Ime.Core.Voice;

[Doc($$"""
{{nameof(VolcengineProtocolCodec)}} 的實現。所有偏移與長度都先驗證再讀，畸形消息一律拋
{{nameof(VoiceRecognitionException)}}（Kind=Protocol），不讓越界或解壓炸彈流進上層。
""")]
public static partial class VolcengineProtocolCodec{

	[Doc("把源生成的 JSON 參數包成 full client request 幀（GZip 負載）。")]
	public static partial byte[] EncodeStartRequest(VolcengineStartRequest Request){
		var Payload = JsonSerializer.SerializeToUtf8Bytes(Request, VolcengineJsonContext.Default.VolcengineStartRequest);
		var Compressed = Compress(Payload);
		return MkFrame(
			EVolcengineMessageType.FullClientRequest
			,EVolcengineMessageFlags.None
			,EVolcengineSerialization.Json
			,EVolcengineCompression.Gzip
			,Compressed
		);
	}

	[Doc("把一個 PCM 分片壓縮成 audio-only 幀；IsLast 決定是否帶最後一包標誌。")]
	public static partial byte[] EncodeAudio(ReadOnlySpan<byte> PcmChunk, bool IsLast){
		var Compressed = Compress(PcmChunk);
		var Flags = IsLast ? EVolcengineMessageFlags.LastPackage : EVolcengineMessageFlags.None;
		return MkFrame(
			EVolcengineMessageType.AudioOnlyRequest
			,Flags
			,EVolcengineSerialization.None
			,EVolcengineCompression.Gzip
			,Compressed
		);
	}

	[Doc("拼出「頭 ＋ 長度 ＋ 負載」的完整消息。")]
	static byte[] MkFrame(
		EVolcengineMessageType MessageType
		,EVolcengineMessageFlags Flags
		,EVolcengineSerialization Serialization
		,EVolcengineCompression Compression
		,byte[] Payload
	){
		var Header = MkHeader(MessageType, Flags, Serialization, Compression);
		var Result = new byte[Header.Length + sizeof(u32) + Payload.Length];
		Header.CopyTo(Result, 0);
		WriteUInt32(Result.AsSpan(Header.Length), checked((u32)Payload.Length));
		Payload.CopyTo(Result, Header.Length + sizeof(u32));
		return Result;
	}

	[Doc("嚴格驗證消息佈局：先抽序號／錯誤碼，再驗負載長度，最後解壓。")]
	public static partial VolcengineProtocolMessage Decode(ReadOnlyMemory<byte> Message, i32 MaxPayloadBytes){
		var Source = Message.Span;
		if(Source.Length < 4){
			throw ProtocolErr("response shorter than the 4-byte protocol header");
		}

		var Version = Source[0] >> 4;
		var HeaderSize = (Source[0] & 0x0F) * 4;
		if(Version != 1 || HeaderSize < 4 || HeaderSize > Source.Length){
			throw ProtocolErr("invalid protocol version or header length");
		}

		var Type = (EVolcengineMessageType)(Source[1] >> 4);
		var Flags = (EVolcengineMessageFlags)(Source[1] & 0x0F);
		var Serialization = (EVolcengineSerialization)(Source[2] >> 4);
		var Compression = (EVolcengineCompression)(Source[2] & 0x0F);
		var Offset = HeaderSize;
		i32? Sequence = null;
		u32? ErrorCode = null;

		if(Type == EVolcengineMessageType.ServerError){
			ErrorCode = ReadUInt32(Source, ref Offset);
		}else if(Flags is EVolcengineMessageFlags.HasSequence or EVolcengineMessageFlags.HasNegativeSequence){
			Sequence = ReadInt32(Source, ref Offset);
		}

		var PayloadSize = ReadUInt32(Source, ref Offset);
		if(PayloadSize > int.MaxValue || Source.Length - Offset != (i32)PayloadSize){
			throw ProtocolErr("invalid payload length");
		}

		return new VolcengineProtocolMessage{
			MessageType = Type,
			Flags = Flags,
			Serialization = Serialization,
			Compression = Compression,
			Sequence = Sequence,
			ErrorCode = ErrorCode,
			Payload = Decompress(Source.Slice(Offset), Compression, MaxPayloadBytes),
		};
	}

	[Doc("造一個協議錯誤；訊息固定英文（本層不負責面向用戶的文案）。")]
	static VoiceRecognitionException ProtocolErr(str Detail){
		return new VoiceRecognitionException(
			EVoiceRecognitionErrorKind.Protocol
			,"Volcengine protocol error: " + Detail + "."
		);
	}

	[Doc("GZip 壓縮；空輸入要回一段可解壓的空 gzip 流（服務端會檢查 ungzip）。")]
	private static partial byte[] Compress(ReadOnlySpan<byte> Payload){
		if(Payload.IsEmpty){
			// GZipStream 在從未寫入資料時可能不產生任何位元組；最後包雖然沒有 PCM，
			// 仍必須帶可解壓的 gzip 空流，否則服務端會報 ungzip EOF。
			return [
				0x1F, 0x8B, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF,
				0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			];
		}
		using var Output = new MemoryStream();
		using(var Gzip = new GZipStream(Output, CompressionLevel.Fastest, leaveOpen: true)){
			Gzip.Write(Payload);
		}
		return Output.ToArray();
	}

	[Doc("解壓；邊讀邊計數，超過上限立即拋（防 gzip 炸彈）。僅接受 None 與 Gzip。")]
	private static partial byte[] Decompress(ReadOnlySpan<byte> Payload, EVolcengineCompression Compression, i32 MaxPayloadBytes){
		if(Compression == EVolcengineCompression.None){
			if(Payload.Length > MaxPayloadBytes){
				throw ProtocolErr("uncompressed payload exceeds the configured limit");
			}
			return Payload.ToArray();
		}
		if(Compression != EVolcengineCompression.Gzip){
			throw ProtocolErr("unsupported compression method");
		}

		using var Input = new MemoryStream(Payload.ToArray(), writable: false);
		using var Gzip = new GZipStream(Input, CompressionMode.Decompress);
		using var Output = new MemoryStream();
		var Buffer = new byte[8192];
		while(true){
			var Read = Gzip.Read(Buffer, 0, Buffer.Length);
			if(Read <= 0){
				break;
			}
			if(Output.Length + Read > MaxPayloadBytes){
				throw ProtocolErr("decompressed payload exceeds the configured limit");
			}
			Output.Write(Buffer, 0, Read);
		}
		return Output.ToArray();
	}

	[Doc("協議頭：版本固定 1，故首字節 0x11；第 4 字節保留為 0。")]
	private static partial byte[] MkHeader(
		EVolcengineMessageType MessageType
		, EVolcengineMessageFlags Flags
		, EVolcengineSerialization Serialization
		, EVolcengineCompression Compression
	){
		return [
			0x11,
			(byte)(((u8)MessageType << 4) | (u8)Flags),
			(byte)(((u8)Serialization << 4) | (u8)Compression),
			0x00,
		];
	}

	[Doc("讀 4 字節大端有符號整數；不足 4 字節拋協議錯誤。")]
	private static partial i32 ReadInt32(ReadOnlySpan<byte> Source, ref i32 Offset){
		if(Source.Length - Offset < sizeof(i32)){
			throw ProtocolErr("truncated signed 32-bit field");
		}
		var Value = BinaryPrimitives.ReadInt32BigEndian(Source.Slice(Offset, sizeof(i32)));
		Offset += sizeof(i32);
		return Value;
	}

	[Doc("讀 4 字節大端無符號整數；不足 4 字節拋協議錯誤。")]
	private static partial u32 ReadUInt32(ReadOnlySpan<byte> Source, ref i32 Offset){
		if(Source.Length - Offset < sizeof(u32)){
			throw ProtocolErr("truncated unsigned 32-bit field");
		}
		var Value = BinaryPrimitives.ReadUInt32BigEndian(Source.Slice(Offset, sizeof(u32)));
		Offset += sizeof(u32);
		return Value;
	}

	[Doc("寫 4 字節大端無符號整數；緩衝不足屬程式錯誤，直接拋參數異常。")]
	private static partial void WriteUInt32(Span<byte> Destination, u32 Value){
		if(Destination.Length < sizeof(u32)){
			throw new ArgumentException("Destination is shorter than 4 bytes.", nameof(Destination));
		}
		BinaryPrimitives.WriteUInt32BigEndian(Destination, Value);
	}
}
