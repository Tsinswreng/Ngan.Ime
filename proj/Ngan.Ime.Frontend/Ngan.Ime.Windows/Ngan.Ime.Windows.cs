namespace Ngan.Ime.Windows;

using Microsoft.Extensions.DependencyInjection;
using Ngan.Ime.Infra;
using Ngan.Ime.Infra.Log;
using Ngan.Ime.Rime;
using Ngan.Ime.UI;
using Ngan.Ime.Windows.Infra;
using Ngan.Ime.Windows.Voice;
using Avalonia;

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
		// 先裝崩潰轉儲：之後不管崩在哪一步，至少會留下一份 dump。
		CrashDump.Install();
		var Opt = new OptHost{ DaemonMode = Args.Contains("--tsf") };
		if (Opt.DaemonMode) {
			// 單實例：照 Weasel 的 `ServerImpl::Start`——已有同名互斥就直接退出。
			// 外殼在探測失敗六次之後會自動拉起服務端，幾十個應用會同時去拉；
			// 沒有這道互斥就會起來好幾支，各自開一個 Rime 引擎搶同一份 user data。
			if (!SingleInstance.TryAcquire()) {
				Console.Error.WriteLine("[Program] 這個安裝目錄已經有一個服務端在跑，本次啟動退出。");
				return;
			}
			// 常駐模式不要有主控台：關掉承載它的終端機／安裝視窗會送 CTRL_CLOSE_EVENT，
			// 系統仍會結束行程（SetConsoleCtrlHandler 擋不住），而且死得無聲無息
			// （沒有崩潰事件、沒有 dump，看起來就像「輸入法突然沒了」）。
			// Weasel 的服務端本來就是沒有主控台的 GUI 程式，這裡用 FreeConsole 達到同樣狀態。
			ConsoleCtrl.Detach();
			// 保險：若 Detach 失敗（沒有主控台或 FreeConsole 失敗），至少讓 Ctrl+C／Ctrl+Break 不生效。
			ConsoleCtrl.IgnoreControlEvents();
		}
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
				// 模式切換提示：訂閱引擎狀態，切中／英、簡／繁時在光標旁閃一下。
				Svc.GetRequiredService<Tsf.SvcModeTip>().Start();
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
