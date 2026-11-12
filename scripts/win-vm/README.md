# Windows VM 部署腳本

這批腳本是開發 Ngan IME 的 Windows TSF 版本時，用來操作 Windows 10 虛擬機、部署與除錯的輔助工具。
放在版控裡是為了換機器或重裝時可以直接重用。

## 檔案

- `vm-common.ps1`：`vmrun` 的小包裝（copy 進出 guest、在 guest 執行、列 guest 行程）。
- `register-install-start.ps1`：在 guest 內註冊 TSF DLL、加入使用者輸入法清單、以分離方式常駐啟動宿主。
- `pipetest.ps1`：直接連 `NganIme.Tsf.v1` 管道送按鍵碼，驗證 TSF 與宿主的鏈路。
- `peek_dump.py`：解析 Windows minidump（module 清單、exception、暫存器、stack 掃描），不需要 cdb。
- `Capture-VmwareWindow.ps1`：抓 VMware 視窗截圖，用於回報畫面。

## 典型流程

```powershell
. .\vm-common.ps1
Copy-ToGuest -Local 'hostx86.zip' -Remote 'C:\NganImeVm\bundle\hostx86.zip'
Invoke-Guest -Command 'C:\Windows\System32\cmd.exe' -Arguments '/c C:\NganImeVm\run-setup-ngan.cmd'
Invoke-Guest -Command 'C:\Windows\System32\cmd.exe' -Arguments '/c C:\NganImeVm\run-register-install-start.cmd'
Get-GuestProcesses | Select-String 'Ngan.Ime'
```

## 注意

- guest 的 guest-ops 帳號是 `Tsinswreng`（內建 Administrator 改名），密碼同帳號名。
- 桌面使用者是 `22900`；INTERACTIVE 排程工作才能在該使用者的 session 執行程式。
- `rime.dll` 是 **x86**，宿主必須用 win-x86 發佈，否則載入會得到 `BadImageFormatException`。
- 宿主若用 cmd 視窗直接跑，關掉視窗會把行程一起帶走；請用 `start /b` 分離啟動。

## 一鍵部署（推薦）

日常「改完代碼 → 去 VM 試」用這一個命令（實作在 `proj/Ngan.Ime.Scripts`，CsSh 寫的）：

```powershell
# 在 Ngan.Ime/ 下
dotnet run --project proj/Ngan.Ime.Scripts -- DeployWinVm
```

它會：確認 VM（`-StartVm` 時自動開機）→ 增量建置三個產物（TSF x64／註冊工具 x64／宿主 x86）
→ 打包複製進 guest → 停舊宿主 → 註冊新 TSF DLL（版本化檔名，避開 explorer 鎖檔）
→ 以桌面使用者啟動宿主 → 管道煙霧測試 → 抓回 `last-run.log` 並印尾段。

常用選項：

- `-Fast`：宿主改用 framework-dependent x86（秒級，但 guest 需要 x86 的 .NET 10 runtime）。
- `-NoBuild`：跳過建置，直接部署上次的產物。
- `-StartVm`：VM 沒開機時自動開（nogui）。
- `-OpenNotepad`：部署完在桌面開記事本，方便立刻測。
- `-SkipSmoke`：跳過管道煙霧測試（只想快點看到界面時）。
- `-DryRun`：只印步驟與判定，不建置、不碰 VM。

產物與日誌落在 `Ngan.Ime/scripts/win-vm/out/`（已 gitignore）。
guest 端需要先有一次性的目錄與 Rime 資源（`C:\NganImeVm\rime\rime.dll`、`rime-user\`），
腳本不主動複製它們（見下面「注意」）。

下面的 `register-install-start.ps1` 等仍是底層零件；`DeployWinVm` 不依賴它們，
只在需要手動排查時單獨使用。
