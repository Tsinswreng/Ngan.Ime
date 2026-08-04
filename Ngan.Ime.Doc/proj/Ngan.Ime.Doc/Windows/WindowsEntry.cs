namespace Ngan.Ime.Doc.Windows;

using Tsinswreng.CsCore;

[Doc($$"""
#H[用途][
	Windows 入口仿照 Android 宿主，先初始化雙源配置，再組裝 DI，最後啟動 Avalonia desktop lifetime。
]

#H[入口行為][
	`Program.Main()`：
	- 從輸出目錄讀取 `Ngan.Ime.Ro.jsonc` / `Ngan.Ime.Rw.jsonc`
	- 將 `AppLog` 寫入輸出目錄的 `Ngan.Ime.Windows.log`，以保留沒有終端的桌面啟動、Rime 與語音診斷資料；不記錄配置憑據或音頻內容
	- 把 `RoCfg` / `RwCfg` 掛到 `AppCfg.Inst`
	- 再把 Windows 本機的 `Librime.DllPath`、`user_data_dir`、`distribution_code_name` 覆寫到可寫配置層
	- 註冊 `RimeSetup.Inst`，讓桌面端 `SvcIme` 和 `RimeKeyProcessor` 共用同一個 Rime session
	- 註冊 `IOsKeyProcessor`
	- 註冊桌面宿主 `IKeyboardHost`、`IClipboardService`
	- 註冊 `IImeKeyProcessor -> RimeKeyProcessor`
	- 註冊 `ImeUiState`
	- 註冊 `ISvcIme -> Ngan.Ime.Rime.SvcIme`
	- `Di.SvcP = provider`
	- `StartWithClassicDesktopLifetime(...)`
]

""")]
file class _{
}
