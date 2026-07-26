using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.Json;
using Avalime.Core.Voice;

namespace Avalime.Backend.Voice.Volcengine;

public static partial class VolcengineProtocolCodec{
	/// 将源生成 JSON 和 GZip 负载包装为火山 full-client 帧。
	public static partial byte[] EncodeStartRequest(VolcengineStartRequest Request){
		var Payload = JsonSerializer.SerializeToUtf8Bytes(Request, VolcengineJsonContext.Default.VolcengineStartRequest);
		var Compressed = Compress(Payload);
		var Header = MkHeader(
			EVolcengineMessageType.FullClientRequest,
			EVolcengineMessageFlags.None,
			EVolcengineSerialization.Json,
			EVolcengineCompression.Gzip
		);
		var Result = new byte[Header.Length + sizeof(u32) + Compressed.Length];
		Header.CopyTo(Result, 0);
		WriteUInt32(Result.AsSpan(Header.Length), checked((u32)Compressed.Length));
		Compressed.CopyTo(Result, Header.Length + sizeof(u32));
		return Result;
	}

	/// 将一个 PCM 分片压缩并附上普通或尾包标志。
	public static partial byte[] EncodeAudio(ReadOnlySpan<byte> PcmChunk, bool IsLast){
		var Compressed = Compress(PcmChunk);
		var Flags = IsLast ? EVolcengineMessageFlags.LastPackage : EVolcengineMessageFlags.None;
		var Header = MkHeader(
			EVolcengineMessageType.AudioOnlyRequest,
			Flags,
			EVolcengineSerialization.None,
			EVolcengineCompression.Gzip
		);
		var Result = new byte[Header.Length + sizeof(u32) + Compressed.Length];
		Header.CopyTo(Result, 0);
		WriteUInt32(Result.AsSpan(Header.Length), checked((u32)Compressed.Length));
		Compressed.CopyTo(Result, Header.Length + sizeof(u32));
		return Result;
	}

	/// 严格验证消息布局，先抽取序号/错误码，再解压负载。
	public static partial VolcengineProtocolMessage Decode(ReadOnlyMemory<byte> Message){
		var Source = Message.Span;
		if(Source.Length < 4){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Protocol, "火山响应不足 4 字节协议头");
		}

		var Version = Source[0] >> 4;
		var HeaderSize = (Source[0] & 0x0F) * 4;
		if(Version != 1 || HeaderSize < 4 || HeaderSize > Source.Length){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Protocol, "火山响应协议版本或头长度无效");
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
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Protocol, "火山响应负载长度无效");
		}

		return new VolcengineProtocolMessage{
			MessageType = Type,
			Flags = Flags,
			Serialization = Serialization,
			Compression = Compression,
			Sequence = Sequence,
			ErrorCode = ErrorCode,
			Payload = Decompress(Source.Slice(Offset), Compression),
		};
	}

	/// GZip 输出需要独立可持有存储，供 WebSocket 异步发送使用。
	private static partial byte[] Compress(ReadOnlySpan<byte> Payload){
		if(Payload.IsEmpty){
			// GZipStream 在從未寫入資料時可能不產生任何位元組。
			// 最後包雖然沒有 PCM，仍必須帶可解壓的 gzip 空流；否則服務端會報 ungzip EOF。
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

	/// 仅接受协议定义的无压缩或 GZip，其他值立即视为协议错误。
	private static partial byte[] Decompress(ReadOnlySpan<byte> Payload, EVolcengineCompression Compression){
		if(Compression == EVolcengineCompression.None){
			return Payload.ToArray();
		}
		if(Compression != EVolcengineCompression.Gzip){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Protocol, "火山响应使用了不支持的压缩方式");
		}

		using var Input = new MemoryStream(Payload.ToArray(), writable: false);
		using var Gzip = new GZipStream(Input, CompressionMode.Decompress);
		using var Output = new MemoryStream();
		Gzip.CopyTo(Output);
		return Output.ToArray();
	}

	/// 火山 v3 固定使用版本 1 和 4 字节头。
	private static partial byte[] MkHeader(
		EVolcengineMessageType MessageType
		, EVolcengineMessageFlags Flags
		, EVolcengineSerialization Serialization
		, EVolcengineCompression Compression
	){
		return [0x11, (byte)(((u8)MessageType << 4) | (u8)Flags), (byte)(((u8)Serialization << 4) | (u8)Compression), 0x00];
	}

	/// 读取前确认仍有 4 字节，避免畸形消息越界。
	private static partial i32 ReadInt32(ReadOnlySpan<byte> Source, ref i32 Offset){
		if(Source.Length - Offset < sizeof(i32)){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Protocol, "火山响应缺少有符号整数");
		}
		var Value = BinaryPrimitives.ReadInt32BigEndian(Source.Slice(Offset, sizeof(i32)));
		Offset += sizeof(i32);
		return Value;
	}

	/// 读取前确认仍有 4 字节，避免畸形消息越界。
	private static partial u32 ReadUInt32(ReadOnlySpan<byte> Source, ref i32 Offset){
		if(Source.Length - Offset < sizeof(u32)){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Protocol, "火山响应缺少无符号整数");
		}
		var Value = BinaryPrimitives.ReadUInt32BigEndian(Source.Slice(Offset, sizeof(u32)));
		Offset += sizeof(u32);
		return Value;
	}

	/// 按协议要求写入大端长度字段。
	private static partial void WriteUInt32(Span<byte> Destination, u32 Value){
		if(Destination.Length < sizeof(u32)){
			throw new ArgumentException("长度字段缓冲不足", nameof(Destination));
		}
		BinaryPrimitives.WriteUInt32BigEndian(Destination, Value);
	}
}
