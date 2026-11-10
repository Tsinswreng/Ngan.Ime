namespace Ngan.Ime.Doc.Windows;

using Tsinswreng.CsCore;

[Doc($$"""
#H[用途][
	Windows 桌面宿主入口。它只做四件事：定日誌去向、定儲存區根目錄、接配置、裝配容器；
	具體服務清單分屬各層的 `DiXxx`，入口不再逐項列出。
]

#H[入口行為][
	`Program.Main()`：
	- 建立 `ServiceCollection`，先 `SetupCore()`，再 `AddAppLog(MkrLogger.ConsoleEtFile(...))`：
	  桌面視窗常無終端，故日誌同時寫 `logs/Ngan.Ime.Windows.log`（路徑由 `I_GetBaseDir` 決定）
	- 註冊 `I_GetBaseDir` 為 `AppDirBaseDirMgr`（可執行檔目錄）
	- `AppCfg.Inst.Setup(AppDirBaseDirMgr.Inst)` 接上雙源配置（出廠只讀 ＋ 日常可寫），並 `SetupCfg` 註冊四種寬度
	- 依序裝配 `SetupRime()`（RimeSetup／日誌源／按鍵處理器／SvcIme）、
	  `SetupWindows()`（`IOsKeyProcessor`、鍵盤宿主與剪貼簿的過渡實現）、`SetupUi()`（各 ViewModel）
	- `WindowsVoiceComposition.AddWindowsVoiceInput(...)` 接上 WASAPI 與火山識別
	- 在配置缺失時補上本機 Rime 路徑（`DllPath`、`user_data_dir`、`distribution_code_name`、`app_name`）；
	  顯式配置優先，不覆蓋用戶設定
	- `Di.SvcProvider = provider`
	- `StartWithClassicDesktopLifetime(...)`
]

#H[與舊版差異][
	- 不再有 `InitCfg()`：配置檔的接法收在 `AppCfg.Setup(I_GetBaseDir)`，桌面與 Android 共用同一套
	- 不再要求 `Ngan.Ime.Ro.jsonc` 必須存在：缺檔即空源，取值走鍵上宣告的默認值
	- `WindowsFileLogger` 已由 `Ngan.Ime.Infra.Log.FileLogger`（經 `MkrLogger.ConsoleEtFile`）取代
]
""")]
file class _{
}
