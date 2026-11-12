using Ngan.Ime.Core.Voice;

namespace Ngan.Ime.Windows.Voice;

/// Windows 麥克風 PCM 採集器的聲明。
/// 第一版使用 NAudio 封裝的 WASAPI 將默認設備轉換為 AudioInputOptions 指定格式，
/// 並以 IAsyncEnumerable 分片輸出，避免一次性把整段錄音載入內存。
/// NAudio 的 NativeAOT 相容性需要在 Windows 原型驗收後單獨驗證；若不滿足發布要求，
/// 後續只替換此平台實現爲原生 WASAPI，不改動 Core 音頻契約或火山後端。
public sealed partial class WindowsAudioInputSource : IAudioInputSource{
	/// 枚舉和打開 Windows 音頻端點的 NAudio 管理器。
	readonly NAudio.CoreAudioApi.MMDeviceEnumerator DeviceEnumerator;

	/// 建立 Windows 麥克風採集器；設備選擇和權限錯誤在 Start 時報告。
	public partial WindowsAudioInputSource();

	/// 啟動默認輸入設備的 PCM 採集會話。
	public partial Task<IAudioInputSession> Start(AudioInputOptions Options, CT Ct);

	/// 校驗目標格式及分片時長，拒絕無法安全轉換或會產生零長度分片的參數。
	private partial void ValidateOptions(AudioInputOptions Options);
}
