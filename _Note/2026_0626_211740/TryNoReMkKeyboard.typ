2026_0626_211858[
看
E:\_code\CsRime\AvlnImeDemo\AvlnImeDemo.Android\ImeInputMethodService.cs

````cs
File: e:\_code\CsRime\AvlnImeDemo\AvlnImeDemo.Android\ImeInputMethodService.cs
20: 	public bool ShouldRecreateImeView {
21: 		get=>false;
22: 		set{}
23: 	}
````

當前的寫法是 複用View
每次隱藏鍵盤再彈出時都用同一個View的實例


如果我改成
````cs
File: e:\_code\CsRime\AvlnImeDemo\AvlnImeDemo.Android\ImeInputMethodService.cs
20: 	public bool ShouldRecreateImeView {
21: 		get;
22: 		set;
23: 	}
````
就是不複用View
每次隱藏鍵盤再彈出時都創建不同的View


現在的問題是 如果複用View
那隱藏鍵盤之後再顯示時 鍵盤界面就都是黑的
但裏面的按鈕仍然是可點擊的

如果不複用View 每次都創建 就會有非常大的開銷 明顯卡頓、特別是接入了真正的輸入法引擎之後問題更多

我希望能複用View  怎麼解決這個問題?

先別看Ngan.Ime的代碼 先只看AvlnImeDemo的 把概念驗證跑通先


]
#[
````
先停一下，因爲我現在有一個關鍵不確定點，不能直接硬改。

我只看了 `AvlnImeDemo` 這邊，目前看到的是 [`ImeInputMethodService.cs`](E:\_code\CsRime\AvlnImeDemo\AvlnImeDemo.Android\ImeInputMethodService.cs) 在 `OnFinishInputView` 把 `ShouldRecreateImeView = true`，`OnStartInputView` 又可能 `SetInputView(OnCreateInputView())`。你現在描述的“複用同一個 `AvaloniaView` 後再次彈出變黑，但按鈕還能點”，很像不是 `ImeKeyboardView` 本身的問題，而是 `AvaloniaView` 背後的 Android render surface 在 IME window hide/show 後丟了，邏輯樹還活着，但畫面沒有重新綁回去。

我這邊有三個可能方案，但要你先拍板我往哪條線驗證：

1. 先做“真複用 View + 手動重掛載/重刷 surface”的 POC
意思是保留同一個 `AvaloniaView` 實例，但在 `OnStartInputView/OnFinishInputView` 補 Android view lifecycle 或強制重繪。
這是我認爲最接近你目標的路，但我現在還沒確認 `Avalonia.Android 12.0.4` 有沒有公開 API 可安全做這件事。

2. 做“半複用”
不重建 Avalonia 裏面的 `Content` / VM / 引擎，只重建外層 `AvaloniaView` 宿主。
這通常比“整個鍵盤全重建”便宜很多，也可能直接避開黑屏。
但它不算你要求的“複用同一個 View 實例”。

3. 直接深入看 `Avalonia.Android` 實現，必要時用反射/子類覆寫去補 lifecycle
這樣最可能找到根因，但代碼會更侵入，而且我目前不確定你是否接受為了 POC 去碰 framework 級 workaround。

我建議先選 `1`。
如果你同意，我下一步會只在 `AvlnImeDemo` 裏做最小實驗：先確認 `AvaloniaView` 在 hide/show 間是否發生 detach/attach，然後嘗試用最小補丁讓同一個實例恢復繪製。
````
]




2026_0626_213517[

按目前的版本

有一個新發現 他不是100%變黑

我大概發現他甚麼時候 黑 甚麼時候不黑了

第一次彈出輸入法鍵盤  再隱藏 再彈出、如果全程輸入框都在聚焦着、那他彈出就是黑的
如果隱藏後 先把輸入框失焦(比如我鎖屏再解鎖、或轉到其他頁面再回來) 就不黑

]


2026_0626_215156[
现在好很多了
又發現了問題
就是 如果我是點上方空白處來使鍵盤隱藏 再點輸入框彈出鍵盤的話
就沒發現問題

如果我點的是鍵盤上的hide鍵 有更高的概率使鍵盤崩壞 然後再也彈不出鍵盤 或是彈出了一個空殼鍵盤 上面只有標題AvlnImeDemo  沒有按鍵 (不是黑屏)

目前還找不到規律
你可以先看adb上有沒有錯誤日誌
]


2026_0626_215705[
現在還是
點了幾次hide之後 有一次 突然就崩了
然後過了一會再彈出鍵盤
但彈出的是一個空殼鍵盤 上面只有標題AvlnImeDemo  沒有按鍵 (不是黑屏)
]
#[
````
有，現在 `adb` 裏已經看到一個比“空殼鍵盤”更底層的錯誤了：

不是 Java 異常了，而是 native 崩潰：

- `Fatal signal 11 (SIGSEGV)`
- 線程：`Render Thread`
- 崩在：`libSkiaSharp.so`
- 時機：多次 `hideWindow` / 再 show 之後

關鍵日志點是這段：

