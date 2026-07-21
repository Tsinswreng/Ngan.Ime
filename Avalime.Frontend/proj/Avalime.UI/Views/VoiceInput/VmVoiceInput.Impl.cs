using Avalime.Core.Language;

namespace Avalime.UI.Views.VoiceInput;

/// 語音輸入頁 ViewModel 的前端佔位實現。
/// 錄音與語言狀態流程尚未接入，本文件只令現階段聲明可被視圖安全建立。
public partial class VmVoiceInput{
	/// 建立不依賴語音服務的前端預覽資料。
	public static partial VmVoiceInput Mk(){
		return new VmVoiceInput();
	}

	/// 語言切換留待語音服務設計完成後實現。
	public partial void ApplyLang(NormLangDetail Lang){
		_ = Lang;
	}

	/// 錄音切換留待平台錄音服務接入後實現。
	public partial void ToggleRecording(){
	}

	/// 停止錄音留待平台錄音服務接入後實現。
	public partial void StopRecording(){
	}
}
