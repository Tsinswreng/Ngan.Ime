using Avalime.UI.Infra;

namespace Avalime.UI.Views.VoiceInput;
using Ctx = VmVoiceInput;

/// 語音輸入頁的 View 聲明。
/// 目標佈局是中間一個圓形錄音按鈕，下方一行狀態文案。
public partial class ViewVoiceInput : AppViewBase<Ctx>
	, IDisposable
{
	public ViewVoiceInput();

	/// 組裝語音輸入頁視圖樹。
	/// 包含中央錄音按鈕與狀態文案。
	partial void Render();

	/// 同步錄音按鈕的視覺狀態。
	/// 非錄音態為白色，錄音態為 `UiCfg.MainColor`。
	partial void SyncRecordingVisual();

	public partial void Dispose();
}