- `InputMethodService: CALL: hideWindow`
- 緊接着 `Render Thread` 崩潰
- `libSkiaSharp.so (sk_surface_draw+28)`

這說明現在的主要問題不是事件重複訂閱了，根因更像是：

- 我們現在這個“複用同一個 `AvaloniaView`，並在 `OnStartInputView/OnWindowShown` 裏手動 `RemoveView + SetInputView + Invalidate` 重掛”的策略，
- 把 Avalonia/Skia 的底層 surface 狀態搞壞了，
- hide 之後 render thread 還在對已失效或半失效的 surface 畫圖，
- 於是直接在 native 層 segfault。

這也能解釋你看到的“空殼鍵盤”：
- IME service 進程崩過又被系統拉起
- 外面的宿主框架還在
- 但 Avalonia 內容樹/渲染面沒有完整恢復，所以只剩標題，按鍵區沒正常畫出來

我現在有兩條路，想讓你拍板：

1. 我先把我這次加的 `ReattachImeView()` 整套撤掉，回到最初“純複用、不手動重掛”
優點：先回到不會 native 崩的基線
缺點：黑屏問題大概率會回來

2. 我繼續做“複用”，但改方向
不再複用 `AvaloniaView` 這個 Android 宿主，只複用它裏面的 Avalonia `Content` / VM / 輸入法引擎狀態；每次 show 重建外層 `AvaloniaView`
優點：大概率能避開 Skia surface 復用崩潰，性能也比整棵 UI 全重建好很多
缺點：這不算“複用同一個 `View` 實例`”的最嚴格形式

我建議選 `2`。
因為從現在的 native crash 看，**同一個 `AvaloniaView` 實例跨 IME hide/show 硬複用** 這條路很可能就是不安全的。
````
]

2026_0626_220114[
有个事、就是同样是隱藏鍵盤、如果我是點屏幕其他位置來隱藏的  目前沒發現過出事、如果我點hide按鈕來隱藏 出事概率就很高 能不能考慮從這個地方入手 把隱藏按鈕的邏輯改成和我點其他地方一樣
]
#[
````
可以從這個方向入手，但我先停一下，因爲這裏有一個關鍵不確定點。

我看了 `adb`，現在最硬的錯誤其實是這個：

- 反覆 `hide/show` 後，`Render Thread` 在 `libSkiaSharp.so` 裏 `SIGSEGV`
- 也就是 native 渲染面炸了，不只是普通的 Java 邏輯問題

所以“點 `Hide` 更容易出事”很可能只是因爲它走的是 `RequestHideSelf(0)` 這條更激進的 IME 自關閉路徑，把我們現在這套複用/重掛邏輯更容易打崩。

但要把它改成“和點別處一樣”，我這裏有個不確定點：

- 真正的“點別處”本質上是宿主 app 的輸入框失焦，然後 app/系統去收鍵盤
- IME 自己其實拿不到一個官方的“模擬用戶點了別處使 editor 失焦”的 API
- 我能改的只是在 IME 端換一條更接近的隱藏路徑，而不是 100% 等價

現在有兩個可試方案，你選一個我就直接做：

1. 先把 `Hide` 按鈕改成不用 `RequestHideSelf(0)`
改走更溫和的路，比如 `HideWindow()`，只收 IME window，不主動請求整個輸入法 self-hide。
這最接近“先把窗口收起來”，我推薦先試這個。

2. 直接把 `Hide` 按鈕功能去掉，先只允許“點外部失焦”來隱藏
這是最接近你現在觀察到的穩定路徑，但功能上會退一步。

我推薦 `1`。
因爲它最符合你的想法，而且改動最小。
````
]


2026_0626_220646[
還是崩。你試試你hide按鈕發送一次 點擊返回鍵 的操作
]
#[
````
我已经把 `Hide` 按钮改成“发送一次返回键”了。

现在 [`ImeInputMethodService.cs`](E:\_code\CsRime\AvlnImeDemo\AvlnImeDemo.Android\ImeInputMethodService.cs) 里，`Hide` 不再走：
- `RequestHideSelf(0)`
- `HideWindow()`

而是走：
- `SendDownUpKeyEvents(Keycode.Back)`

这条路径更接近系统 Back 的收起方式。`dotnet build` 已通过。

你现在就只测一件事：
- 反复点键盘里的 `Hide`

看它相对之前是否明显稳定一些。
如果还是崩，我下一步就要回到更核心的问题：当前 `ReattachImeView()` 这套“复用同一个 `AvaloniaView` 并反复重挂”的策略本身大概率就在破坏 Skia surface，不只是隐藏路径的问题。
````
]

2026_0626_220712[
你現在這個發送的返回鍵的效果和我自己點擊屏幕下方導航欄的返回鍵 的效果 完全不一樣

我現在用的是傳統的導航欄
左邊是 多任務視圖 中間是home 右邊是返回←

當輸入法彈出是 右邊的返回鍵變↓

點擊之後是隱藏輸入法

你現在的效果是 點擊之後先看到 直接回到上一個視圖 再看到輸入法也被隱藏、推測輸入法被隱藏的原因是 回到了上個頁面、原先的輸入框失去焦點
]
#[
````md
這個觀察很關鍵，說明我剛才那個“發送返回鍵”方向是錯的。

它現在實際做的是：
- 把 `Back` 發給了當前 app / input connection
- 所以宿主頁面先返回上一層
- 輸入框隨之失焦
- 輸入法只是被動跟着收起

而你要的是：
- 走系統導航欄那個 `↓` 的語義
- 只收起 IME
- 不讓宿主頁面退回上一個視圖

這裏我先停一下，因爲接下來有兩條不同方向：

1. 我繼續只在 IME service 內試
目標是找一個更接近系統 `↓` 的 API，比如直接走 IME 自己的 back/dismiss 路徑，而不是往 input connection 發 `Back`。
這條我不確定 `InputMethodService` 對外暴露的 API 是否足夠乾淨。

2. 我把現在我加的“手動重掛 `AvaloniaView`”整套先撤掉，再單獨驗證 `RequestHideSelf/HideWindow` 本身到底會不會出事
因爲目前 native crash 很可能不是“Hide API 選錯了”本身，而是 `Hide` 之後配合 `ReattachImeView()` 才把 Skia surface 搞炸。

我建議先選 `2`。
理由是現在已經知道：
- `發送 Back` 不對
- `HideWindow()` 也沒明顯解決
- 真正高風險點很可能是 [`ImeInputMethodService.cs`](E:\_code\CsRime\AvlnImeDemo\AvlnImeDemo.Android\ImeInputMethodService.cs) 裏這套 `ReattachImeView()`

如果你同意，我下一步就先把我後來加的 `ReattachImeView()` 路徑整個撤掉，回到最小基線，再只測 `Hide`。
````
]

2026_0626_221146[
你要試就試吧 不行再聽我的
]

2026_0626_221444[
現在這版效果更差  無論是點頁面空白部分 還是點hide 都一樣高概率黑屏
]

2026_0626_222854[
原先的狀態是 複用View則聚焦隱藏再彈出必黑屏
每次重新創建View則聚焦隱藏再彈出不黑屏 但是有明顯卡頓 開銷大
今天達成了: 按空白處或點擊導航欄的返回鍵隱藏再彈出不黑屏。
只是鍵盤上的隱藏按鈕實現不了 試了很多方法都不行
你把今天的各種嘗試和結果詳細寫在文檔裏
寫在 E:\_code\CsRime\AvlnImeDemo\HindKeyboard.typ 裏。
這個項目本身是有概念驗證項目
可以給將來別人也想用avalonia開發安卓輸入法的 提供參考
你就把上面我說的寫清楚

目前用了甚麼方式 效果是甚麼 做不到甚麼/有甚麼問題
其他嘗試過的方法 效果是甚麼 做不到甚麼/有甚麼問題

]

2026_0626_222617[
(重新開會話)

先別看Ngan.Ime的代碼 先只看AvlnImeDemo的 把概念驗證跑通先
E:\_code\CsRime\AvlnImeDemo\AvlnImeDemo.Android\ImeInputMethodService.cs
看這個文件
目前彈出輸入法鍵盤之後再隱藏有兩種辦法
一種是 點擊界面空白處、輸入框失去焦點、鍵盤就隱藏了
第二種是 點擊導航欄的返回鍵

我現在用的是傳統的導航欄
左邊是 多任務視圖 中間是home 右邊是返回←

當輸入法彈出是 右邊的返回鍵變↓
這樣點一下返回鍵就隱藏輸入法了

用這兩種方法來隱藏輸入法都沒問題 再彈出還是能正常用的

就是 輸入法界面中也給了一個隱藏按鈕。這個隱藏按鈕就很難實現。

你嘗試一下 能不能 點擊這個隱藏按鈕後 發送一次操作 效果相當于我手動點擊返回鍵 來隱藏輸入法
]


2026_0626_224705[
我現在放棄 用輸入法按鍵來隱藏鍵盤了
現在看Ngan.Ime項目
仿照AvlnDemo的方式 不要多次重複創建View、複用同一個View、注意不要黑屏

然後原本退格鍵有個功能、就是退格鍵上滑能隱藏輸入法、你把這個功能用 `#if false`關掉先、寫清原因。

]




2026_0627_112603[
(新會話)

看
E:\_code\CsRime\AvlnImeDemo\AvlnImeDemo.Android\ImeInputMethodService.cs

然後你再看 Ngan.Ime的代碼
AvlnImeDemo中、當我彈出鍵盤、再隱藏鍵盤、再彈出、重複多次、目前都沒有觀察到有黑屏的問題

我之前讓Ngan.Ime參照AvlnImeDemo的代碼來改過了、但現在還是有概率發生黑屏問題

黑屏問題就是輸入法鍵盤隱藏後再彈出、界面都是黑的、但是裏面的按鈕仍然是可點擊的

你看看是怎麼回事 是不是哪裏沒改到位

]

