= Ngan.Ime

IME Frontend for Rime[https://github.com/rime/librime] with Avalonia

![](assets/2025-03-23-14-16-46.png)

**This project is still in an early stage of progress. That commit a candidate to a focusing input box is not supported by now**

But you can still build and try it manually on Windows:

see `proj/Ngan.Ime.Rime/RimeSetup.cs`
modify the `dllPath` and `user_data_dir`  according to your actual path.

in `proj/Ngan.Ime.Frontend/Ngan.Ime.Windows`, run
```bash
dotnet run -r win-x86 # win-x86 is for weasel0.15.0, or use other runtime identifier depending on your rime.dll
```

AOT is also supported, just change `dotnet run` into `dotnet publish -c Release`




= Ngan.Ime

IME Frontend for Rime with Avalonia

== 目錄結構

所有專案都在 `proj/` 下，一個專案一個目錄，目錄名與專案名相同：

```text
proj/
  Ngan.Ime.Core/                 # 平台無關的抽象：按鍵模型、Ime 契約、配置、日誌
  Ngan.Ime.Rime/                 # librime 適配層（原生指針、會話、鍵碼轉換）
  Ngan.Ime.Doc/                  # 文檔專案：以 [Doc] 描述整體設計，可編譯檢查
  Ngan.Ime.Scripts/              # 開發期命令：Test／RunWin／GenAppJsonCtx／DeployWinVm
  Ngan.Ime.Test/                 # 單元測試
  Ngan.Ime.Frontend/             # 所有與 Avalonia 有關的專案
    Ngan.Ime.UI/                 # 共用界面：View／ViewModel、鍵盤布局、主題
    Ngan.Ime.Backend/            # 後端能力：語音辨識（火山引擎）
    Ngan.Ime.Windows/            # Windows 宿主：桌面視窗 ＋ TSF 管道宿主端
    Ngan.Ime.Android/            # Android 輸入法服務宿主
    Ngan.Ime.Desktop/            # 桌面預覽宿主（跨平台）
    Ngan.Ime.Browser/            # 瀏覽器宿主
    Ngan.Ime.iOS/                # iOS 宿主
    Ngan.Ime.Tsf.Windows/        # TSF 原生組件（NativeAOT，手寫 COM，不含 CLR）
    Ngan.Ime.Tsf.Registration/   # TSF 註冊工具
  _shared/                       # 全專案共用的 TypeAlias／GlobalUsing
  Directory.Build.props          # 全專案地基：TFM、本地庫引用、DI 套件
  Directory.Build.targets        # 必須晚於 NuGet props 的設定
```

`proj/Directory.Build.props` 是所有專案的地基；`proj/Ngan.Ime.Frontend/Directory.Build.props`
在其上再加 Avalonia 版本與界面層共用的引用。新增專案時不要各自重寫 TFM 與本地庫清單。


== Android Release 編譯

=== 前置條件

- .NET 10 SDK
- Android SDK（API 23+）
- 已連接的 Android 設備或模擬器
- librime.so 及依賴（`libc++_shared#image("assets\2026-07-12-21-01-01.png").so`、`libCsRimeLua.so`）已放入 `/sdcard/rime/`
- Rime 配置檔案已放入 `/sdcard/rime/`

=== 編譯與安裝

```bash
cd proj/Ngan.Ime.Frontend/Ngan.Ime.Android

# Release 編譯
dotnet publish -c Release

# 安裝到已連接的設備
adb install -r bin/Release/net10.0-android/publish/Tsinswreng.Ngan.Ime-Signed.apk

```

=== WSL NativeAOT 編譯

Android 工程的日常編譯仍然使用 Mono runtime；NativeAOT 只通過專用腳本顯式啟用。
腳本需要在 WSL 中執行，預設使用 `android-arm64`、`$HOME/Android/Sdk`，以及
`/mnt/d/ENV/_wsl/android-ndk-r27c`。如果本機路徑不同，可先設置環境變量：

```bash
cd Ngan.Ime/proj/Ngan.Ime.Frontend/Ngan.Ime.Android

ANDROID_SDK_DIRECTORY=/path/to/android-sdk \\
ANDROID_NDK_DIRECTORY=/path/to/linux-ndk \\
NATIVEAOT_ARTIFACTS_PATH=/path/to/artifacts \\
bash PublishNativeAot.sh
```

這個流程保持 Avalonia 12.1.6 不變，並使用 `PublishAot=true`、
`UseMonoRuntime=false` 和 `PublishTrimmed=true` 生成 NativeAOT APK。

=== 啟用輸入法

安裝後在 Android 系統設定中啟用 Ngan.Ime 輸入法：

1. 設定 → 語言與輸入 → 虛擬鍵盤 → 管理鍵盤
2. 開啟 Ngan.Ime
3. 在任意輸入框中切換到 Ngan.Ime

=== 首次使用

點擊鍵盤上方「連接 Rime」按鈕載入引擎，之後即可開始打字。
