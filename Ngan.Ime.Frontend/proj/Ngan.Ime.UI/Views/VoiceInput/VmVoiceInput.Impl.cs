//Impl: 聲明見 VmVoiceInput.cs。
namespace Ngan.Ime.UI.Views.VoiceInput;

using Avalonia.Threading;
using Ngan.Ime.Core.Language;
using Ngan.Ime.Core.Voice;

public partial class VmVoiceInput{

	[Doc("忙碌時忽略延遲的語言回調，保證一輪會話的語言參數不變。")]
	public partial void ApplyLang(INormLang Lang){
		if(!IsBusy && Lang is not null){
			CurrentLang = Lang;
		}
	}

	[Doc("錄音中點擊＝收尾；等待最終結果時點擊＝取消；其餘階段＝開始。")]
	public async partial Task<nil> ToggleRecording(CT Ct){
		switch(Phase){
			case EVoiceInputPhase.Recording:
				return await StopRecognition(Ct);
			case EVoiceInputPhase.Finalizing:
				return await CancelRecognition(Ct);
			default:
				return await StartRecognition(Ct);
		}
	}

	[Doc("先建遠端會話、成功後才開麥克風，避免未鑒權時無意義採集音頻。")]
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
			StatusText = Todo.I18n("正在連接語音識別服務");
			Phase = EVoiceInputPhase.Starting;

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

			// 兩條管線各自只允許消費一次；看守任務負責觀察它們的例外。
			var SendTask = PumpAudio(AudioSession, RecognitionSession, SessionCt);
			var ReadTask = PumpResults(RecognitionSession, SessionCt);
			Fire(WatchPipelinesAsy(SendTask, ReadTask));

