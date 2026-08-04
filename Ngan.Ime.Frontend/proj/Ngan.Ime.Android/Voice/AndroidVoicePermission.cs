using Android.App;
using Android.Content;
using Android.Content.PM;
using Ngan.Ime.Core.Infra.Log;

namespace Ngan.Ime.Android.Voice;

/// Android 麥克風運行時權限入口。
/// InputMethodService 不能直接顯示權限對話框，因此缺權限時啓動主 Activity 代爲申請。
public static class AndroidVoicePermission{
	/// 專用 Intent action，讓主 Activity 區分普通啓動與語音權限請求。
	public const str RequestAction = "Tsinswreng.Ngan.Ime.action.REQUEST_RECORD_AUDIO";

	/// 主 Activity 使用的錄音權限請求碼。
	public const i32 RequestCode = 12001;

	/// 檢查當前進程是否已獲得麥克風權限。
	public static bool IsGranted(Context Context){
		return Context.CheckSelfPermission(global::Android.Manifest.Permission.RecordAudio) == Permission.Granted;
	}

	/// 從 Activity 或 IME Service 打開主 Activity，交由 Activity 顯示系統權限對話框。
	public static void Request(Context Context){
		var Intent = new Intent(Context, typeof(MainActivity));
		Intent.SetAction(RequestAction);
		Intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
		Context.StartActivity(Intent);
		AppLog.Info("[VoiceInput][Android] Opened microphone permission activity");
	}
}
