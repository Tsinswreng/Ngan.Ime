using Ngan.Ime.Core.Infra;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.Core.Language;
using Ngan.Ime.Core.Voice;
using Avalonia.Threading;

namespace Ngan.Ime.UI.Views.VoiceInput;

/// 語音輸入頁的識別會話編排實現。
/// VM 只協調跨平臺抽象，不解析供應商協議，也不直接調用 Windows 麥克風 API。
public partial class VmVoiceInput{
	/// 保存所有注入依賴，並讀取宿主是否支持上屏。
	public partial VmVoiceInput(
		IKeyboardHost KeyboardHost
		, IVoiceRecognitionProvider RecognitionProvider
		, IAudioInputSource AudioInputSource
		, AudioInputOptions AudioOptions
	){
		this.KeyboardHost = KeyboardHost;
		this.RecognitionProvider = RecognitionProvider;
		this.AudioInputSource = AudioInputSource;
		this.AudioOptions = AudioOptions;
		IsCommitSupported = KeyboardHost.CanCommitText;
	}

	/// 兜底創建仍走 DI，使 View 在未直接註冊 VM 時取得相同依賴組合。
	public static partial VmVoiceInput Mk(){
		return new VmVoiceInput(
			Di.GetRSvc<IKeyboardHost>(),
			Di.GetRSvc<IVoiceRecognitionProvider>(),
			Di.GetRSvc<IAudioInputSource>(),
			Di.GetRSvc<AudioInputOptions>()
		);
	}

	/// 識別期間忽略延遲語言回調，確保一輪會話參數不可變。
	public partial void ApplyLang(INormLang Lang){
		if(!IsBusy && Lang is not null){
			CurrentLang = Lang;
		}
	}

	/// 錄音中點擊表示正常收尾；只有已進入等待最終結果時，第二次點擊才表示放棄等待。
	public async partial Task<nil> ToggleRecording(CT Ct){
		if(Phase == EVoiceInputPhase.Recording){
			return await StopRecognition(Ct);
		}
		if(Phase == EVoiceInputPhase.Finalizing){
			return await CancelRecognition(Ct);
		}
		if(Phase is EVoiceInputPhase.Idle or EVoiceInputPhase.Completed or EVoiceInputPhase.Failed){
			return await StartRecognition(Ct);
		}
		return NIL;
	}

	/// 先建立遠端會話，成功後才開啓麥克風，避免未鑒權時無意義採集音頻。
	public async partial Task<nil> StartRecognition(CT Ct){
		await SessionGate.WaitAsync(Ct);
		try{
			if(IsBusy){
				return NIL;
			}
			ErrorText = "";
			InterimText = "";
			FinalizedText = "";
			DraftText = "";
			StatusText = "正在連接語音識別服務";
			Phase = EVoiceInputPhase.Starting;
			AppLog.Info($"[VoiceInput] Start requested language={CurrentLang.Code} sampleRate={AudioOptions.SampleRate} channels={AudioOptions.Channels}");
			SessionCts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
			var SessionCt = SessionCts.Token;
			var Request = new VoiceRecognitionRequest{
				Language = CurrentLang,
				Audio = new AudioInputOptions{
					SampleRate = AudioOptions.SampleRate,
					Channels = AudioOptions.Channels,
					BitsPerSample = AudioOptions.BitsPerSample,
					ChunkDurationMs = AudioOptions.ChunkDurationMs,
					MaxBufferedChunks = AudioOptions.MaxBufferedChunks,
				},
			};
			RecognitionSession = await RecognitionProvider.StartSession(Request, SessionCt);
			AudioSession = await AudioInputSource.Start(Request.Audio, SessionCt);
			ReadResultsTask = PumpResults(RecognitionSession, SessionCt);
			SendAudioTask = PumpAudio(AudioSession, RecognitionSession, SessionCt);
			// 任一后台管线意外失败时，切回 UI 线程串行结束本轮会话。
			_ = SendAudioTask.ContinueWith(async Task=>{
				if(Task.IsFaulted && Task.Exception is not null){
					await SessionGate.WaitAsync();
					try{
						if(IsBusy){
							await FailRecognition(Task.Exception.GetBaseException(), CancellationToken.None);
						}
					}finally{
						SessionGate.Release();
					}
				}
			}, TaskScheduler.Default);
			_ = ReadResultsTask.ContinueWith(async Task=>{
				if(Task.IsFaulted && Task.Exception is not null){
					await SessionGate.WaitAsync();
					try{
						if(IsBusy){
							await FailRecognition(Task.Exception.GetBaseException(), CancellationToken.None);
						}
					}finally{
						SessionGate.Release();
					}
				}
			}, TaskScheduler.Default);
			StatusText = "正在錄音";
			Phase = EVoiceInputPhase.Recording;
		}catch(Exception Error){
			await FailRecognition(Error, Ct);
		}
		finally{
			SessionGate.Release();
		}
		return NIL;
	}

