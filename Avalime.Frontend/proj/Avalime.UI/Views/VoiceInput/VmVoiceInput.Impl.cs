using Avalime.Core.Infra;
using Avalime.Core.Language;
using Avalonia.Threading;

namespace Avalime.UI.Views.VoiceInput;

/// 語音輸入頁 ViewModel 的前端狀態實現。
/// 當前尚未注入真正的識別 Provider，因此錄音入口會以明確錯誤結束，其他前端能力可獨立驗證。
public partial class VmVoiceInput{
	/// 建立 VM 並從宿主读取是否支持上屏。
	public partial VmVoiceInput(IKeyboardHost KeyboardHost){
		this.KeyboardHost = KeyboardHost;
		IsCommitSupported = KeyboardHost.CanCommitText;
	}

	/// 使用全局 DI 提供兜底构造；正式 View 仍通过 `DiOrMk` 优先解析已注册依赖。
	public static partial VmVoiceInput Mk(){
		return new VmVoiceInput(Di.GetRSvc<IKeyboardHost>());
	}

	/// 在非忙碌状态应用新语言，并统一生成按钮显示文本。
	public partial void ApplyLang(NormLangDetail Lang){
		if(IsBusy){
			return;
		}
		CurrentLang = Lang;
		CurrentLangDisplay = str.IsNullOrWhiteSpace(Lang.NativeName)
			? Lang.Code
			: $"{Lang.Code}  {Lang.NativeName}";
	}

	/// 根据当前阶段决定开始或停止；过渡阶段忽略重复点击。
	public async partial Task<nil> ToggleRecording(CT Ct){
		if(Phase == EVoiceInputPhase.Recording){
			return await StopRecognition(Ct);
		}
		if(Phase is EVoiceInputPhase.Idle or EVoiceInputPhase.Completed or EVoiceInputPhase.Failed){
			return await StartRecognition(Ct);
		}
		return NIL;
	}

	/// 当前没有 Provider，使用失败状态明确暴露尚未接入，而不是伪造录音成功。
	public async partial Task<nil> StartRecognition(CT Ct){
		Ct.ThrowIfCancellationRequested();
		ErrorText = "語音識別 Provider 尚未接入";
		StatusText = "暫時無法開始語音識別";
		Phase = EVoiceInputPhase.Failed;
		await Task.CompletedTask;
		return NIL;
	}

	/// Provider 接入前没有可停止的录音会话；保持已有文本并回到完成状态。
	public async partial Task<nil> StopRecognition(CT Ct){
		Ct.ThrowIfCancellationRequested();
		StatusText = "語音輸入已停止";
		Phase = EVoiceInputPhase.Completed;
		await Task.CompletedTask;
		return NIL;
	}

	/// 在 UI 线程用稳定段与临时段快照重建草稿，避免重复追加。
	public partial void ApplyRecognitionText(VoiceRecognitionTextUpdate Update){
		if(!Dispatcher.UIThread.CheckAccess()){
			var Snapshot = new VoiceRecognitionTextUpdate{
				FinalizedText = Update.FinalizedText,
				InterimText = Update.InterimText,
				IsCompleted = Update.IsCompleted,
			};
			Dispatcher.UIThread.Post(()=>ApplyRecognitionText(Snapshot));
			return;
		}

		FinalizedText = Update.FinalizedText;
		InterimText = Update.InterimText;
		DraftText = FinalizedText + InterimText;
		if(Update.IsCompleted){
			StatusText = "識別完成，可修改後上屏";
			Phase = EVoiceInputPhase.Completed;
		}
	}

	/// 非忙碌时清空所有前端文本状态。
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

	/// 二次检查能力与文本后，把用户确认的草稿原样交给宿主。
	public partial void CommitText(){
		if(!CanCommitText){
			return;
		}
		KeyboardHost.CommitText(DraftText);
	}

	/// 取消会话时保留草稿；当前没有 Provider 资源需要额外释放。
	public async partial Task<nil> CancelRecognition(CT Ct){
		Ct.ThrowIfCancellationRequested();
		if(IsBusy){
			StatusText = "語音輸入已取消";
			Phase = EVoiceInputPhase.Idle;
		}
		await Task.CompletedTask;
		return NIL;
	}
}
