Ngan IME（Windows 版）
=====================

這個目錄裏就是執行輸入法所需的全部檔案；安裝腳本會把整份複製到
%LOCALAPPDATA%\NganIme\v<時間戳>，所以「每次安裝」都是一個獨立的輸入法：
自己的 CLSID、自己的輸入法 profile、自己的服務端、自己的管道、自己的 Rime 用戶資料。
多個版本可以並存，各自運作、各自卸載，互不影響。

## 檔案

  Ngan.Ime.Tsf.dll                  TSF 外殼（x64），由應用行程載入
  Ngan.Ime.Windows.exe              服務端（x86），載入 Rime 引擎、顯示候選窗
  Ngan.Ime.*.dll / *.dll            服務端執行時依賴（Skia、HarfBuzz 等）
  Ngan.Ime.Tsf.Registration.exe     註冊工具（x64，需 .NET 10 桌面執行時）
  Ngan.Ime.Rw.jsonc                 本機配置：至少要填 Librime.DllPath
  install.ps1                       安裝（管理員）
  install-user.cmd                  只把本版本加進使用者的輸入法清單（本人身分）
  uninstall.ps1                     卸載本版本（管理員）
  StartServer.vbs                   手動啟動服務端（排程工作直接指向 exe，本檔是備用入口）

## 安裝

以管理員身分開啟 PowerShell，切到本目錄：

  powershell -ExecutionPolicy Bypass -File .\install.ps1

腳本會：
  1. 複製到 %LOCALAPPDATA%\NganIme\v<時間戳>；
  2. 生成這一版的 CLSID 與輸入法 profile（寫進該目錄的 identity.txt）；
  3. 複製一份 Rime 用戶資料到該版本目錄（不想複製就加 -ShareUserData）；
  4. 註冊機器層 COM／profile，並把新 profile 加進目前使用者的輸入法清單；
  5. 建立這一版的排程工作 NganIme-Server-v<版本>，登入時自動啟動服務端。

安裝前要先填好 Ngan.Ime.Rw.jsonc：

  Librime.DllPath            必填，32 位元的 rime.dll（例如小狼毫 weasel 的 rime.dll）
  Librime.RimeTraits.user_data_dir
                             可留空。留空時安裝腳本會給這個版本一個獨立目錄，第一次啟動由 Rime 部署。
                             填了路徑就會被複製一份給這個版本（-ShareUserData 則直接共用）。

裝完之後登出再登入（或重開要用的程式），讓應用重新載入外殼，然後用 Win+Space 選
「Ngan IME v<版本>」。

## 為甚麼要版本化

TSF 外殼是被應用行程載入的 DLL。跑着的行程不放手，任何安裝器都換不掉它的映像，
所以「就地覆蓋」永遠對已開着的程式無效，而且分不清用的是哪一版。
裝到新目錄＋新的 CLSID，等於多一個獨立輸入法：新開的程式用新版，舊程式繼續用舊版。

## 反安裝與清理

管理員執行本目錄的 uninstall.ps1：只移除這一版的 profile、CLSID、排程工作與目錄。
加 -KeepUserData 可保留版本目錄。

舊版本目錄若已沒人用（沒在註冊表、沒被載入、沒有排程工作），可用：

  dotnet run --project proj/Ngan.Ime.Scripts -- CleanWin

只裝不卸的話，每個版本都會留在輸入法清單與排程工作裏（每次安裝都是一個獨立輸入法）。
要一次清掉「除了最新版以外的全部舊版」，用（需管理員，它要改 HKLM）：

  dotnet run --project proj/Ngan.Ime.Scripts -- CleanWin -UninstallOld
  dotnet run --project proj/Ngan.Ime.Scripts -- CleanWin -UninstallOld -DryRun   只印計劃

## 已知限制

- 服務端以 NativeAOT 發佈，Windows 的 WASAPI 語音輸入不可用；其他功能不受影響。
- 註冊工具是框架依賴版本，目標機器需要 .NET 10 桌面執行時（x64）。