			StatusText = Todo.I18n("正在錄音");
			Phase = EVoiceInputPhase.Recording;
		}catch(Exception Error){
			await FailRecognition(Error, Ct);
		}finally{
			SessionGate.Release();
		}
		return NIL;
	}

	[Doc("停止麥克風、發尾包、等待最終結果；取消令牌觸發時視為「取消」而非「失敗」。")]
	public async partial Task<nil> StopRecognition(CT Ct){
		await SessionGate.WaitAsync(Ct);
		try{
			if(Phase != EVoiceInputPhase.Recording || AudioSession is null || RecognitionSession is null){
				return NIL;
			}
			Phase = EVoiceInputPhase.Finalizing;
			StatusText = Todo.I18n("正在等待最終識別結果");
			// 用本輪會話令牌，讓「取消」能中斷正在等待的尾包。
			var SessionCt = SessionCts?.Token ?? Ct;
			var Audio = AudioSession;
			var Recognition = RecognitionSession;
			await Audio.Stop(SessionCt);
			await Recognition.CompleteAudio(SessionCt);
			StatusText = Todo.I18n("識別完成，可修改後上屏");
			Phase = EVoiceInputPhase.Completed;
			await ReleaseSessionResources(false, Ct);
		}catch(OperationCanceledException) when(SessionCts?.IsCancellationRequested == true){
			// 使用者主動取消：不顯示錯誤，保留已取得的文本。
			Phase = EVoiceInputPhase.Idle;
			StatusText = Todo.I18n("語音輸入已取消");
			await ReleaseSessionResources(true, CancellationToken.None);
		}catch(Exception Error){
			await FailRecognition(Error, Ct);
		}finally{
			SessionGate.Release();
		}
		return NIL;
	}

	[Doc("在 UI 線程以快照重建草稿，避免背景 WebSocket 線程直接改綁定對象。")]
	public partial void ApplyRecognitionText(VoiceRecognitionResult Update){
		if(!Dispatcher.UIThread.CheckAccess()){
			Dispatcher.UIThread.Post(() => ApplyRecognitionText(Update));
			return;
		}
		FinalizedText = Update.FinalizedText;
		InterimText = Update.InterimText;
		DraftText = FinalizedText + InterimText;
		if(Update.IsCompleted && Phase == EVoiceInputPhase.Finalizing){
			StatusText = Todo.I18n("正在整理最終識別結果");
		}
	}

	[Doc("單次消費麥克風序列並依序交給遠端會話。")]
	private partial async Task<nil> PumpAudio(IAudioInputSession Audio, IVoiceRecognitionSession Recognition, CT Ct){
		await foreach(var Chunk in Audio.ReadChunks(Ct)){
			await Recognition.SendAudio(Chunk, Ct);
		}
		return NIL;
	}

	[Doc("單次消費遠端結果序列。")]
	private partial async Task<nil> PumpResults(IVoiceRecognitionSession Recognition, CT Ct){
		await foreach(var Update in Recognition.ReadResults(Ct)){
			ApplyRecognitionText(Update);
		}
		return NIL;
	}

	[Doc("看守兩條管線：正常結束什麼都不做，例外則在閘門內收束成 Failed。")]
	private partial async Task<nil> WatchPipelinesAsy(Task<nil> SendTask, Task<nil> ReadTask){
		try{
			await Task.WhenAll(SendTask, ReadTask);
		}catch(OperationCanceledException){
			// 取消是正常路徑，由 StopRecognition／CancelRecognition 收尾。
		}catch(Exception Error){
			await SessionGate.WaitAsync();
			try{
				if(IsBusy){
					await FailRecognition(Error, CancellationToken.None);
				}
			}finally{
				SessionGate.Release();
			}
		}
		return NIL;
	}

	[Doc("取消或正常完成都走這裡：先清引用，再逐項釋放；取消路徑向遠端送終止訊號。")]
	private partial async Task<nil> ReleaseSessionResources(bool CancelRemote, CT Ct){
		var Audio = AudioSession;
		var Recognition = RecognitionSession;
		var Cancellation = SessionCts;
		AudioSession = null;
		RecognitionSession = null;
		SessionCts = null;
		Cancellation?.Cancel();
		if(Audio is not null){
			await Audio.DisposeAsync();
		}
		if(Recognition is not null){
			if(CancelRemote){
				// 清理不能依賴已經被頁面生命週期取消的令牌。
				await Recognition.Stop(CancellationToken.None);
			}
			await Recognition.DisposeAsync();
		}
		Cancellation?.Dispose();
		return NIL;
	}

	[Doc("所有啟動、錄音、網絡與協議異常的唯一收束點；保留原始堆棧供排查。")]
	private partial async Task<nil> FailRecognition(Exception Error, CT Ct){
		AppLog.Error(Error, "[VoiceInput] 語音識別失敗");
		ErrorText = MkErrText(Error);
		StatusText = Todo.I18n("語音識別已停止");
		Phase = EVoiceInputPhase.Failed;
		await ReleaseSessionResources(true, Ct);
		return NIL;
	}

	[Doc("依錯誤分類給使用者可讀文案；庫層訊息只進日誌。")]
	private static partial str MkErrText(Exception Error){
		if(Error is not VoiceRecognitionException Voice){
			return Todo.I18n("語音識別失敗，請檢查麥克風、網絡和配置");
		}
		return Voice.Kind switch {
			EVoiceRecognitionErrorKind.Configuration => Todo.I18n("語音識別配置不完整，請先在配置檔填入金鑰"),
			EVoiceRecognitionErrorKind.MicrophonePermission => Todo.I18n("沒有麥克風權限，請在系統設定中授予"),
			EVoiceRecognitionErrorKind.AudioDevice => Todo.I18n("麥克風不可用或被其他程式佔用"),
			EVoiceRecognitionErrorKind.Network => Todo.I18n("網絡不通，請稍後重試"),
			EVoiceRecognitionErrorKind.Authentication => Todo.I18n("語音服務鑒權失敗，請檢查金鑰"),
			EVoiceRecognitionErrorKind.Quota => Todo.I18n("語音服務額度不足"),
			EVoiceRecognitionErrorKind.UnsupportedLanguage => Todo.I18n("該語言不受支援"),
			EVoiceRecognitionErrorKind.Protocol => Todo.I18n("語音服務回應異常，請稍後重試"),
			EVoiceRecognitionErrorKind.Cancelled => Todo.I18n("操作已取消"),
			_ => Todo.I18n("語音識別失敗，請檢查麥克風、網絡和配置"),
		};
	}

	[Doc("非忙碌時重置文字與階段。")]
	public partial void ClearText(){
		if(IsBusy){
			return;
		}
		FinalizedText = "";
		InterimText = "";
		DraftText = "";
		ErrorText = "";
		StatusText = Todo.I18n("點擊開始語音輸入");
		Phase = EVoiceInputPhase.Idle;
	}

	[Doc("上屏前再檢查能力與文本，避免繞過按鈕狀態。")]
	public partial void CommitText(){
		if(CanCommitText){
			KeyboardHost.CommitText(DraftText);
		}
	}

	[Doc("取消保留現有草稿，只停止音頻與網絡資源。")]
	public async partial Task<nil> CancelRecognition(CT Ct){
		// 先發取消訊號再等閘門：否則 StopRecognition 持鎖等尾包時，取消按鈕按了沒反應。
		SessionCts?.Cancel();
		await SessionGate.WaitAsync(Ct);
		try{
			if(IsBusy){
				await ReleaseSessionResources(true, Ct);
				StatusText = Todo.I18n("語音輸入已取消");
				Phase = EVoiceInputPhase.Idle;
			}
		}finally{
			SessionGate.Release();
		}
		return NIL;
	}

	[Doc("釋放：取消在途會話並釋放閘門；之後不得再使用本實例。")]
	public partial void Dispose(){
		try{
			SessionCts?.Cancel();
		}catch(ObjectDisposedException){
			// 已釋放，忽略。
		}
		GC.SuppressFinalize(this);
	}
}
