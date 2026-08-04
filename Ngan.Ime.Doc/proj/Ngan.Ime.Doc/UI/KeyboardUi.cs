namespace Ngan.Ime.Doc.UI;

using Tsinswreng.CsCore;

[Doc($$"""
#H[用途][
	UI 端分成 `ViewIme`、候選欄、工具欄、預編輯欄、剪貼板頁、語音輸入頁和純鍵盤。
	`MainView` 直接掛 `ViewIme`。
	候選詞完整鏈路與生命週期另見 `CandidateLifecycle`。
]

#H[鍵盤結構][
	`ViewIme` 是輸入法總視圖。
	它組裝：
	- `ViewPreedit`
	- `ViewToolBar`
	- `ViewCandidatesBar`
	- `ViewClipboard`
	- `ViewVoiceInput`
	- `ViewKeyBoard`

	`ViewKeyBoard` 現在只負責純鍵盤按鍵區。

	按鍵之間不能有無法點擊的縫隙。分隔線僅是視覺上的作用，邏輯上每個按鍵都緊密相連。
	鍵盤使用 `Grid` 星號列佈局，按鍵間無 `Margin`/`Spacing`，視覺分隔完全由 `BorderThickness = 0.5` 的邊框線實現。
	這樣確保點擊兩個按鍵交界處時不會因間隔而丟失點擊。

	`VmIme` 是總控狀態：
	- 自動異步連接 Rime
	- 根據 `RimeStatus.is_composing` 決定顯示工具欄還是候選欄
	- 控制是否進入剪貼板頁
]

#H[DI 分層][
	當前 UI 層依賴注入規則是：
	- **只有 View 層可以直接調全局 `Di.GetRSvc<T>()`**
	- `Di` 是唯一的全局 DI 入口；不要再經由 `App` 暴露 service locator
	- `Vm` / `RimeConnectionState` / 其他非 View 類型一律使用構造函數注入
	- **所有 View 都必須提供 `public` 無參構造函數**
	- View 本身視爲無狀態；不要把 `Vm` 或其他運行時狀態通過 View 構造參數傳入
	- 除 `MainView` / `MainWindow` 這種項目模板自帶入口外，`Views/` 下每個 View/Vm 都必須放在自己的專屬同名文件夾中（如 `Views/ViewIme/ViewIme.cs`、`Views/ViewIme/VmIme.cs`）

	也就是說：
	- `ViewIme` / `ViewKeyBoard` / `ViewInput` 這些 View 內部只負責通過 `Di.DiOrMk<Ctx>()` 解析自身 `Ctx`
	- 子 View 也各自解析自己的 `Ctx`；不要在父 View 中手動 `new VmXxx(...)` 再塞給子 View
	- 禁止寫成 `new ViewXxx(vm)` 這種帶狀態構造方式
	- `VmIme` / `VmCandidatesBar` / `VmInput` / `VmClipboard` / `VmToolBar` / `VmKey` / `VmRimeLog` 內部都不應再直接碰 `Di`
	- `RimeConnectionState` 這類 service 內部若需要 `ImeState` 等依賴，也應在構造函數注入，不可反查全局容器

	這條約束是爲了把 View 與非 View 的依賴邊界固定住，避免 VM / service 變成隱式 service locator。
]

#H[交互][
	`VmKeyBoard` 維持佈局狀態。
	`VmKey` 負責按鍵、長按與滑動動作。
	`KeyCfg.IsRepeat` 控制長按後是否持續重複：長按 400ms 後首次觸發 `LongPress`，之後每 50ms 觸發一次 `Click`。
	退格鍵（`Backspace`）設了 `IsRepeat=true`，長按可連續刪除。
	空格兩側的左右方向鍵把長按與上滑手勢分別映射到 `Home` / `End`：
	- `←`：長按或上滑發送 `Home`
	- `→`：長按或上滑發送 `End`
	普通點擊仍保留 `Left` / `Right`，因此“逐字移動”與“跳到行首/尾”共存。
	退格鍵原本還試過「上滑隱藏輸入法」；
	目前已在 `ViewKeyBoard` 用 `#if false` 編譯期關掉。
	原因是 Android IME 從鍵盤內部主動 hide 之後，再次 show 的生命周期與“點空白失焦”或“系統返回鍵收起”不等價，
	此前已反覆導致黑屏與狀態異常，所以先保留代碼入口但不把它掛回正式手勢。
	`KeyCfg.SwipeLeftAction` 支援自訂滑動動作（不限於發送按鍵），用於 Y 鍵左滑切換 ASCII 模式等場景。
	`VmCandidatesBar` 和 `VmInput` 都依賴 `ImeState.AfterInput`。
	`VmIme` 也依賴 `ImeState.AfterInput`，並通過 `get_status` 讀 `is_composing`。
	`VmCandidate` 支援點擊（`Click`），點擊後發送對應數字鍵選中該候選詞上屏。
	候選詞列表最多顯示 16 個。
	所有 UI 發起的按鍵現在都走 `ImeState.InputSafely(...)`，避免原生處理阻塞 UI。
	由於 `AfterInput` 可能在後台線程觸發，`VmIme` / `VmCandidatesBar` / `VmInput` 更新綁定屬性時都需要切回 `Dispatcher.UIThread`。
	Y 鍵左滑切換 `ascii_mode`，通過 `RimeConnectionState.ToggleAsciiMode()` 在**後台線程**調用 `set_option`（防止約 350ms 的原生調用阻塞 UI 線程導致 ANR）。
	`ToggleAsciiMode` 內部使用 `Interlocked` 防止連點並發，直接計算新狀態並通過 `Dispatcher.UIThread.Post` 更新 `IsAsciiMode`。
	ASCII 模式下所有按鍵標籤顯示為小寫拉丁字母（如 Q→q、Σ→s），`VmKey` 監聽 `IsAsciiMode` 自動切換。
	工具欄第一個按鈕切換 `simplification` option：
	- `false` 時顯示 `漢`
	- `true` 時顯示 `汉`

	工具欄第二個按鈕切換剪貼板頁。
	目前先接平台當前 clipboard 內容，不含歷史。
	點擊條目後通過宿主接口 `IKeyboardHost.CommitText(...)` 直接上屏，然後退出剪貼板頁。
	剪貼板頁打開期間，工具欄剪貼板圖標使用 `UiCfg.MainColor`；退出後恢復白色。

	工具欄新增麥克風按鈕，切換到第一版語音輸入前端：
	- 語音頁替換 `ViewKeyBoard` 所在的 body 區，保留上方 preedit 與工具欄/候選欄結構
	- 頂部顯示當前 BCP 47 語言入口
	- 中央顯示白色圓形錄音按鈕及麥克風圖標
	- 點擊語言入口會顯示參考 CsNgan.Dict NormLang 頁製作的搜索列與語言列表
	- 當前列表使用靜態示例資料，只用於確認前端佈局，不代表最終語音引擎支持範圍
	- 錄音、搜索、語言選中、持久化及語音識別服務均尚未接入

	語音輸入下一版前端聲明以 `VmVoiceInput.cs` 和 `ViewVoiceInput.cs` 充當詳細設計文檔，核心約束為：
	- 識別文本框至少佔語音頁 body 可用高度的一半，識別結果不塞入候選欄
	- 流式結果區分“已穩定文本”和“可被下一包替換的臨時文本”，避免重複追加
	- 錄音及等待尾包期間文本框只讀，完成或失敗後允許人工修正
	- 只有用戶點擊“上屏”才提交 `DraftText`，識別完成本身不自動上屏
	- Windows 宿主尚未接系統輸入法 API，通過 `IKeyboardHost.CanCommitText=false` 禁用上屏並顯示說明
	- 語言列表由識別 Provider 公布的實際支持語言產生，不由 UI 硬編碼假設
	- 語言選擇覆蓋層必須提供顯式關閉入口，不能只依賴選中語言才能退出
	- 語音頁採用左右分欄：左側文本框佔三分之一，右側操作區佔三分之二，避免矮窗口裁切錄音與底部操作
	- 全局 Button 懸停只以 `UiCfg.MainColor` 高亮模板邊框，普通背景保持穩定，不再使用突兀的黑色懸停背景

	工具欄還有一個日誌按鈕，點擊後在鍵盤區與 `Rime` 日誌頁之間切換；
	日誌頁顯示期間，工具欄日誌圖標使用 `UiCfg.MainColor`；手動釘住或初始自動顯示都算作打開。
	切換範圍只覆蓋 `ViewKeyBoard` 所在的 body 區，預編輯欄、候選欄、工具欄本身不切。
	`VmIme` 開始 Rime 連接時先展示一次日誌頁，方便直接觀察引擎部署輸出。
	這裡拆成兩條狀態：
	- `Initial`：本次連接的首屏日誌頁；只在用戶未作任何 body 頁操作且連接成功時自動回到鍵盤
	- `Pinned`：用戶手動點擊工具欄日誌按鈕後打開/關閉
	用戶在部署過程中切到鍵盤、剪貼板、語音或手動日誌頁，都會清除 `Initial` 並保留其選擇；連接流程不會再覆蓋它。
	連接失敗且用戶未操作時，保留 `Initial` 日誌頁供查看錯誤輸出。

	工具欄新增了分體鍵盤按鈕：
	- 點擊後切換 `ImeUiState.IsSplitKeyboardEnabled`
	- 此狀態會持久化到可寫配置，hide/show 與下次啓動仍保留
	- 實際保存改爲後台執行，避免按下分體按鈕時同步 `Save()` 卡住 UI 線程
	- UI 層本身只負責暴露狀態；真正是否走 Android overlay 由宿主決定

	工具欄新增了系統按鍵映射按鈕：
	- 文案爲 `映`
	- 點擊後切換 `ImeUiState.IsSystemKeyRemappingEnabled`
	- 此狀態也會持久化到可寫配置，默認關閉
	- 它不改動 Rime 的收鍵，只影響“Rime 未處理按鍵 -> OS 發送前”的最後一跳
	- 當前首條規則是：`Backspace -> Alt_R`
]

#H[Rime 日誌頁][
	`RimeSetup` 現在把引擎通知回調包成 C# 事件 `OnLog` / `OnOptionChanged`。
	`Ngan.Ime.Core.Infra.Log.IRimeLogSource` 是 UI 看到的抽象；
	宿主層（如 Android / Windows）負責把 `Ngan.Ime.Rime.RimeLogStore.Inst` 註冊爲該介面。
	`RimeLogStore` 會先保存最近一段初始化日誌，再把新增日誌事件往外廣播。
	`RimeLogBuffer` 僅依賴 `IRimeLogSource`：
	- 構造時先回放歷史快照，保證即使 librime 在 UI 顯示前就已開始預熱，也能看到前面的初始化輸出
	- 運行中再訂閱 `OnLogAppended`，把後續日誌持續追加到頁面
	工具欄的日誌按鈕切換的是 `VmIme.IsRimeLogVisible`；
	其互斥關係和剪貼板頁相同：打開日誌頁時會關閉剪貼板頁，反之亦然。
]

#H[重建與釋放][
	Android IME 宿主現在改爲複用單例 `AvaloniaView`，hide/show 之間不再重建整棵 UI。
	但這不表示 UI 可以忽略釋放問題：
	一旦未來又因爲配置切換、宿主重建或調試手動重建視圖，舊樹上的事件訂閱仍然會洩漏。

	目前釋放鏈仍然保留：
	- `MainView.Dispose()` 轉交給 `ViewIme.Dispose()`
	- `ViewIme.Dispose()` 再向下釋放 `ViewKeyBoard`、`VmIme` 與各子模塊

	目前已顯式解除的訂閱包括：
	- `VmIme`：解除 `PropertyChanged` 與 `ImeState.AfterInput`
	- `VmCandidatesBar`：解除 `ImeState.AfterInput`
	- `VmInput`：解除 `ImeState.AfterInput`
	- `VmClipboard` / `VmToolBar` / `VmKey`：解除各自對宿主或全局狀態的訂閱
	- `ViewKeyBoard`：回收所有 `VmKey`，並解除對 `VmKeyBoard.PropertyChanged` 的監聽

	這一層文檔的約束是：
	- 以後若再新增掛在 `ImeState` / `RimeConnectionState` / 其他長生命週期單例上的事件訂閱，必須同步補 `Dispose`
	- 否則一旦宿主或調試流程再次重建 UI，舊樹仍會收到事件，最終表現爲越來越卡、甚至崩潰
]

#H[快捷鍵][
	A / Z / X / C / V / Y 鍵支援 Ctrl 組合鍵（長按或右滑）：
	- A → Ctrl+A（全選） → 底部 hint: ☑
	- Z → Ctrl+Z（撤銷） → 底部 hint: ↶
	- X → Ctrl+X（剪切） → 底部 hint: ✁
	- C → Ctrl+C（複製） → 底部 hint: ❐
	- V → Ctrl+V（粘貼） → 底部 hint: ▣
	- Y → Ctrl+Y（重做） → 底部 hint: ↷

	實現方式：`MkSendCtrlKey` 發送 Ctrl_L Down → Key Down → Key Up → Ctrl_L Up 四個按鍵事件。
	這四個事件會攜帶 `KeyBoardState`，把「當前 Ctrl 正按下」這個上下文沿輸入鏈傳下去。
	`KeyCfg.LongClickAction` 和 `KeyCfg.SwipeRightAction` 用於綁定這類自訂按鍵序列。
	Y 鍵保留頂部 `⇆` hint 提示左滑切換 ASCII 模式。
]

#H[Hint 佈局][
	每個按鍵有兩個 hint 位置：
	- `Hint`：右上角。用於數字鍵（提示長按/上滑的符號）和特殊功能鍵（如 ⇆、⇪ 等）
	- `HintBottom`：左下角。用於 Ctrl 組合鍵功能提示
	數字鍵 hint 顯示對應的符號字元（1→!, 2→@, 3→#, ...）
	`[` / `]` 的 hint 分別顯示 `{` / `}`；左右方向鍵的 hint 分別顯示 `⇤` / `⇥`
]

#H[$ 與 Shift 鎖定][
	第五排原 `Shift` 鍵改為 `$` 鍵：
	- 單擊：發送 `$`
	- 上滑：切換 `Shift` 鎖定

	`Shift` 鎖定採用持續鎖定式：
	- 上滑一次後，只切換鍵盤內部的鎖定狀態
	- 鎖定期間，後續普通按鍵事件都在 `KeyBoardState.AllDownKeys` 中攜帶 `Shift_L`
	- 再上滑一次時，從 `AllDownKeys` 語義上取消 `Shift_L`
	- UI 層不直接發額外的 `Shift Down/Up`，也不直接改寫字符；具體如何把 `Shift` 狀態解釋成大寫/符號或導航選區，由下游轉換器根據 `AllDownKeys` 統一處理
	- 鎖定開啟時，`$` 鍵背景色切換為 `UiCfg.MainColor`

	M 鍵保留單擊 `m`；
	滑動時發送固定字串後再補一次 `Space` 上屏：
	- 下滑：`$m,` + `Space`
	- 左滑：`$m,j` + `Space`
	- 右滑：`$m,i` + `Space`
]

#H[UiCfg][
	`Ngan.Ime.UI.UiCfg` 統一保存 UI 主色配置。
	當前 `MainColor` 與按鍵按下態背景色保持一致；`$` 鍵的 Shift 鎖定高亮也引用這個值。
	鍵盤整體字體會讀 `KeysCfg.Keyboard.Font`。
	`Keyboard.Font.Path` 必須指向單個字體文件；`Keyboard.Font.Family` 可選。
	若 `Family` 有值，直接按 `file-uri#family` 的方式交給 Avalonia 解析。
	若 `Family` 爲空，則從字體文件的 name table 中讀取 family name，再按同樣方式解析。
	若配置值爲空、字體文件不存在、family 無法解析，則回退到宿主原本默認字體。
]

#H[分體鍵盤視圖重用][
	Android 分體模式不是重新定義半套按鍵。
	現在做法是：
	- 每個 side overlay 內都放一個完整 `ViewKeyBoard`
	- 將其寬度拉成 overlay 寬度的兩倍
	- 左半窗左對齊、右半窗右對齊
	- 容器 `ClipToBounds=true`，因此只露出完整鍵盤的一半

	這樣可以直接保留既有：
	- 普通點擊
	- 長按重複
	- 上下左右滑動
	- 數字層切換
	- Shift 鎖定與 Ctrl 組合鍵

	避免再維護一份獨立的“分體鍵位表”。

	分體頂條則是另一條約束：
	- 仍然沿用「完整頂條拉寬後裁半邊」的思路，讓中間 50% 保持真正空出
	- 但 Android 宿主不再讓頂條 overlay 自由 `WrapContent`
	- 宿主會明確限制 split 頂條 window 高度爲「預編輯 + 工具欄/候選欄」的總高度
	- `ViewSplitTopHalf` 內部內容貼底顯示，避免工具欄與第一排鍵之間出現整塊黑縫
	- 宿主再補一個極小像素縫，避免頂條下沿與第一排鍵位上沿互相壓住
]

""")]
file class _{
}
