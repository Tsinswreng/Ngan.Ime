namespace Ngan.Ime.Doc.UI;

using Tsinswreng.CsCore;

[Doc($$"""
#H[用途][
	此文專門說明 Ngan.Ime 候選詞的完整鏈路與生命週期。
	範圍包括：
	- 候選詞如何從 Rime 引擎流到 UI
	- 候選詞如何從 UI 點擊後再回到輸入鏈
	- 候選欄在 hide/show 重建場景下如何初始化與釋放
]

#H[涉及角色][
	- `ImeState`：輸入鏈樞紐，負責 `InputSafely` / `Input` / `AfterInput`
	- `SvcIme`：`ISvcIme` 的 Rime 實現，負責把 `RimeContext` / `RimeStatus` 回填到 Core 狀態
	- `VmCandidatesBar`：候選欄 ViewModel，負責從 Rime 取候選列表
	- `VmCandidate`：單個候選詞的 ViewModel
	- `ViewCandidatesBar`：候選欄視圖，綁定 `CandVms`
	- `ViewCandidate`：單個候選詞視圖，處理 pointer release 後的實際點擊
	- Android `AvalimeInputMethodService`：訂閱 `ImeState.OnCommit`，把 commit 文字送進目標輸入框
]

#H[正向鏈路: 普通按鍵 -> 候選詞刷新][
	1. 用戶在 `ViewKeyBoard` 點擊按鍵。
	2. `ViewKeyBoard` 中的 `VmKey.Click` / 其他按鍵動作最終都調 `ImeState.InputSafely(...)`。
	3. `ImeState.InputSafely(...)` 把 `Input(...)` 丟到後台 `Task` 執行，避免阻塞 UI 線程。
	4. `ImeState.Input(...)` 先調 `ImeKeyProcessor.OnKeyEventsAsy(...)`，即把按鍵送進 Rime。
	5. `SvcIme` 自己也已訂閱 `ImeState.AfterInput`；它在後台線程內調 `get_status()` / `get_context()`，把最新 `IsComposing` / `Preedit` / `Candidates` 回填到 `ImeState`。
	6. `ImeState.Input(...)` 之後觸發同一輪 `AfterInput` 訂閱方。
	7. `VmCandidatesBar` 在構造函數中已訂閱 `ImeState.AfterInput`，因此每次輸入後都會讀取 `ImeState.Candidates` 這份最新快照。
	8. `VmCandidatesBar` 不直接再碰 Rime C API；它只在 UI 線程 `Post` 一次 `ApplyCandidates(...)`。
	9. `ApplyCandidates(...)` 會把當前候選資料原地覆寫進固定預建的 `VmCandidate` 池：
		- `Text` 來自 `Candidate.Text`
		- `Comment` 來自 `Candidate.Comment`
		- `Index` 是其在當前列表中的序號
		- `Foreground` 若等於 highlighted index 則用 `UiCfg.MainColor`，否則白色
		- `Click` 回調被綁成“發送對應數字鍵”
		- 超出本輪候選數量的槽位只被隱藏，不銷毀 VM / View
	10. `ViewCandidatesBar` 綁定了固定大小的 `CandVms`；候選變化時主要是切換可見性與覆寫文字，而不是首次打字時臨時 new 出整排候選視圖。
]

#H[UI 渲染鏈路: CandVms -> 候選項控件][
	`ViewCandidatesBar` 的 `ItemsControl` 對 `CandVms` 做 item template：
	- 每個 `VmCandidate` 對應一個 `ViewCandidate`
	- `ViewCandidate` 自己是無參構造，內部用本地 `Ctx = VmCandidate.Mk()` 佔位
	- 真正顯示時，再用 `DataContext` 綁成外部傳入的那個 `VmCandidate`

	`ViewCandidate` 渲染內容包括：
	- 上方 comment
	- 下方主文字
	- 高亮/普通前景色
	- 背景色與邊框

	`ViewCandidate` 通過 `Ctx.Bind(this, x=>x.MinWidth, x=>x.MinWidth)` 將自身的 `MinWidth` 綁定到 `VmCandidate.MinWidth`。
]

#H[候選詞最小寬度與間隔][
	候選詞間隔與鍵盤按鍵採用相同原則：不能有無法點擊的縫隙，分隔線僅是視覺作用。

	- `StackPanel` 的 `Spacing = 0`，候選之間無實際間距
	- 視覺分隔完全由 `ViewCandidate` 內 `Border` 的 `BorderThickness = 0.5` 實現
	- 兩個相鄰候選的邊框重疊（0.5 + 0.5 = 1px 視覺分隔），與鍵盤按鍵一致

	為使單字候選與鍵盤按鍵嚴格對齊，`ViewCandidatesBar` 會在自身已有寬度時，就提前把每個候選槽位的 `MinWidth` 算好並灌進預建 VM 池。

	公式：`MinWidth = 容器寬度 / 10`
	其中 10 對應鍵盤每行 10 列。間隔已由邊框提供，公式中不扣減 Spacing。

	計算後通過 `VmCandidate.MinWidth` 屬性（`INotifyPropertyChanged`）通知到 `ViewCandidate` 的綁定。
	這一步不再要求“先出現候選，再等首輪 item layout 後回填寬度”。

	Guard 條件：容器寬度變化 或 當前 VM 們的 MinWidth 尚未等於目標值時才更新，避免無效重複設定。
]

#H[反向鏈路: 點擊候選詞 -> 數字選詞 -> commit 上屏][
	1. 用戶點擊候選詞時，`ViewCandidate` 不在 `PointerPressed` 就立即上屏。
	2. 當前實現只在 `PointerReleased` 且 pointer 仍在控件內時，才認定為一次有效點擊。
	3. 有效點擊後執行 `VmCandidate.Click`。
	4. `VmCandidate.Click` 並不直接 commit 文本；它會回到 `VmCandidatesBar` 預先綁好的回調。
	5. 該回調按 `Index` 映射成數字鍵：
		- 0 -> `D1`
		- 1 -> `D2`
		- ...
		- 8 -> `D9`
		- 9 -> `D0`
	6. 然後再次調 `ImeState.InputSafely(...)`，發送這個數字鍵的 down/up 事件。
	7. Rime 收到數字鍵後，將當前候選項選中；若該次選中導致 commit，則 `ImeKeyProcessor` 會把 commit 文字放進 `RespOnKeyEvent.Commits`。
	8. `ImeState.Input(...)` 先遍歷 `Commits`，逐個觸發 `ImeState.OnCommit`。
	9. Android 端 `AvalimeInputMethodService` 訂閱 `OnCommit`，切回主線程後調 `CurrentInputConnection.CommitText(...)`。
	10. 之後 `ImeState.Input(...)` 才觸發 `AfterInput`，讓 Ngan.Ime 自己刷新 preedit / 候選詞。
	11. 因此從“點擊候選詞”到“文字真正上屏”，中間仍然經過完整的 `ImeState -> Rime -> OnCommit -> Android InputConnection` 鏈路，而不是 UI 直接插文字。
]

#H[候選欄顯隱條件][
	候選欄是否可見不由 `VmCandidatesBar` 自己決定，而由 `VmIme` 統一控制。

	`VmIme.ShowCandidates` 的當前條件是：
	- `(IsComposing || HasPreedit) && !IsClipboardVisible`

	也就是說：
	- Rime 處於 composing 或有 preedit 時，候選欄可顯示
	- 一旦切到剪貼板頁，候選欄會被隱藏
	- 是否顯示日誌頁不直接決定候選欄；日誌頁替換的是 body 區的鍵盤部分
]

#H[生命週期: 初始化][
	候選欄相關對象的初始化順序是：
	1. `MainView` 建 `ViewIme`
	2. `ViewIme` 在 `Render()` 內建 `ViewCandidatesBar`
	3. `ViewCandidatesBar` 無參構造時通過 `Di.DiOrMk<VmCandidatesBar>()` 解析自己的 `Ctx`
	4. DI 容器按構造函數注入 `ImeState` 與 `RimeConnectionState`
	5. `VmCandidatesBar` 構造函數內立即訂閱 `ImeState.AfterInput`

	因此只要這棵 UI 樹活著，候選欄就會持續跟隨每次輸入刷新。
]

#H[生命週期: 更新策略][
	候選欄不再在每次 `AfterInput` 都整體替換 `CandVms`，也不再在首顯時從空列表臨時增長出整排候選。
	現在策略是：
	- 啟動時先預建固定大小的 `VmCandidate` 池（當前為 16）
	- 每次輸入後原地覆寫前 N 個槽位的文字、註釋、高亮與點擊回調
	- 剩餘槽位僅標記為不可見，不銷毀對應 VM / View

	這種策略的特點是：
	- 狀態來源單一：一切以當前 Rime session 的最新候選列表為準
	- 候選項控件可被 Avalonia 復用，減少每次輸入時的 UI 重建與重新佈局
	- `MinWidth` 等已算好的顯示屬性可隨 VM 對象保留，避免每幀從 0 重新綁起
	- 當“無候選 -> 第一個候選出現”時，UI 不需要再同步做“批量創建 VM / View + 首輪寬度回填”這兩筆額外工作

	因此 `VmCandidate` 不再是“每次輸入必定整批失效”的短生命週期對象；
	在候選數量不變或接近時，通常是同一批 VM 被原地更新。

	這一點對 Android IME 特別重要：
	在 hide/show 之後，若候選欄仍每次整批替換 `CandVms`，
	實測會讓候選項視圖反覆重建，伴隨整排候選重新綁定與重新佈局，
	進而放大“上屏後立刻彈出新聯想候選”這個時刻的卡頓感。
	改成原地復用 VM 後，該類卡頓可明顯收斂。
]

#H[生命週期: 釋放][
	Android 端 hide/show 會重建整棵 `AvaloniaView`。
	因此候選欄鏈路上的釋放必須完整：

	- `AvalimeInputMethodService.OnCreateInputView()` 重建前先 `Dispose` 舊 `MainView`
	- `MainView.Dispose()` 轉給 `ViewIme.Dispose()`
	- `ViewIme.Dispose()` 轉給各子 View / Vm
	- `VmCandidatesBar.Dispose()` 解除 `ImeState.AfterInput -= _afterInputHandler`

	這一步很關鍵。
	若不解除 `AfterInput` 訂閱，舊候選欄即使已不在畫面上，仍會在每次輸入後收到事件並重跑 Rime 查詢，最終造成多重刷新、卡頓甚至崩潰。
]

#H[線程與所有權][
	候選詞刷新時有兩個線程邊界：
	- `ImeState.InputSafely(...)` 在後台線程跑
	- `VmCandidatesBar` 的 `AfterInput` 回調也因此先在後台線程上執行

	所以 `VmCandidatesBar` 的做法是：
	- 在後台線程只讀 `ImeState.Candidates` 這份已由 `SvcIme` 準備好的快照
	- 完成後只把最終 `ApplyCandidates(...)` 這一步切回 `Dispatcher.UIThread`

	這樣可避免：
	- 在 UI 線程上再跑一次 Rime C API
	- 直接跨線程改 Avalonia 綁定集合
]

#H[現有約束與注意點][
	- 候選詞點擊只認 `PointerReleased` 內部命中；長按不應直接上屏
	- 候選詞最多顯示 16 個
	- 10 個以後當前仍回退到數字鍵映射，實際可選範圍受 `IndexToKey` 限制
	- 候選詞本身不直接持有 `ImeState`；真正輸入回流入口仍集中在 `VmCandidatesBar`
	- View 層可以直接用全局 `Di` 解析自己的 `Ctx`；`VmCandidatesBar` / `VmCandidate` 本身不直接碰全局 `Di`
]

""")]
file class _{
}
