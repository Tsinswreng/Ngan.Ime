using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace Ngan.Ime.Android;

/// 恢復通知的常量與構建邏輯集中放在這裡，
/// 讓 MainActivity 與 IME Service 共用同一套通知配置。
public static class AvalimeRecoveryNotification {
	/// 常駐通知 channel id。
	public const str ChannelId = "ngan.ime_ime_recovery";

	/// 常駐通知 id。
	public const i32 NotificationId = 1001;

	/// 點擊通知後觸發“強制重建鍵盤”的廣播 action。
	public const str ActionRecoverIme = "Tsinswreng.Ngan.Ime.ActionRecoverIme";

	/// Android 13+ 申請通知權限用的 request code。
	public const i32 PermissionRequestCode = 1001;

	const str ChannelName = "Ngan.Ime IME Recovery";

	/// 參照 Ngan.Dict 的做法，在 Activity 端確保通知存在。
	/// Android 13+ 若無權限，先請求權限並直接返回。
	public static void Ensure(Activity activity) {
		if(
			Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
			&& activity.CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted
		){
			activity.RequestPermissions(
				[Manifest.Permission.PostNotifications],
				PermissionRequestCode
			);
			return;
		}

		var notificationManager = activity.GetSystemService(Context.NotificationService) as NotificationManager;
		if(notificationManager is null){
			return;
		}

		EnsureChannel(notificationManager);
		notificationManager.Notify(NotificationId, BuildNotification(activity));
	}

	/// 供 Activity 的權限回調判斷是否是通知權限請求。
	public static bool IsPermissionRequest(i32 RequestCode) {
		return RequestCode == PermissionRequestCode;
	}

	/// 建立 Android 8+ 的通知 channel。
	static void EnsureChannel(NotificationManager notificationManager) {
		if(Build.VERSION.SdkInt < BuildVersionCodes.O){
			return;
		}

		var channel = new NotificationChannel(
			ChannelId,
			ChannelName,
			NotificationImportance.High
		){
			Description = "Ngan.Ime 輸入法黑屏時用於手動強制恢復"
		};
		channel.SetShowBadge(false);
		notificationManager.CreateNotificationChannel(channel);
	}

	/// 生成常駐通知。通知本體與 action 都直接發廣播給 IME 恢復接收器。
	static Notification BuildNotification(Context context) {
		var pendingIntent = CreateRecoverPendingIntent(context);
		var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
			? new Notification.Builder(context, ChannelId)
			: new Notification.Builder(context);

		builder
			.SetContentTitle("Ngan.Ime 正在運行")
			.SetContentText("若鍵盤黑屏，點此強制刷新鍵盤")
			.SetSmallIcon(Resource.Drawable.Icon)
			.SetOngoing(true)
			.SetOnlyAlertOnce(true)
			.SetAutoCancel(false)
			.SetCategory(Notification.CategoryService)
			.SetContentIntent(pendingIntent);
		SetPriorityCompat(builder);
		AddRecoveryAction(builder, context, pendingIntent);
		return builder.Build();
	}

	/// 點通知後直接發廣播，不必先打開 Activity。
	static PendingIntent CreateRecoverPendingIntent(Context context) {
		var intent = new Intent(context, typeof(AvalimeImeRecoveryReceiver));
		intent.SetAction(ActionRecoverIme);
		var flags = PendingIntentFlags.UpdateCurrent;
		if(Build.VERSION.SdkInt >= BuildVersionCodes.M){
			flags |= PendingIntentFlags.Immutable;
		}
		return PendingIntent.GetBroadcast(context, NotificationId, intent, flags)!;
	}

	/// 舊版 Android 仍依賴 priority 控制顯示強度。
	static void SetPriorityCompat(Notification.Builder builder) {
		if(Build.VERSION.SdkInt >= BuildVersionCodes.O){
			return;
		}

		builder.SetPriority((int)NotificationPriority.Max);
	}

	/// 給通知加一個顯式“刷新鍵盤”按鈕。
	static void AddRecoveryAction(Notification.Builder builder, Context context, PendingIntent pendingIntent) {
		if(Build.VERSION.SdkInt >= BuildVersionCodes.M){
			builder.AddAction(Resource.Drawable.Icon, "刷新鍵盤", pendingIntent);
			return;
		}

		builder.AddAction(Resource.Drawable.Icon, "刷新鍵盤", pendingIntent);
	}
}
