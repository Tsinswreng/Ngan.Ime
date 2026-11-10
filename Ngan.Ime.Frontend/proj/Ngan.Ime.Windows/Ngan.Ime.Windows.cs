namespace Ngan.Ime.Windows;

using Ngan.Ime.Rime;
using Ngan.Ime.UI;
using Ngan.Ime.Windows.Voice;
using Avalonia;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsKeyNode;

/// Windows 桌面宿主入口。
/// 入口只做四件事：定日誌去向、定儲存區根目錄、接配置、裝配容器。
/// 具體註冊分屬各層的 DiXxx，這裡不再列舉服務。
sealed class Program{
	/// 本機 Rime 的默認位置；只在配置沒有給值時才使用。
	const str RimeDllPath = @"D:\ENV\Rime\weasel-0.15.0\rime.dll";
	/// 本機 Rime 用戶資料的默認位置；同上。
	const str UserDataDir = @"D:\Program Files\Rime\User_Data";
	/// 本宿主在日誌與配置裡的自稱。
	const str AppName = "Ngan.Ime.Windows";

	[STAThread]
	public static void Main(str[] Args){
		// 1: 建立容器並定下日誌去向。桌面視窗通常沒有附著終端，
		//    故主控台＋檔案雙寫；檔案落在儲存區的 logs/ 下。
		var Svc = new ServiceCollection();
		Svc.SetupCore();
		Svc.AddAppLog(MkrLogger.ConsoleEtFile(AppName));

		// 2: 儲存區根目錄＝可執行檔目錄（不是當前工作目錄）。
		Svc.AddSingleton<I_GetBaseDir>(AppDirBaseDirMgr.Inst);

		// 3: 配置必須早於任何取配置的地方：引擎、語音、界面字體都讀它。
		AppCfg.Inst.Setup(AppDirBaseDirMgr.Inst);
		Svc.SetupCfg(AppCfg.Inst);

		// 4: 各層裝配。Rime 由 DiRime 負責，Windows 只補本端實現。
		Svc.SetupRime();
		Svc.SetupWindows();
		Svc.SetupUi();
		WindowsVoiceComposition.AddWindowsVoiceInput(Svc);

		// 5: 宿主在配置缺失時補上本機默認值；顯式配置優先，不覆蓋用戶設定。
		SetupWindowsCfg();

		var Provider = Svc.BuildServiceProvider();
		Di.SvcProvider = Provider;

		BuildAvaloniaApp().StartWithClassicDesktopLifetime(Args);
	}

	/// <summary>
	/// Windows 端在啟動時把本機實際 Rime 路徑補進可寫配置層，
	/// 但仍以顯式配置優先，避免宿主默認值把用戶配置覆蓋掉。
	/// </summary>
	static void SetupWindowsCfg(){
		EnsureCfgOrDefault(KeysAppCfg.Librime.DllPath, RimeDllPath);
		EnsureCfgOrDefault(KeysAppCfg.Librime.RimeTraits.user_data_dir, UserDataDir);
		EnsureCfgOrDefault(KeysAppCfg.Librime.RimeTraits.distribution_code_name, AppName);
		EnsureCfgOrDefault(KeysAppCfg.Librime.RimeTraits.app_name, AppName);
	}

	/// <summary>
	/// 宿主只在配置缺失時補默認值；一旦配置文件已明確給值，就完全尊重配置。
	/// </summary>
	static void EnsureCfgOrDefault(KeyNode<str> Key, str DefaultValue){
		var Cur = AppCfg.Inst.Get(Key) ?? "";
		if(!string.IsNullOrWhiteSpace(Cur)){
			return;
		}
		AppCfg.Inst.SetNoSave(Key, DefaultValue);
	}

	public static AppBuilder BuildAvaloniaApp(){
		var Options = new FontManagerOptions {
			DefaultFamilyName = "孤鹜 筑紫明朝",
			FontFallbacks = new[] { new FontFallback { FontFamily = "Microsoft YaHei" } }
		};
		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.With(Options)
			.LogToTrace();
	}

}
