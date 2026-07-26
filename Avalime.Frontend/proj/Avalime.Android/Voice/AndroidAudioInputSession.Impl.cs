using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Android.Media;
using Avalime.Core.Voice;

namespace Avalime.Android.Voice;

public sealed partial class AndroidAudioInputSession{
	/// AudioRecord 緩衝至少取系統下限與兩個應用分片中的較大值，降低調度抖動造成的欠載風險。
	public partial AndroidAudioInputSession(AudioInputOptions Options, CT Ct){
		ArgumentNullException.ThrowIfNull(Options);
		ChunkByteLength = checked(Options.SampleRate * Options.Channels * (Options.BitsPerSample / 8) * Options.ChunkDurationMs / 1000);
		var MinBufferSize = AudioRecord.GetMinBufferSize(Options.SampleRate, ChannelIn.Mono, Encoding.Pcm16bit);
		if(MinBufferSize <= 0){
			throw MkAudioError("Android 無法取得麥克風最小緩衝大小", MinBufferSize);
		}

		var RecorderBufferSize = Math.Max(MinBufferSize, checked(ChunkByteLength * 2));
		Recorder = new AudioRecord(AudioSource.VoiceRecognition, Options.SampleRate, ChannelIn.Mono, Encoding.Pcm16bit, RecorderBufferSize);
		if(Recorder.State != State.Initialized){
			Recorder.Release();
			Recorder.Dispose();
			throw MkAudioError("Android 麥克風初始化失敗");
		}

		Chunks = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(Options.MaxBufferedChunks){
			SingleReader = true,
			SingleWriter = true,
			FullMode = BoundedChannelFullMode.Wait,
		});
		SessionCts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		try{
			Recorder.StartRecording();
			if(Recorder.RecordingState != RecordState.Recording){
				throw MkAudioError("Android 麥克風未進入錄音狀態");
			}
			CaptureTask = Task.Run(() => Capture(SessionCts.Token), CancellationToken.None);
		}catch{
			SessionCts.Dispose();
			Recorder.Release();
			Recorder.Dispose();
			throw;
		}
	}

	/// ChannelReader 保持懶加載與單次消費，取消只影響本次枚舉等待。
	public async partial IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunks([EnumeratorCancellation] CT Ct){
		await foreach(var Chunk in Chunks.Reader.ReadAllAsync(Ct)){
			yield return Chunk;
		}
	}

	/// 先取消並停止 AudioRecord 以解除阻塞 Read，再等待採集循環完成清理。
	public async partial Task<nil> Stop(CT Ct){
		if(Interlocked.Exchange(ref IsStopped, 1) != 0){
			return NIL;
		}

		SessionCts.Cancel();
		try{
			if(Recorder.RecordingState == RecordState.Recording){
				Recorder.Stop();
			}
		}catch(Java.Lang.IllegalStateException){
			// 設備已被系統停止時仍繼續走統一釋放路徑。
		}

		try{
			await CaptureTask.WaitAsync(Ct);
		}catch(OperationCanceledException) when(SessionCts.IsCancellationRequested){
			// 主動停止造成的採集取消不是語音失敗。
		}finally{
			Chunks.Writer.TryComplete();
			Recorder.Release();
			Recorder.Dispose();
			SessionCts.Dispose();
		}
		return NIL;
	}

	/// Dispose 不受外部取消影響，確保底層麥克風句柄一定被釋放。
	public async partial ValueTask DisposeAsync(){
		await Stop(CancellationToken.None);
	}

	/// 每個 byte[] 僅交給下游一次；下游進入下一次迭代前完成發送後即可由 GC 回收。
	private async partial Task Capture(CT Ct){
		Exception? CompletionError = null;
		try{
			while(!Ct.IsCancellationRequested){
				var Buffer = new byte[ChunkByteLength];
				var Offset = 0;
				while(Offset < Buffer.Length && !Ct.IsCancellationRequested){
					var Read = Recorder.Read(Buffer, Offset, Buffer.Length - Offset);
					if(Read < 0){
						if(Ct.IsCancellationRequested){
							break;
						}
						throw MkAudioError("Android 麥克風讀取失敗", Read);
					}
					if(Read == 0){
						continue;
					}
					Offset += Read;
				}
				if(Offset == Buffer.Length && !Chunks.Writer.TryWrite(Buffer)){
					throw MkAudioError("語音發送速度跟不上 Android 麥克風，音頻緩衝已滿");
				}
			}
		}catch(OperationCanceledException) when(Ct.IsCancellationRequested){
			// Stop 會取消阻塞後的 Channel 寫入；這是正常收尾。
		}catch(Exception Error){
			CompletionError = Error is VoiceRecognitionException
				? Error
				: MkAudioError("Android 麥克風採集意外終止", Error: Error);
		}finally{
			Chunks.Writer.TryComplete(CompletionError);
		}
	}

	/// 保留原生錯誤碼供診斷，同時讓 UI 只依賴 AudioDevice 分類。
	private static partial VoiceRecognitionException MkAudioError(str Message, i32 ErrorCode, Exception? Error){
		return new VoiceRecognitionException(
			EVoiceRecognitionErrorKind.AudioDevice,
			Message,
			ProviderCode: ErrorCode == 0 ? "" : ErrorCode.ToString(),
			InnerException: Error
		);
	}
}
