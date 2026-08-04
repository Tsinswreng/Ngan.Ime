using Android.Content;
using Ngan.Ime.Core.Voice;

namespace Ngan.Ime.Android.Voice;

/// Android 原生麥克風 PCM 採集入口。
/// 使用 AudioRecord，避免引入依賴反射或運行時代碼生成的第三方音頻庫，保持 NativeAOT 可達。
public sealed partial class AndroidAudioInputSource : IAudioInputSource{
	/// 用於權限檢查和啓動權限 Activity 的應用上下文。
	readonly Context Context;

	/// 建立 Android 麥克風來源；只保存 Application Context，不持有 Activity 或 IME Service。
	public partial AndroidAudioInputSource(Context Context);

	/// 校驗 PCM 契約、檢查權限並啓動一次 AudioRecord 會話。
	public partial Task<IAudioInputSession> Start(AudioInputOptions Options, CT Ct);

	/// Android 首版只接受火山已驗證的 16 kHz、16-bit、單聲道 PCM，避免隱式重採樣。
	private static partial void ValidateOptions(AudioInputOptions Options);
}
