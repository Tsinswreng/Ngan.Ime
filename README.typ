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

=== 啟用輸入法

安裝後在 Android 系統設定中啟用 Avalime 輸入法：

1. 設定 → 語言與輸入 → 虛擬鍵盤 → 管理鍵盤
2. 開啟 Avalime
3. 在任意輸入框中切換到 Avalime

=== 首次使用

點擊鍵盤上方「連接 Rime」按鈕載入引擎，之後即可開始打字。

