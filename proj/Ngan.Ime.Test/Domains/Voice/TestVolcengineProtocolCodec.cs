using Ngan.Ime.Backend.Voice.Volcengine;
using Ngan.Ime.Core.Voice;
using Tsinswreng.CsTreeTest;

namespace Ngan.Ime.Test.Domains.Voice;

[Doc($$"""
被測對象：`{{nameof(VolcengineProtocolCodec)}}`，火山語音協議的自造幀格式。

測四類情形：音頻幀往返解碼、尾包是可解壓的空淨荷且帶尾包標誌、
幀頭不足時報協議錯誤、解壓後超過上限時報協議錯誤。
""")]
public class TestVolcengineProtocolCodec : Tester{
	[Doc("""
	掛上本測試類的全部用例。
	""")]
	public override void Init(){
		this.AddTestCase(nameof(EncodeEtDecode_Audio_Should_RoundTrip), EncodeEtDecode_Audio_Should_RoundTrip!);
		this.AddTestCase(nameof(EncodeEtDecode_LastPackage_Should_BeEmpty), EncodeEtDecode_LastPackage_Should_BeEmpty!);
		this.AddTestCase(nameof(Decode_ShortHeader_Should_Throw), Decode_ShortHeader_Should_Throw!);
		this.AddTestCase(nameof(Decode_OverLimit_Should_Throw), Decode_OverLimit_Should_Throw!);
	}

	[Doc("""
	音頻幀解回來後，消息型別、淨荷長度與淨荷內容都要與送進去的一致。
	""")]
	public async Task<nil> EncodeEtDecode_Audio_Should_RoundTrip(obj? O){
		var T = Assert.IsTrue;
		// 1: 造一幀 4 字節音頻。
		var Frame = VolcengineProtocolCodec.EncodeAudio([1, 2, 3, 4], IsLast: false);
		// 2: 解回來逐項比對。
		var Msg = VolcengineProtocolCodec.Decode(Frame, 1024);
		T(Msg.MessageType == EVolcengineMessageType.AudioOnlyRequest, $"消息型別應為 audio-only，實際 {Msg.MessageType}");
		T(Msg.Payload.Length == 4, $"音頻負載長度應為 4，實際 {Msg.Payload.Length}");
		T(Msg.Payload.Span.SequenceEqual(new byte[]{ 1, 2, 3, 4 }), "音頻負載內容應與送進去的一致");
		return NIL;
	}

	[Doc("""
	尾包的淨荷是空，且要帶上 LastPackage 標誌，服務端才據此收尾。
	""")]
	public async Task<nil> EncodeEtDecode_LastPackage_Should_BeEmpty(obj? O){
		var T = Assert.IsTrue;
		var Last = VolcengineProtocolCodec.Decode(
			VolcengineProtocolCodec.EncodeAudio([], IsLast: true), 1024);
		T(Last.Payload.Length == 0, $"尾包淨荷應為空，實際長度 {Last.Payload.Length}");
		T((Last.Flags & EVolcengineMessageFlags.LastPackage) != 0, "尾包應帶 LastPackage 標誌");
		return NIL;
	}

	[Doc("""
	少於四字節的輸入湊不出幀頭，解碼應報協議錯誤，而不是返回垃圾資料。
	""")]
	public async Task<nil> Decode_ShortHeader_Should_Throw(obj? O){
		var T = Assert.IsTrue;
		var Threw = false;
		try{
			_ = VolcengineProtocolCodec.Decode(new byte[]{ 0x11 }, 1024);
		}catch(VoiceRecognitionException){
			Threw = true;
		}
		T(Threw, "不足四字節應拋協議錯誤");
		return NIL;
	}

	[Doc("""
	解壓後超過調用方給的上限時應報協議錯誤，避免被畸形幀撐爆內存。
	""")]
	public async Task<nil> Decode_OverLimit_Should_Throw(obj? O){
		var T = Assert.IsTrue;
		var Frame = VolcengineProtocolCodec.EncodeAudio([1, 2, 3, 4], IsLast: false);
		var Threw = false;
		try{
			// 上限壓到 2 字節，正常幀必然超限。
			_ = VolcengineProtocolCodec.Decode(Frame, 2);
		}catch(VoiceRecognitionException){
			Threw = true;
		}
		T(Threw, "解壓後超過上限應拋協議錯誤");
		return NIL;
	}
}