	/// 停止麥克風後發送尾包，等待服務端完成並保留最後草稿。
	public async partial Task<nil> StopRecognition(CT Ct){
		await SessionGate.WaitAsync(Ct);
		try{
			if(Phase != EVoiceInputPhase.Recording || AudioSession is null || RecognitionSession is null){
				return NIL;
			}
			Phase = EVoiceInputPhase.Finalizing;
			StatusText = "正在等待最終識別結果";
			// 使用本輪會話令牌，使錄音按鈕的「取消」能中斷正在等待的尾包。
			var SessionCt = SessionCts?.Token ?? Ct;
			await AudioSession.Stop(SessionCt);
			await SendAudioTask!;
			await RecognitionSession.CompleteAudio(SessionCt);
			await ReadResultsTask!;
			StatusText = "識別完成，可修改後上屏";
			Phase = EVoiceInputPhase.Completed;
			await ReleaseSessionResources(false, Ct);
		}catch(Exception Error){
			await FailRecognition(Error, Ct);
		}finally{
			SessionGate.Release();
		}
		return NIL;
	}

	/// 在 UI 線程以快照重建草稿，避免后台 WebSocket 线程直接触碰绑定对象。
	public partial void ApplyRecognitionText(VoiceRecognitionResult Update){
		if(!Dispatcher.UIThread.CheckAccess()){
			Dispatcher.UIThread.Post(()=>ApplyRecognitionText(Update));
			return;
		}
		FinalizedText = Update.FinalizedText;
		InterimText = Update.InterimText;
		DraftText = FinalizedText + InterimText;
		if(Update.IsCompleted && Phase == EVoiceInputPhase.Finalizing){
			StatusText = "正在整理最終識別結果";
		}
	}

	/// 单次消费麦克风序列并按顺序交给远端会话。
	private async partial Task<nil> PumpAudio(IAudioInputSession AudioSession, IVoiceRecognitionSession RecognitionSession, CT Ct){
		await foreach(var Chunk in AudioSession.ReadChunks(Ct)){
			await RecognitionSession.SendAudio(Chunk, Ct);
		}
		return NIL;
	}

	/// 单次消费远端结果序列；任何管线异常都由 Start/Stop 的 await 统一处理。
	private async partial Task<nil> PumpResults(IVoiceRecognitionSession RecognitionSession, CT Ct){
		await foreach(var Update in RecognitionSession.ReadResults(Ct)){
			ApplyRecognitionText(Update);
		}
		return NIL;
	}

	/// 取消或正常完成均幂等释放会话；取消路径向远端发送终止信号。
	private async partial Task<nil> ReleaseSessionResources(bool CancelRemote, CT Ct){
		var Audio = AudioSession;
		var Recognition = RecognitionSession;
		var Cancellation = SessionCts;
		AudioSession = null;
		RecognitionSession = null;
		SessionCts = null;
		SendAudioTask = null;
		ReadResultsTask = null;
		Cancellation?.Cancel();
		if(Audio is not null){
			await Audio.DisposeAsync();
		}
		if(Recognition is not null){
			if(CancelRemote){
				// 清理不能依赖已经被页面生命周期取消的调用令牌。
				await Recognition.Stop(CancellationToken.None);
			}
			await Recognition.DisposeAsync();
		}
		Cancellation?.Dispose();
		return NIL;
	}

	/// 映射异常并保留已识别文本，用户可复制、修正或立即开始下一轮。
	private async partial Task<nil> FailRecognition(Exception Error, CT Ct){
		// 此處是所有啟動、錄音、網絡與協議異常的唯一收束點，必須保留原始堆棧供本機日誌排查。
		AppLog.Error(Error, "[VoiceInput] Recognition session failed");
		ErrorText = Error is VoiceRecognitionException VoiceError
			? VoiceError.Message
			: "語音識別失敗，請檢查麥克風、網絡和配置";
		StatusText = "語音識別已停止";
		Phase = EVoiceInputPhase.Failed;
		await ReleaseSessionResources(true, Ct);
		return NIL;
	}

	/// 非忙碌时重置文字与阶段，避免与后台快照竞争。
	public partial void ClearText(){
		if(IsBusy){
			return;
		}
		FinalizedText = "";
		InterimText = "";
		DraftText = "";
		ErrorText = "";
		StatusText = "點擊開始語音輸入";
		Phase = EVoiceInputPhase.Idle;
	}

	/// 上屏前再检查能力和文本，避免绕过按钮状态调用宿主。
	public partial void CommitText(){
		if(CanCommitText){
			KeyboardHost.CommitText(DraftText);
		}
	}

	/// 取消保留现有草稿，只停止音频和网络资源。
	public async partial Task<nil> CancelRecognition(CT Ct){
		// 先發出取消訊號，再等待 SessionGate；否則 StopRecognition 持鎖等待尾包時，取消按鈕看似點了卻無法生效。
		SessionCts?.Cancel();
		await SessionGate.WaitAsync(Ct);
		try{
			if(IsBusy){
				await ReleaseSessionResources(true, Ct);
				StatusText = "語音輸入已取消";
				Phase = EVoiceInputPhase.Idle;
			}
		}finally{
			SessionGate.Release();
		}
		return NIL;
	}
}
