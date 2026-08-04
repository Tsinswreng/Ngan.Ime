using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;

namespace Ngan.Ime.Android;

/// <summary>
/// 分體懸浮鍵盤依賴 overlay 權限。
/// Android 這個權限不是普通 runtime permission，需要跳系統設置頁授權。
/// </summary>
public static class AvalimeOverlayPermission
{
	public static bool CanDraw(Context Context)
	{
		if(Build.VERSION.SdkInt < BuildVersionCodes.M){
			return true;
		}
		return Settings.CanDrawOverlays(Context);
	}

	/// <summary>
	/// 缺權限時直接打開系統 overlay 設置頁。
	/// 由於這個流程無法像普通權限那樣同步回調，調用方需自行決定是否暫時回退到普通鍵盤。
	/// </summary>
	public static bool Ensure(Context Context)
	{
		if(CanDraw(Context)){
			return true;
		}
		OpenSettings(Context);
		return false;
	}

	public static void OpenSettings(Context Context)
	{
		if(Build.VERSION.SdkInt < BuildVersionCodes.M){
			return;
		}
		var intent = new Intent(Settings.ActionManageOverlayPermission);
		intent.SetData(global::Android.Net.Uri.Parse($"package:{Context.PackageName}"));
		intent.AddFlags(ActivityFlags.NewTask);
		Context.StartActivity(intent);
	}
}
