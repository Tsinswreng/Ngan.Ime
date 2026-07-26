using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Avalime.Core.Voice;
using Avalime.Core.Infra.Log;

namespace Avalime.Backend.Voice.Volcengine;

public sealed partial class VolcengineVoiceRecognitionSession{
	/// 初始化有界结果通道和唯一接收循环；接收失败通过通道完成状态交给消费者。
	public partial VolcengineVoiceRecognitionSession(
		ClientWebSocket Socket
		, str TraceId
		, VolcengineVoiceRecognitionOptions Options
		, CT Ct
	){
		this.Socket = Socket ?? throw new ArgumentNullException(nameof(Socket));
		this.TraceId = TraceId;
		this.Options = Options ?? throw new ArgumentNullException(nameof(Options));
		SessionCts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		Results = Channel.CreateBounded<VoiceRecognitionResult>(new BoundedChannelOptions(32){
			SingleReader = true,
			SingleWriter = true,
			FullMode = BoundedChannelFullMode.Wait,
		});
		SendLock = new SemaphoreSlim(1, 1);
		FinalResultCompletion = new TaskCompletionSource<nil>(TaskCreationOptions.RunContinuationsAsynchronously);
		ReceiveTask = ReceiveLoop(SessionCts.Token);
	}

	/// 普通音频帧不能在尾包发送后继续写入。
	public async partial Task<nil> SendAudio(ReadOnlyMemory<byte> PcmChunk, CT Ct){
		if(IsDisposed || IsAudioCompleted){
			throw new InvalidOperationException("語音識別會話已停止接收音頻");
		}
		if(PcmChunk.IsEmpty){
			return NIL;
		}
		// 僅記錄分片尺寸，不記錄音頻內容，藉此區分“沒有採到音”與“服務端沒有回包”。
		AppLog.Debug($"[VoiceInput] Send PCM bytes={PcmChunk.Length}");
		await SendMessage(VolcengineProtocolCodec.EncodeAudio(PcmChunk.Span, false), Ct);
		return NIL;
	}

