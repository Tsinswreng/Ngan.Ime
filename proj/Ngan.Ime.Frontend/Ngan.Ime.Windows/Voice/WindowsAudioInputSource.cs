using Tsinswreng.CsCore;
using Ngan.Ime.Core.Voice;

namespace Ngan.Ime.Windows.Voice;

[Doc($$"""
Windows 麥克風 PCM 採集器的聲明。
第一版使用 NAudio 封裝的 WASAPI 將默認設備轉換為 AudioInputOptions 指定格式，
並以 IAsyncEnumerable 分片輸出，避免一次性把整段錄音載入內存。
NAudio 的 NativeAOT 相容性需要在 Windows 原型驗收後單獨驗證；若不滿足發布要求，
後續只替換此平台實現爲原生 WASAPI，不改動 Core 音頻契約或火山後端。
""")]
public sealed partial class WindowsAudioInputSource : IAudioInputSource{
	[Doc($$"""
	枚舉和打開 Windows 音頻端點的 NAudio 管理器。
	*延後建立*：NAudio 的 WASAPI 走 `[ComImport]` 類介面，NativeAOT 不支援這條路徑；
	若在建構子就 `new`，整個界面在啟動時就會因 `InvalidProgramException` 崩掉
	（桌面模式建 MainView 時就會解析到本服務）。故改成第一次錄音時才建。
	""")]
	NAudio.CoreAudioApi.MMDeviceEnumerator? DeviceEnumerator;

	[Doc($$"""
	建立 Windows 麥克風採集器；不碰 COM，設備選擇和權限錯誤在 Start 時報告。
	""")]
	public partial WindowsAudioInputSource();

	[Doc($$"""
	啟動默認輸入設備的 PCM 採集會話。
	""")]
	public partial Task<IAudioInputSession> Start(AudioInputOptions Options, CT Ct);

	[Doc($$"""
	校驗目標格式及分片時長，拒絕無法安全轉換或會產生零長度分片的參數。
	""")]
	private partial void ValidateOptions(AudioInputOptions Options);
}
