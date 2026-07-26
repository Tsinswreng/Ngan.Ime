using Avalime.Core.Voice;

namespace Avalime.Windows.Voice;

/// Windows 麥克風 PCM 採集器的聲明。
/// 實現階段使用 Windows 可用的音頻 API 將默認設備轉換為 AudioInputOptions 指定格式，
/// 並以 IAsyncEnumerable 分片輸出，避免一次性把整段錄音載入內存。
public sealed partial class WindowsAudioInputSource : IAudioInputSource{
	/// 建立 Windows 麥克風採集器；設備選擇和權限錯誤在 Start 時報告。
	public partial WindowsAudioInputSource();

	/// 啟動默認輸入設備的 PCM 採集會話。
	public partial Task<IAudioInputSession> Start(AudioInputOptions Options, CT Ct);
}
