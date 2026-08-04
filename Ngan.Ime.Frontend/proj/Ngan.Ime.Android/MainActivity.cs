using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using Ngan.Ime.Android.Voice;

namespace Ngan.Ime.Android;

[Activity(
	Label = "Ngan.Ime",
	Theme = "@style/MyTheme.NoActionBar",
	Icon = "@drawable/icon",
	MainLauncher = true,
	ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
	protected override void OnCreate(Bundle? savedInstanceState) {
		base.OnCreate(savedInstanceState);
		if(RequestVoicePermissionIfNeeded(Intent)){
			return;
		}
		AvalimeRecoveryNotification.Ensure(this);
		AvalimeOverlayPermission.Ensure(this);
	}

	/// SingleTop 權限入口被重用時，從新 Intent 繼續處理錄音權限。
	protected override void OnNewIntent(global::Android.Content.Intent? intent) {
		base.OnNewIntent(intent);
		Intent = intent;
		RequestVoicePermissionIfNeeded(intent);
	}

	/// 只有從輸入法語音入口啓動時才申請權限；普通主界面啓動不主動打擾用戶。
	bool RequestVoicePermissionIfNeeded(global::Android.Content.Intent? intent) {
		if(intent?.Action != AndroidVoicePermission.RequestAction){
			return false;
		}
		if(AndroidVoicePermission.IsGranted(this)){
			Finish();
			return true;
		}
		RequestPermissions(
			[global::Android.Manifest.Permission.RecordAudio],
			AndroidVoicePermission.RequestCode
		);
		return true;
	}

	/// 參照 Ngan.Dict 的做法，權限授予後立刻補發常駐恢復通知。
	public override void OnRequestPermissionsResult(
		int requestCode, string[]? permissions, Permission[]? grantResults
	){
		base.OnRequestPermissionsResult(
			requestCode,
			permissions ?? [],
			grantResults ?? []
		);
		if(requestCode == AndroidVoicePermission.RequestCode){
			// 關閉專用權限 Activity，讓系統返回先前使用輸入法的應用。
			Finish();
			return;
		}
		if(!AvalimeRecoveryNotification.IsPermissionRequest(requestCode)){
			return;
		}
		if(grantResults is null || grantResults.Length == 0){
			return;
		}
		if(grantResults[0] != Permission.Granted){
			return;
		}
		AvalimeRecoveryNotification.Ensure(this);
	}
}
