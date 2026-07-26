using System.Collections.Generic;

namespace Avalime.Core.Voice;

/// 描述送往語音識別服務的音頻格式。
/// 第一個 Windows 實現固定輸出單聲道、16-bit、16 kHz PCM；格式獨立成契約，方便未來替換設備或平台。
public sealed class AudioInputOptions{
	/// PCM 採樣率，火山流式通用模型首版使用 16000 Hz。
	/// 就是每秒连拍 1.6万张 声音的快照。
	/// 數字越大 音越清晰 越保真 越佔空間
	public i32 SampleRate{get;set;} = 16000;

	/// PCM 聲道數；語音識別首版使用單聲道。
	/// 單聲道: 用多少隻耳來聽
	public i32 Channels{get;set;} = 1;

	/// 每個採樣點的位數；首版使用 16-bit little-endian PCM。
	// 	16位 意味着把声音的响度（音量）分成 65536个级别（从静音到最大声）。
	// 如果改成 8位，只能分 256 个级别，录出来的声音会很粗糙，有明显的“嘶嘶”底噪。
	// 为什么用 16？ 这是目前高质量音频的“标配”，能清晰录下你说话时轻声细语和大声嚷嚷的细节。
	public i32 BitsPerSample{get;set;} = 16;

	/// 每個音頻分片的建議時長，用於控制網絡延遲和服務端分包大小。
	/// 默認每 0.1 秒切一截聲音，位於火山各流式模式建議的 100~200ms 範圍內。
	public i32 ChunkDurationMs{get;set;} = 100;

	/// 麥克風回調與網絡發送之間最多緩衝的音頻包數。
	/// 隊列滿時必須以設備/網絡過慢錯誤終止，不能靜默丟棄音頻或無限增長內存。
	public i32 MaxBufferedChunks{get;set;} = 32;
}

/// 提供實時 PCM 音頻分片的跨平台輸入源。
/// 實現只負責麥克風採集，不感知火山引擎或其他識別服務協議。
public interface IAudioInputSource{
	/// 啟動一次獨立的麥克風採集會話。
	public Task<IAudioInputSession> Start(AudioInputOptions Options, CT Ct);
}

/// 一次麥克風採集會話，音頻分片只允許被消費一次。
public interface IAudioInputSession : IAsyncDisposable{
	/// 按實時順序返回 PCM 分片；結束、取消或設備錯誤時停止枚舉。
	/// 外層 `IAsyncEnumerable` 表示持續異步產生多個音頻包，內層 `ReadOnlyMemory<byte>` 表示單個包。
	/// 每個分片在該次枚舉迭代結束前保持有效；消費者必須在進入下一次迭代前完成發送，不得長期保存引用。
	public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunks(CT Ct);

	/// 主動停止採集並釋放底層設備；可重複調用。
	public Task<nil> Stop(CT Ct);
}
