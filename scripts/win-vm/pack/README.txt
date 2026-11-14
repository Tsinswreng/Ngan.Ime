Ngan IME（Windows 版）
=====================

這個目錄裏就是執行輸入法所需的全部檔案，直接整包拷到目標機器即可。

## 檔案

  Ngan.Ime.Tsf.dll                  TSF 外殼（x64），由應用行程載入
  Ngan.Ime.Windows.exe              服務端（x86），載入 Rime 引擎、顯示候選窗
  Ngan.Ime.*.dll / *.dll            服務端執行時依賴（Skia、HarfBuzz 等）
  Ngan.Ime.Tsf.Registration.exe     註冊工具（x64，需 .NET 10 桌面執行時）
  install.ps1                       安裝（管理員）
  install-user.cmd                  加進目前使用者的輸入法清單（本人身分）
  uninstall.ps1                     反安裝（管理員）
  StartServer.vbs                   啟動服務端；安裝時建立排程工作在登入時呼叫

## 安裝

以管理員身分開啟 PowerShell，切到本目錄：

  powershell -ExecutionPolicy Bypass -File .\install.ps1 -RimeDll <rime.dll 路徑>

  -RimeDll 必須是 32 位的 rime.dll（例如小狼毫 weasel 的 rime.dll）。
  可用 -RimeUserData 指定一份 Rime 用戶資料；不指定或本機還沒有獨立副本時，
  安裝腳本會複製到 %LOCALAPPDATA%\NganIme\rime-user，不與小狼毫共用。
  確定要共用小狼毫那份時加 -ShareUserData（兩個前端同時 deploy 會互相干擾，慎用）。

然後以「目前使用者」身分（不要提權）雙擊 install-user.cmd，
最後登出再登入，讓已開著的應用重新載入外殼。

完成後用 Win+Space 切到 Ngan IME。

## 為甚麼要分兩步

機器層註冊（CLSID、輸入法 profile、分類）要管理員；
「加進輸入法清單」是每個使用者自己的設定，必須由本人執行，否則會加到管理員帳號底下。

## 服務端

安裝腳本會建立排程工作 NganIme-Server，在你登入時自動啟動服務端；
也可以直接雙擊 StartServer.vbs。服務端在背景執行，沒有視窗。

## 反安裝

管理員執行 uninstall.ps1。加 -KeepUserData 可保留 %LOCALAPPDATA%\NganIme 底下的用戶資料。

## 已知限制

- 服務端以 NativeAOT 發佈，Windows 的 WASAPI 語音輸入不可用；其他功能不受影響。
- 註冊工具是框架依賴版本，目標機器需要 .NET 10 桌面執行時（x64）。
