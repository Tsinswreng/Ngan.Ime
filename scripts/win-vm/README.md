# Windows VM 部署腳本

這批腳本是開發 Ngan IME 的 Windows TSF 版本時，用來操作 Windows 10 虛擬機、部署與除錯的輔助工具。
放在版控裡是為了換機器或重裝時可以直接重用。

## 檔案

- `vm-common.ps1`：`vmrun` 的小包裝（copy 進出 guest、在 guest 執行、列 guest 行程）。
- `register-install-start.ps1`：在 guest 內註冊 TSF DLL、加入使用者輸入法清單、以分離方式常駐啟動服務端。
- `pipetest.ps1`：直接連 `NganIme.Tsf.v1` 管道送按鍵碼，驗證 TSF 外殼與服務端的鏈路。
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

- guest 的 guest-ops 帳號是 `Tsinswreng`（內建 Administrator 改名）。
  密碼**不寫在版控裡**：`DeployWinVm` 從環境變數 `NGAN_VM_GUEST_PASS` 讀，帳號可用 `NGAN_VM_GUEST_USER` 覆蓋。
- 桌面使用者是 `22900`；INTERACTIVE 排程工作才能在該使用者的 session 執行程式。
- `rime.dll` 是 **x86**，服務端必須用 win-x86 發佈，否則載入會得到 `BadImageFormatException`。
- 服務端若用 cmd 視窗直接跑，關掉視窗會把行程一起帶走；請用 `start /b` 或啟動器腳本分離啟動。
- `StartServer.vbs`／`InstallStartServer.cmd`：手動啟動服務端並裝到桌面與啟動資料夾（VM 重啟後不必重新部署）。
- `deploy-guest.ps1`：部署腳本本體，由 `DeployWinVm` 複製進 guest 執行；必須維持 UTF-8 BOM。

## 一鍵部署（推薦）

日常「改完代碼 → 去 VM 試」用這一個命令（實作在 `proj/Ngan.Ime.Scripts`，CsSh 寫的）：

```powershell
# 在 Ngan.Ime/ 下
dotnet run --project proj/Ngan.Ime.Scripts -- DeployWinVm
```

它會：確認 VM（`-StartVm` 時自動開機）→ 增量建置三個產物（TSF x64／註冊工具 x64／服務端 x86）
→ 打包複製進 guest → 清掉改名遺留（舊排程工作與舊目錄）→ 停舊服務端 → 註冊新 TSF DLL（版本化檔名，避開 explorer 鎖檔）
→ 以桌面使用者啟動服務端 → 管道煙霧測試（斷言版本、吃掉、候選、擡起、F4 方案選單）→ 抓回 `last-run.log` 並印尾段。

### 發布 Windows 安裝包（不碰 VM，命令名 PubWinX86／別名 PubWin）

```powershell
dotnet run --project proj/Ngan.Ime.Scripts -- PubWinX86
```

產出 `artifacts/publish/win/`（已在 .gitignore），**所有執行時產物攤在同一層**：

- `Ngan.Ime.Tsf.dll`：TSF 外殼，x64 NativeAOT
- `Ngan.Ime.Windows.exe` 與 Skia／HarfBuzz 等依賴：服務端，x86 NativeAOT
- `Ngan.Ime.Tsf.Registration.exe` 與其框架依賴（約 20 個托管 dll）：x64，框架依賴
- `install.ps1`／`install-user.cmd`／`uninstall.ps1`／`StartServer.vbs`／`README.txt`

安裝流程：管理員跑 `install.ps1 -RimeDll <rime.dll>`，再以本人身分跑 `install-user.cmd`，
登出再登入。安裝腳本會建立登入自啟的排程工作 `NganIme-Server`，並把 Rime 用戶資料
複製到 `%LOCALAPPDATA%\NganIme\rime-user`，避免與小狼毫共用。

- `-Out <目錄>`：改輸出位置。
- `-KeepSymbols`：保留 `.pdb`。
- `-DryRun`：只印步驟。

腳本檔的編碼有講究：`.ps1` 要 UTF-8 BOM（PowerShell 5.1 對無 BOM 的 UTF-8 用 ANSI 解），
`.cmd` 用 GBK（主控台碼頁），`.vbs` 用 UTF-16 BOM（WSH 支援 Unicode）。

常用選項：

- `-Fast`：服務端改用 framework-dependent x86（秒級，但 guest 需要 x86 的 .NET 10 runtime）。
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

## 已踩過的坑（改這支命令前先看）

- `vmrun` 在 session 0 起的 `cmd.exe` 一律以 1 結束、且沒有任何輸出；同一條路徑上的
  `powershell.exe` 正常。故 guest 端一律用 PowerShell，不要用 `cmd /c`。
- `runProgramInGuest … powershell.exe -NoProfile -ExecutionPolicy Bypass -File <ps1>`
  在我們的呼叫路徑上會「瞬間回 1、腳本第一行都沒跑到」；改成
  `-Command "& '<ps1>'; exit $LASTEXITCODE"` 即正常。
- `copyFileFromHostToGuest` 偶爾「回 0 但沒寫檔」或「寫完不返回」。故每次複製後都用
  guest 端 PowerShell 比對檔案大小，逾時會殺掉子行程並重試一次。
- 部署包裡的 `Ngan.Ime.Rw.jsonc` 是主機上的配置（`D:/Program Files/Rime/...`），
  直接用會得到 `DllNotFoundException` 並讓服務端當場退出；`deploy-guest.ps1` 會在 guest 內
  把 `DllPath`／`user_data_dir`／`log_dir` 改寫成本機路徑。
- 管道的每條連線可送多個請求（逐行讀、逐行回）；早期「一請求一連線」的寫法已廢除。
- guest 端 `.ps1` 必須存成 **帶 BOM 的 UTF-8**：Windows PowerShell 5.1 對無 BOM 的
  檔案用 ANSI 解碼，中文註釋會變成亂碼並吞掉引號，整份腳本語法錯誤。

- *TSF 是「行程載入時」決定的*：重新註冊新 DLL 之後，已經開着的記事本／explorer 仍然跑舊 DLL
  （症狀：新加的路由看起來完全沒生效，例如方向鍵、Escape）。`DeployWinVm` 會結束 notepad、
  並列出仍持有舊 DLL 的行程；必要時加 `-RestartExplorer` 連 explorer 一起重啟。
  測試前請重開記事本。
