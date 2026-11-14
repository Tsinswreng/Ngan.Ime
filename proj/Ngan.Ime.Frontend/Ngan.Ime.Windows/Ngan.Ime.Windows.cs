namespace Ngan.Ime.Windows;

using Microsoft.Extensions.DependencyInjection;
using Ngan.Ime.Infra;
using Ngan.Ime.Infra.Log;
using Ngan.Ime.Rime;
using Ngan.Ime.UI;
using Ngan.Ime.Windows.Voice;
using Avalonia;
using Tsinswreng.CsCore;

[Doc($$"""
Windows 桌面服務端入口。

*流程*：組合根（{{nameof(AppComposition)}}）建立容器 → 補本機配置默認值 →
啟動 TSF 管道服務端 → 開桌面主視窗（`--tsf` 時不開，只常駐）。

*配置默認值*：本機 Rime 路徑可由環境變數 `NGAN_RIME_DLL`／`NGAN_RIME_USER_DATA` 覆蓋，
其次才是下面的默認常量；**顯式配置永遠優先**（`AppCfg.SetDefaultIfBlank` 只補空值）。

#Eg[
	```sh
	Ngan.Ime.Windows.exe --tsf
	```
]
""")]
sealed class Program{

	[Doc("本機 Rime 動態庫的默認位置；只在配置與環境變數都沒給時使用。")]
	const str DfltRimeDllPath = @"D:\ENV\Rime\weasel-0.15.0\rime.dll";

	[Doc("本機 Rime 用戶資料的默認位置；同上。")]
	const str DfltUserDataDir = @"D:\Program Files\Rime\User_Data";

	[Doc("本服務端在日誌與配置裡的自稱。")]
	const str AppName = "Ngan.Ime.Windows";

	[Doc("解析環境變數覆蓋後的 Rime 動態庫路徑。")]
	static str RimeDllPath => Environment.GetEnvironmentVariable("NGAN_RIME_DLL") ?? DfltRimeDllPath;

	[Doc("解析環境變數覆蓋後的 Rime 用戶資料目錄。")]
	static str UserDataDir => Environment.GetEnvironmentVariable("NGAN_RIME_USER_DATA") ?? DfltUserDataDir;

	[Doc("裝配、補配置、啟動 TSF 與 Avalonia。")]
	[STAThread]
	public static void Main(str[] Args){
		// 常駐模式由選項物件表達，不再用全域可變旗標；同一個實例既進容器、也給裝配用。
		var Opt = new OptHost{ DaemonMode = Args.Contains("--tsf") };
		AppComposition.MkServices(new HostBootstrap{
			AppName = AppName,
			BaseDir = AppDirBaseDirMgr.Inst,
			// 桌面視窗通常沒有附著終端，故主控台＋檔案雙寫（檔案落在儲存區的 logs/）。
			Logger = MkrLogger.ConsoleEtFile(AppName),
			SetupEngine = Svc => Svc.SetupRime(),
			SetupHost = Svc => {
				Svc.AddSingleton(Opt);
				Svc.SetupWindows(Opt);
				WindowsVoiceComposition.AddWindowsVoiceInput(Svc);
			},
			AfterBuild = Svc => {
				SetupWindowsCfg();
				// TSF 服務端：接受系統輸入法轉來的按鍵，交給 Rime 並顯示候選窗。
				// 從容器解析而不是直接呼叫靜態類，啟動與否因此可替換、可測試。
				Svc.GetRequiredService<Tsf.ISvcTsfPipeServer>().Start();
			},
		});
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(Args);
	}

	[Doc("把本機實際路徑補進可寫配置層；仍以顯式配置優先。")]
	static void SetupWindowsCfg(){
		AppCfg.Inst.SetDefaultIfBlank(KeysAppCfg.Librime.DllPath, RimeDllPath);
		AppCfg.Inst.SetDefaultIfBlank(KeysAppCfg.Librime.RimeTraits.user_data_dir, UserDataDir);
		AppCfg.Inst.SetDefaultIfBlank(KeysAppCfg.Librime.RimeTraits.distribution_code_name, AppName);
		AppCfg.Inst.SetDefaultIfBlank(KeysAppCfg.Librime.RimeTraits.app_name, AppName);
	}

	[Doc("Avalonia 建置器；設計器也會呼叫，故不要移除。")]
	public static AppBuilder BuildAvaloniaApp(){
		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.WithInterFont()
			.LogToTrace();
	}
}