	/// 尾包通知服务端音频结束，再按配置等待最终响应或接收错误。
	public async partial Task<nil> CompleteAudio(CT Ct){
		if(IsDisposed){
			return NIL;
		}
		if(!IsAudioCompleted){
			IsAudioCompleted = true;
			AppLog.Info("[VoiceInput] Send final PCM packet");
			await SendMessage(VolcengineProtocolCodec.EncodeAudio(ReadOnlySpan<byte>.Empty, true), Ct);
		}

		using var TimeoutCts = new CancellationTokenSource(Options.FinalResultTimeout);
		using var LinkedCts = CancellationTokenSource.CreateLinkedTokenSource(Ct, TimeoutCts.Token);
		try{
			await FinalResultCompletion.Task.WaitAsync(LinkedCts.Token);
		}catch(OperationCanceledException Error) when(TimeoutCts.IsCancellationRequested){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Network, "等待火山語音識別最終結果超時", TraceId: TraceId, InnerException: Error);
		}
		return NIL;
	}

	/// 结果通道只允许 VM 的单条接收管线消费。
	public async partial IAsyncEnumerable<VoiceRecognitionResult> ReadResults(CT Ct){
		await foreach(var Result in Results.Reader.ReadAllAsync(Ct)){
			yield return Result;
		}
	}

	/// 取消接收、关闭套接字并结束结果通道；不等待自身接收任务以避免重入死锁。
	public async partial Task<nil> Stop(CT Ct){
		if(IsDisposed){
			return NIL;
		}
		IsDisposed = true;
		SessionCts.Cancel();
		FinalResultCompletion.TrySetCanceled();
		try{
			if(Socket.State is WebSocketState.Open or WebSocketState.CloseReceived){
				await Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "client stopped", Ct);
			}
		}catch(WebSocketException){
			Socket.Abort();
		}
		Results.Writer.TryComplete();
		return NIL;
	}

	/// Dispose 复用 Stop，再释放本地系统资源。
	public async partial ValueTask DisposeAsync(){
		await Stop(CancellationToken.None);
		Socket.Dispose();
		SessionCts.Dispose();
		SendLock.Dispose();
	}

	/// 唯一读取者持续解析服务端消息，并把稳定文本快照写入结果通道。
	private async partial Task<nil> ReceiveLoop(CT Ct){
		Exception? Error = null;
		try{
			while(!Ct.IsCancellationRequested && Socket.State is WebSocketState.Open or WebSocketState.CloseSent){
				var Raw = await ReceiveMessage(Ct);
				var Message = VolcengineProtocolCodec.Decode(Raw);
				// 記錄協議元資料與解壓後負載大小，避免把識別文本或任何敏感音頻寫入日誌。
				AppLog.Debug($"[VoiceInput] Receive frame type={Message.MessageType} flags={Message.Flags} payloadBytes={Message.Payload.Length}");
				if(Message.MessageType == EVolcengineMessageType.ServerError){
					throw MkServerException(Message);
				}
				var Result = ConvertResult(Message);
				if(Result is not null){
					await Results.Writer.WriteAsync(Result, Ct);
					if(Result.IsCompleted){
						FinalResultCompletion.TrySetResult(NIL);
					}
				}
			}
		}catch(OperationCanceledException) when(Ct.IsCancellationRequested){
			FinalResultCompletion.TrySetCanceled(Ct);
		}catch(Exception E){
			Error = E;
			FinalResultCompletion.TrySetException(E);
		}finally{
			Results.Writer.TryComplete(Error);
		}
		return NIL;
	}

	/// WebSocket 可将一条协议消息分成多帧，先完整重组并强制最大尺寸。
	private async partial Task<byte[]> ReceiveMessage(CT Ct){
		var Buffer = new ArrayBufferWriter<byte>();
		var Temp = new byte[8192];
		while(true){
			var Receive = await Socket.ReceiveAsync(Temp, Ct);
			if(Receive.MessageType == WebSocketMessageType.Close){
				throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Network, "火山語音識別連接已關閉", TraceId: TraceId);
			}
			if(Receive.MessageType != WebSocketMessageType.Binary || Buffer.WrittenCount + Receive.Count > Options.MaxServerMessageBytes){
				throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Protocol, "火山返回了無效或過大的識別消息", TraceId: TraceId);
			}
			Temp.AsSpan(0, Receive.Count).CopyTo(Buffer.GetSpan(Receive.Count));
			Buffer.Advance(Receive.Count);
			if(Receive.EndOfMessage){
				return Buffer.WrittenSpan.ToArray();
			}
		}
	}

	/// 把服务端 complete utterance 累入稳定段；最后一个未 definite 的句子保持临时段。
	private partial VoiceRecognitionResult? ConvertResult(VolcengineProtocolMessage Message){
		if(Message.MessageType != EVolcengineMessageType.FullServerResponse){
			return null;
		}
		if(Message.Serialization != EVolcengineSerialization.Json){
			throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Protocol, "火山识别响应不是 JSON", TraceId: TraceId);
		}
		var Response = JsonSerializer.Deserialize(Message.Payload.Span, VolcengineJsonContext.Default.VolcengineServerResponse);
		if(Response?.result is null){
			return null;
		}

		var Stable = new StringBuilder();
		var Interim = new StringBuilder();
		foreach(var Utterance in Response.result.utterances){
			if(Utterance.definite){
				Stable.Append(Utterance.text);
			}else{
				Interim.Append(Utterance.text);
			}
		}
		if(Response.result.utterances.Count == 0){
			Interim.Append(Response.result.text);
		}
		var IsCompleted = Message.Flags is EVolcengineMessageFlags.LastPackage or EVolcengineMessageFlags.HasNegativeSequence;
		if(IsCompleted && Interim.Length > 0){
			Stable.Append(Interim);
			Interim.Clear();
		}
		return new VoiceRecognitionResult{
			FinalizedText = Stable.ToString(),
			InterimText = Interim.ToString(),
			IsCompleted = IsCompleted,
			TraceId = TraceId,
		};
	}

	/// 服务端错误负载可能是 JSON 或纯文本，均保留原始码与链路标识。
	private partial Exception MkServerException(VolcengineProtocolMessage Message){
		var ProviderCode = Message.ErrorCode?.ToString() ?? "";
		var Detail = Message.Payload.IsEmpty ? "火山服務端返回錯誤" : Encoding.UTF8.GetString(Message.Payload.Span);
		var Kind = Message.ErrorCode switch{
			45000001 or 45000151 => EVoiceRecognitionErrorKind.Protocol,
			45000081 => EVoiceRecognitionErrorKind.Network,
			55000031 => EVoiceRecognitionErrorKind.Quota,
			_ => EVoiceRecognitionErrorKind.Network,
		};
		return new VoiceRecognitionException(Kind, Detail, ProviderCode, TraceId);
	}

	/// 串行发送防止 PCM 与尾包交错；WebSocket 完整消息必须一次发送完成。
	private async partial Task<nil> SendMessage(ReadOnlyMemory<byte> Message, CT Ct){
		await SendLock.WaitAsync(Ct);
		try{
			if(Socket.State != WebSocketState.Open){
				throw new VoiceRecognitionException(EVoiceRecognitionErrorKind.Network, "火山語音識別連接不可用", TraceId: TraceId);
			}
			await Socket.SendAsync(Message, WebSocketMessageType.Binary, true, Ct);
		}finally{
			SendLock.Release();
		}
		return NIL;
	}
}
