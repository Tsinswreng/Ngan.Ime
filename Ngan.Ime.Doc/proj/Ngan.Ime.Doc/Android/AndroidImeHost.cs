namespace Ngan.Ime.Doc.Android;

using Tsinswreng.CsCore;

[Doc($$"""
#H[用途][
	Android 端是標準 `InputMethodService` 宿主。
	它把 Avalonia 視圖包進 `AvaloniaView`，並回傳給系統輸入法窗口。
]

#H[註冊][
	輸入法由：
	- `[Service]`
	- `android.view.InputMethod`
	- `@xml/ime_method`
	組成。
]

#H[窗口行為][
	`OnEvaluateFullscreenMode()` 會關掉全屏提取。
	`OnConfigureWindow()` 把高度限制成半屏。
	輸入法服務顯式使用 `MyTheme.Ime`，避免沿用 Activity 的透明窗口配置。
	隱藏鍵盤時仍只調 `RequestHideSelf(0)`；
	IME 宿主現在改爲仿照 `AvlnImeDemo` 的方案：
	- `OnCreateInputView()` 只創建一次 `LoggingAvaloniaView`
	- 後續 hide/show 都複用同一個 view，不再反覆重建整棵 Avalonia UI
	- 在 `OnCreateInputView()` 與 `OnConfigurationChanged()` 前，宿主都會先把複用 view 從舊父節點摘下；這是爲了避免橫豎屏切換後 `InputMethodService` 內部重新 `setInputView(...)` 時因“child already has a parent”而崩潰
	- `OnStartInputView()` 會先把該 view 從舊父節點摘下，再 `SetInputView(...)` 重掛回窗口
	- `OnWindowShown()` 只補 `RequestLayout()` / `Invalidate()` 的輕量刷新，不再二次重掛；這是爲了減少橫豎屏切換後首次彈出時的額外 surface 重建成本
	- Android 宿主不再在 `Application` 或 `InputMethodService.OnCreate()` 階段提前觸發 `RimeWarmup`
	- 現在先保證 `OnCreateInputView()` / `OnStartInputView()` 能把 IME UI 拉起來
	- 真正的 Rime 連接改由 `VmIme` 構造後異步調 `ISvcIme.ConnectAsy()`；首屏先展示日誌，成功後僅在用戶未主動切換 body 頁時才回到鍵盤
	- 另外會啓動一個常駐前台通知；若偶發黑屏且無法自行恢復，用戶可點通知觸發“強制恢復”
	- “強制恢復”路徑不改動默認 hide 行爲，而是丟棄當前 `LoggingAvaloniaView`、重建新的 `MainView` 與 `AvaloniaView`，再重掛回 IME window
	- Android 9+ 還會額外調 `RequestShowSelf(ShowFlags.Forced)`，嘗試把重建後的鍵盤重新拉回可見狀態

	這條路徑的出發點是：
	相比“每次 hide 後強制重建整棵 view”，單例複用 + 重掛更接近系統原生輸入法窗口的生命週期，
	也更符合已驗證過的 `AvlnImeDemo` 行爲。

	另外，Android 端現在加入了分體懸浮鍵盤模式：
	- 用戶在工具欄手動點擊分體按鈕後，狀態持久化保存
	- 分體偏好仍會寫回可寫配置，但保存動作已改到後台執行，避免點擊分體按鈕時把磁盤 IO 壓在 UI 線程上
	- 若開啓分體，IME 主窗口不再承載整塊鍵盤，而是縮成極薄佔位 view
	- 左右兩半鍵盤與中間窄頂條改走 `TYPE_APPLICATION_OVERLAY`
	- 宿主會在 service 建立後預先創建 split overlay 的 Avalonia 內容樹，將首次切換分體時的 view 構造成本前移
	- split overlay 現在改成「首次 `AddView(...)` 後常駐」，後續切換分體或 hide/show 主要通過 `Visibility` 顯隱，而不是每次都 remove/add 四個 window
	- 中間窄頂條不再交給 Android `WrapContent` 自由量測，而是宿主明確給出「預編輯 + 頂條」的固定高度；這是爲了避免 overlay 把中間本應留給底下 App 的區域整塊蓋黑
	- 宿主另外會給頂條與鍵盤之間留極小安全邊界，避免不同縮放與像素取整下，頂條下沿壓住第一排鍵位上沿
	- 這樣中間 50% 區域不屬於 IME 視窗，可把點擊交回底下 App
	- 缺少 overlay 權限時，宿主會跳轉系統設置頁，並回退到普通整體鍵盤，避免進入不可用狀態
]

#H[默認字體][
	`Application.CustomizeAppBuilder` 中設置 `DefaultFamilyName = "serif"`（帶襯線字體）。
	替代了原來的 `WithInterFont()`（無襯線）。
]

#H[文字輸出][
	`OnCreate` 中設定 `ImeState.OsKeyProcessor` 為 `AndroidOsKeyProcessor`，
	並訂閱 `ImeState.OnCommit` 事件。
	- `OnCommit`：收到 commit 文字後，通過 `CurrentInputConnection.CommitText` 輸出。
	- `AndroidOsKeyProcessor`：將 Rime 未處理的按鍵轉發給 OS（Backspace→`Delete`、Enter→`Enter`、普通文字鍵→`CommitText`）。
	- 現在 `AndroidOsKeyProcessor` 外面再包一層 `KeyRemappingOsKeyProcessor`
		- 只處理 `UnhandledKeys`
		- 規則由 `IKeyEventRemapper` / `IKeyEventRemapRule` 抽象承載
		- 因此不會把映射污染到 Rime 自身的按鍵語義
		- 工具欄 `映` 開關打開後，當前先啓用 `Backspace -> Alt_R`
	- 若按鍵事件攜帶 `KeyBoardState` 中的 Ctrl/Shift/Alt/Meta 狀態，則改為 `SendKeyEvent` 發送帶 meta state 的系統組合鍵（如 Ctrl+A）。
	- `Shift` 鎖定在上層抽象裡只體現爲 `AllDownKeys` 含有 `Shift_L`；Android 宿主層再根據這個狀態決定如何落地。
	- 對字符類組合鍵，通常直接用 `metaState` 即可；但對 `Shift+Left` / `Shift+Home` 這類導航選區鍵，某些宿主只認真實修飾鍵序列，因此 Android 端會把該抽象翻譯成 `Shift Down -> Navigation Down/Up -> Shift Up`。
	若當前沒有 `InputConnection`，則跳過輸出並記錄日誌。

	由於按鍵輸入現在通過 `ImeState.InputSafely(...)` 在後台執行，
	`OnCommit` 回調裡需要顯式切回 Android 主線程，再調 `CurrentInputConnection.CommitText`。
]

#H[依賴注入與日誌][
	Android 端在 `InputMethodService.OnCreate()` 組裝 `ServiceCollection`，並寫入全局 `Di.SvcProvider`。
	`Di` 是當前全局 DI 中心；UI / Core 側若拿服務，統一通過 `Di.GetRSvc<T>()`。
	`ISvcIme` 在 Android 端不再直接使用空殼基類，而是註冊 `Ngan.Ime.Rime.SvcIme`。
	這樣 `Ngan.Ime.UI` 仍只依賴 `Ngan.Ime.Core`，但真正的 Rime 連接、狀態同步與 option 切換由 `Ngan.Ime.Rime` 層實現。
	`ILogger` 也註冊爲 `AppLog.Inst`，方便不便注入的地方與可注入的地方共用同一條日誌出口。
	`AppLog.Inst.InnerLogger` 在 Android `Application` 啟動時切到 `AndroidLogger`，Release 也可直接進 logcat。
]

#H[語音輸入宿主][
	Android 語音輸入通過原生 `AudioRecord` 採集 16 kHz、16-bit、單聲道 PCM，
	後續火山 WebSocket 協議與語音頁狀態機直接複用跨平台程序集。

	`Application` 的普通 Activity 容器與 `InputMethodService.OnCreate()` 的 IME 容器都調用同一 Android 語音組合根，
	避免主界面可解析語音服務、輸入法窗口卻缺少依賴。

	錄音權限採用 Activity 中轉：
	- Manifest 聲明 `RECORD_AUDIO`
	- IME Service 發現缺權限時，以專用 action 打開 `MainActivity`
	- 該啓動路徑只申請麥克風，不觸發普通啓動的通知與懸浮窗權限流程
	- 權限結果返回後立即關閉 Activity，讓用戶回到原應用並再次點擊錄音
]

#H[原生庫載入][
	Android 端不再把 `librime.so` 放進 APK 的 `lib/arm64-v8a/` 標準原生庫路徑。
	原因是某些 `librime_jni.so` 變體在 `JNI_OnLoad` 會主動查找宿主 App 裡的 Java 類，
	如果被系統在應用啟動期自動 `loadLibrary`，會過早執行 JNI 初始化並直接崩潰。

	現在做法是：
	- 將原生庫內容以非 `.so` 文件名（如 `librime.bin`）作為 `AndroidAsset` 打包進 APK
	- `Ngan.Ime.Ro.jsonc` 放在 App 私有目錄，只保存 `RwCfgPath`
	- 若 `/sdcard/Android/.../files/Ngan.Ime.Rw.jsonc` 不存在，則從 APK 資產複製一份初始配置
	- 啟動時解包 `librime.bin` 到 App 私有目錄並還原成 `librime.so`
	- 再把實際 `DllPath`、`user_data_dir`、`distribution_code_name`、`app_name` 寫入雙源配置的可寫層，供 `RimeSetup` 按配置讀取
]

""")]
file class _{
}
