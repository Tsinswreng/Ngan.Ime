= Avalime

IME Frontend for Rime[https://github.com/rime/librime] with Avalonia

![](assets/2025-03-23-14-16-46.png)

**This project is still in an early stage of progress. That commit a candidate to a focusing input box is not supported by now**

But you can still build and try it manually on Windows:

see `Avalime.Rime/src/RimeSetup.cs`
modify the `dllPath` and `user_data_dir`  according to your actual path.

in Avalime.Windows, run
```bash
dotnet run -r win-x86 # win-x86 is for weasel0.15.0, or use other runtime identifier depending on your rime.dll
```

AOT is also supported, just change `dotnet run` into `dotnet publish -c Release`




= Avalime

IME Frontend for Rime with Avalonia

== Android Release 編譯

=== 前置條件

- .NET 10 SDK
- Android SDK（API 23+）
- 已連接的 Android 設備或模擬器
- librime.so 及依賴（`libc++_shared#image("assets\2026-07-12-21-01-01.png").so`、`libCsRimeLua.so`）已放入 `/sdcard/rime/`
- Rime 配置檔案已放入 `/sdcard/rime/`

=== 編譯與安裝

```bash
cd Avalime.Frontend/proj/Avalime.Android

# Release 編譯
dotnet publish -c Release

# 安裝到已連接的設備
adb install -r bin/Release/net10.0-android/publish/Tsinswreng.Avalime-Signed.apk

```

=== WSL NativeAOT 編譯

Android 工程的日常編譯仍然使用 Mono runtime；NativeAOT 只通過專用腳本顯式啟用。
腳本需要在 WSL 中執行，預設使用 `android-arm64`、`$HOME/Android/Sdk`，以及
`/mnt/d/ENV/_wsl/android-ndk-r27c`。如果本機路徑不同，可先設置環境變量：

```bash
cd Avalime/Avalime.Frontend/proj/Avalime.Android

ANDROID_SDK_DIRECTORY=/path/to/android-sdk \\
ANDROID_NDK_DIRECTORY=/path/to/linux-ndk \\
NATIVEAOT_ARTIFACTS_PATH=/path/to/artifacts \\
bash PublishNativeAot.sh
```

這個流程保持 Avalonia 12.0.4 不變，並使用 `PublishAot=true`、
`UseMonoRuntime=false` 和 `PublishTrimmed=true` 生成 NativeAOT APK。

=== 啟用輸入法

安裝後在 Android 系統設定中啟用 Avalime 輸入法：

1. 設定 → 語言與輸入 → 虛擬鍵盤 → 管理鍵盤
2. 開啟 Avalime
3. 在任意輸入框中切換到 Avalime

=== 首次使用

點擊鍵盤上方「連接 Rime」按鈕載入引擎，之後即可開始打字。
