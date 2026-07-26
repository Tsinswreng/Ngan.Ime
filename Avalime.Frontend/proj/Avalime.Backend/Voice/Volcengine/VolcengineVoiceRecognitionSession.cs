using System.Net.WebSockets;
using System.Threading.Channels;
using Avalime.Core.Voice;

namespace Avalime.Backend.Voice.Volcengine;

/// 一次火山 v3 WebSocket 語音識別會話。
/// 音頻發送由調用方串行執行，接收循環在後台運行並把結果寫入單消費者 Channel。
public sealed partial class VolcengineVoiceRecognitionSession : IVoiceRecognitionSession{
	/// 已完成握手的 WebSocket。
	readonly ClientWebSocket Socket;

	/// 服務端結果的有界生命週期隊列；接收循環是唯一寫入者，UI 管線是唯一讀取者。
	readonly Channel<VoiceRecognitionResult> Results;

	/// 控制接收循環和整個會話生命週期。
	readonly CancellationTokenSource SessionCts;

	/// 保證首包、音頻包和最後包不會併發寫入同一 WebSocket。
	readonly SemaphoreSlim SendLock;

	/// 收到服務端最終包、錯誤或取消時完成，供 CompleteAudio 等待尾包。
	readonly TaskCompletionSource<nil> FinalResultCompletion;

	/// 當前 Provider 配置快照，提供尾包超時和最大響應大小限制。
	readonly VolcengineVoiceRecognitionOptions Options;

	/// 後台 WebSocket 接收任务。
	readonly Task<nil> ReceiveTask;

	/// 握手响应中的 X-Tt-Logid，供异常诊断及 UI 错误信息引用。
	public str TraceId{get;}

	/// 是否已经发送最后一个音频包。
	public bool IsAudioCompleted{get;private set;}

	/// 是否已经完成停止或释放流程。
	public bool IsDisposed{get;private set;}

	/// 包装一个已经成功握手的 WebSocket，并立即启动唯一接收循环。
	public partial VolcengineVoiceRecognitionSession(
		ClientWebSocket Socket
		, str TraceId
		, VolcengineVoiceRecognitionOptions Options
		, CT Ct
	);

	/// 发送普通 audio-only 帧；完成音频后禁止继续发送。
	public partial Task<nil> SendAudio(ReadOnlyMemory<byte> PcmChunk, CT Ct);

	/// 发送带最后包标志的空音频帧，并等待接收循环产生最终结果。
	public partial Task<nil> CompleteAudio(CT Ct);

	/// 单次消费识别结果队列。
	public partial IAsyncEnumerable<VoiceRecognitionResult> ReadResults(CT Ct);

	/// 取消远端会话并结束结果队列；可重复调用。
	public partial Task<nil> Stop(CT Ct);

	/// 释放会话，与 Stop 具有相同的幂等语义。
	public partial ValueTask DisposeAsync();

	/// 持续组装 WebSocket 消息、解析协议帧并写入文本快照。
	private partial Task<nil> ReceiveLoop(CT Ct);

	/// 接收一条可能跨多个 WebSocket frame 的完整二进制消息。
	private partial Task<byte[]> ReceiveMessage(CT Ct);

	/// 把服务端协议消息转换为供 UI 消费的稳定段/临时段快照。
	private partial VoiceRecognitionResult? ConvertResult(VolcengineProtocolMessage Message);

	/// 解析服务端错误帧并生成包含错误码和 TraceId 的异常。
	private partial Exception MkServerException(VolcengineProtocolMessage Message);

	/// 在 SendLock 保護下發送一條完整二進制 WebSocket 消息。
	private partial Task<nil> SendMessage(ReadOnlyMemory<byte> Message, CT Ct);
}
