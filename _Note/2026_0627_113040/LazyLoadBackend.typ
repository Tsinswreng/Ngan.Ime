2026_0627_113049[
看Ngan.Ime
我在安卓上試驗的時候
當我把輸入法切換成Ngan.Ime
然後聚焦輸入框
鍵盤要過好一會纔會彈出來

預期應該是 UI不等待後端加載的
聚焦輸入框時應該先把鍵盤UI弄出來
不應讓用戶乾等

你看看是怎麼回事


]
#[
````md
問題基本查清了，延遲不是「後端明明異步了但 UI 還慢一點」，而是現在這條鏈路本身就在等後端。

關鍵路徑在這裡：

- [Ngan.ImeInputMethodService.cs](E:\_code\CsRime\Ngan.Ime\Ngan.Ime.Frontend\proj\Ngan.Ime.Android\Ngan.ImeInputMethodService.cs:55)
  `OnCreateInputView()` 會同步 `new MainView()`
- [MainView.cs](E:\_code\CsRime\Ngan.Ime\Ngan.Ime.Frontend\proj\Ngan.Ime.UI\Views\MainView.cs:10)
  `MainView` 構造時立刻 `new ViewIme()`
- [ViewIme.cs](E:\_code\CsRime\Ngan.Ime\Ngan.Ime.Frontend\proj\Ngan.Ime.UI\Views\Ime\ViewIme.cs:24)
  `ViewIme` 構造第一步就是 `Ctx = Di.DiOrMk<VmIme>()`
- [VmIme.cs](E:\_code\CsRime\Ngan.Ime\Ngan.Ime.Frontend\proj\Ngan.Ime.UI\Views\Ime\VmIme.cs:35)
  `VmIme` 構造需要 `ISvcIme`
- [Ngan.ImeInputMethodService.cs](E:\_code\CsRime\Ngan.Ime\Ngan.Ime.Frontend\proj\Ngan.Ime.Android\Ngan.ImeInputMethodService.cs:213)
  `ISvcIme` 的實現是 `SvcIme`，而它依賴 `RimeSetup.Inst`
- [RimeSetup.cs](E:\_code\CsRime\Ngan.Ime\Ngan.Ime.Rime\proj\Ngan.Ime.Rime\RimeSetup.cs:82)
  `RimeSetup` 構造函數裡同步做 `_setupRimeApi()`、`_setupRimeTraits()`、`_setupRimeSession()`
- [RimeSetup.cs](E:\_code\CsRime\Ngan.Ime\Ngan.Ime.Rime\proj\Ngan.Ime.Rime\RimeSetup.cs:179)
  `_setupRimeSession()` 裡還同步跑了 `initialize()`、`start_maintenance()`、`join_maintenance_thread()`、`create_session()`

所以現狀其實是：

聚焦輸入框
-> 系統要鍵盤 view
-> Ngan.Ime 開始同步建整棵 UI
-> 建 UI 過程中同步實例化 `SvcIme`
-> `SvcIme` 依賴同步初始化 `RimeSetup`
-> `RimeSetup` 同步等 librime maintenance/session
-> 這段做完前，鍵盤 UI 就出不來

你說的「UI 不應等待後端加載」目前確實沒有做到。

我這裡有個設計疑問，先停下來跟你確認後再改：

1. 保守修法，最小改動。
把 `RimeSetup` 改成真正 lazy async，`OnCreateInputView()` 只先出空鍵盤 UI / loading UI，`VmIme` 再背景 `ConnectAsy()`。
好處是改動集中，風險小。
代價是首次顯示時，候選/狀態會晚一點就緒。

2. 徹底修法，把「UI 初始化」和「IME 後端初始化」徹底解耦。
也就是 View/ViewModel 構造時完全不碰 `SvcIme` 的重依賴，改成先綁一個輕量 state，再由背景任務把 Rime service 掛上來。
好處是架構更乾淨。
代價是會動到更多 UI/DI 代碼。

3. 激進修法，服務創建時就預熱 Rime。
在 `InputMethodService.OnCreate()` 或更早時機先異步把 Rime session 建好，之後聚焦時只顯示現成 UI。
好處是首次聚焦最快。
代價是會增加常駐成本，而且如果你本來就不想讓後端提早起，這方向不對。

我目前推薦 `1`：先把首次鍵盤顯示從 Rime 初始化中解耦，先讓 UI 秒出，再慢慢補 backend 連接。你要我按這個方向直接改，還是你更想做 `2`/`3`？
````
]



2026_0627_114121[
先做3
]

2026_0627_114126[
然後1也做一下

就是你看工具欄上有個日誌按鈕
最初這個按鈕的設計是
在正常狀態下 點這個按鈕、鍵盤UI就被切換成滾動輸出的日誌UI
再次點擊 則切換回鍵盤UI。

當用戶聚焦輸入框、但未初始化完成時、鍵盤應先彈出日誌UI、等初始化完成後馬上自動切回鍵盤UI

這個功能目前沒有實現、按日誌按鈕只是能在界面間跳轉、但「但未初始化完成時、鍵盤應先彈出日誌UI、等初始化完成後馬上自動切回鍵盤UI」
包括正常狀態下直接點擊看日誌 都沒有。
]



2026_0627_152417[
	Ngan.Ime
	我發現一個問題、橫豎屏轉換後首次鍵盤彈出是有延遲的
	如果我一直保持在橫屏或豎屏模式下、我隱藏又彈出鍵盤 基本看不到有大延遲

	但是如果涉及橫豎屏切換 延遲就大了。比如我原先用的是豎屏、我切到橫屏、
	第一次聚焦輸入框時、鍵盤要等好幾秒纔能彈出。 後面在保持在橫屏狀態下就正常速度彈出。
	如果我再切回豎屏 那又是第一次彈出要等幾秒。
	看看怎麼回事

我剛剛沒開分體懸浮模式 剛剛只是在開發分體懸浮鍵盤 我試了一下而已
這個橫豎屏轉換的卡頓問題在分體懸浮鍵盤被開發出來之前就已經存在了 不開分體懸浮也有問題

可以自己用adb查詢相關日誌


]
