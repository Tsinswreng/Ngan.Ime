#[橫屏鍵盤分屏]#[
	[2026_0627_115255,]

	看ngan.ime
	當前鍵盤高度固定取屏幕高度的一半、
	橫屏時彈出鍵盤也是這樣。


	現在我想爲橫屏鍵盤做適配。

	原本合體鍵盤是
```
1234567890
qwertuiopy
asdfghjkl;
zxcvbnm,.'
```
這樣的

工具欄中加一個分體鍵盤的按鈕
點擊按鈕後 鍵盤沿中線被分成兩半

在安卓端、
兩半鍵盤各靠手指兩邊方便按、高度佔屏幕80% 寬度每邊佔25%

在桌面端、可以就直接拆成兩個獨立的window 每邊一半
現在優先要實現安卓端的

先評審一下需求 能不能實現 難不難 要不要大改

然後想想怎麼做

候選欄應該怎麼處理

想幾種方案

]

#[

````md
**結論**

能做。安卓端優先做的話，不需要大改輸入鏈，也不用碰 `Ngan.Ime.Core` / `Ngan.Ime.Rime`。改動主要集中在安卓宿主高度策略、`ImeUiState`、工具欄、以及 [`ViewKeyBoard.cs`](/E:/_code/CsRime/ngan.ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/KeyBoard/ViewKeyBoard.cs:69) 的佈局重構。

難度我判成 `中等偏上`，但還不到“要重寫整個鍵盤”的程度。最大的原因不是功能本身，而是現在鍵盤是按“整塊單體硬編碼行佈局”寫的，分體後需要把它拆成可重組的左右兩半。現狀關鍵點如下：

- 安卓鍵盤高度現在確實是直接固定成半屏，見 [`Ngan.ImeInputMethodService.cs`](/E:/_code/CsRime/ngan.ime/Ngan.Ime.Frontend/proj/Ngan.Ime.Android/Ngan.ImeInputMethodService.cs:48) 和 [`Ngan.ImeInputMethodService.cs`](/E:/_code/CsRime/ngan.ime/Ngan.Ime.Frontend/proj/Ngan.Ime.Android/Ngan.ImeInputMethodService.cs:159)
- 工具欄目前只有幾個固定按鈕，直接加一個“分體/合體”按鈕即可，見 [`ViewToolBar.cs`](/E:/_code/CsRime/ngan.ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/ToolBar/ViewToolBar.cs:28)
- 候選欄現在默認假設“整條寬度對應 10 列鍵位”，`MinWidth = width / 10`，見 [`ViewCandidatesBar.cs`](/E:/_code/CsRime/ngan.ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/CandidatesBar/ViewCandidatesBar.cs:43)
- 鍵盤本體沒有方向/分體模式狀態，`ImeUiState` 可以擴這個狀態，見 [`ImeUiState.cs`](/E:/_code/CsRime/ngan.ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/ImeUiState.cs:34)

**怎麼做**

我建議先走一版“安卓橫屏分體，桌面暫不做雙窗”的方案：

1. 安卓宿主改高度策略。
橫屏時窗口高度從 `50%` 改成 `80%`，豎屏保持原樣。

2. 在 `ImeUiState` 加兩個狀態。
`IsSplitKeyboardEnabled` 和 `IsLandscape`，再算一個實際模式 `IsSplitKeyboardActive`。

3. 工具欄加分體按鈕。
按一下切 `合體/分體`。橫屏時可用，豎屏時可以隱藏或禁用。

4. 重構 `ViewKeyBoard`。
不要再直接 `MkRow1..MkRow6` 組成唯一大鍵盤，而是抽成“左半行/右半行”可重組。
前四排可以很自然按中線 5+5 切開。
第五、六排也能切，但要先確認你要不要嚴格按中線硬切。

5. 候選欄先不要跟着一起拆。
先保留在頂部一整條，這樣輸入鏈、選詞鏈、點擊回流都不用動。

**候選欄方案**

我想到 3 種，推薦第 1 種：

1. `推薦`
候選欄仍然保持頂部整條全寬。
優點是改動最小，不碰選詞鏈。
缺點是它不再和左右兩半鍵位嚴格對齊。

2. 候選欄改成“中間浮條”。
放在兩半鍵盤中間上方，寬度控制在中間區域或 50% 左右。
視覺更協調，但要改 `ViewIme` 和 `ViewCandidatesBar` 的寬度策略。

3. 候選欄也做分體。
左右各一條或鏡像一條。
這個最不推薦，因為候選是單一列表，拆成兩邊在交互上很彆扭，還容易引入重複點擊區和視覺混亂。

**我這邊現在有 3 個需要你拍板的點，否則我不該直接開做**

1. 橫屏時要不要自動進分體？
我的建議：`默認自動進分體，工具欄按鈕可切回合體`

2. 第五排要不要按中線硬切成這樣？
左：`Enter Tab ← Space`
右：`Space → $ Backspace`

3. 候選欄先保留整條全寬，接受“不再和鍵位 1:1 對齊”，可以嗎？
我的建議：`先接受，先把分體鍵盤做出來`

你如果同意，我建議先按這個組合實作：

- 安卓橫屏高度 `80%`
- 橫屏默認分體，工具欄可切換
- 候選欄先保持全寬不拆
- 桌面端雙窗先不做，只把狀態和結構預留好
````
]


2026_0627_130955[
補充一下需求
分體就是
兩半鍵盤各靠手指兩邊、高度佔屏幕80% 寬度每邊佔25%
輸入法記錄狀態
我手動點分體纔分
不點就不分
在豎屏狀態下也能點 反正點了你就走同樣的邏輯
剛剛漏強調一點東西了、就是 分體後、左右各佔屏幕50%寬度、中間剩50%是留出來顯示原來的內容的、不要遮擋
]

#[
````md
按你剛補的定義，需求變成這樣：

- 只有手動點按鈕才進分體，系統不自動切
- 豎屏也能點分體，邏輯和橫屏一致
- 分體後左右各是一半鍵盤
- 左右各貼屏幕兩側
- 每半邊實際占整屏 `25%` 寬
- 中間 `50%` 留空，目的是讓底下原 App 內容露出來
- 安卓端鍵盤高度改成 `80%` 屏高

前 5 條在 UI 結構上都能做，且不算特別難。真正有疑問的是最後這條“中間不要遮擋”。

我得先跟你確認這個“不要遮擋”你指的是哪一種：

1. 只是視覺上露出中間內容
也就是輸入法窗口仍然是整塊全寬，只是在中間不畫鍵盤，讓它看起來像空出一塊。

2. 真正不蓋住、不攔截中間區域
也就是中間那 50% 不是 IME 視窗的一部分，底下 App 真正可見、可點、可互動。

這兩種差很多。
我目前判斷：

- `方案 1` 能做，而且不需要大改安卓宿主，只要讓 IME 的根視圖仍然全寬，裡面放左鍵盤和右鍵盤，中間放空。
- `方案 2` 在標準 Android `InputMethodService` 下大概率做不到，因為 IME 視窗本身就是一整塊輸入法窗口。中間可以“不畫東西”，但通常不能真的把那塊窗口挖掉還讓下層 App 正常交互。

所以現在我需要你拍一個板：

你要的是“中間視覺留空”，還是“中間真正不被輸入法窗口佔用”？

如果你接受“先做視覺留空版”，那我建議方案是：

- 安卓宿主高度統一改成 `80%`
- `ImeUiState` 加 `IsSplitKeyboardEnabled`
- 工具欄加分體切換按鈕
- `ViewKeyBoard` 改成 `full-width root + left panel + center spacer + right panel`
- 寬度比先做成 `25 / 50 / 25`
- 候選欄先保持整條全寬，不跟着拆

如果你堅持“中間必須真不遮擋、底下還能點”，那我建議先別做，因為這很可能不是現有 IME 視窗模型能直接達成的，得先專門驗證安卓輸入法窗口能力，再決定要不要換實現路線。
````
]

2026_0627_131400[
我希望的中間不遮擋是
真正不蓋住、不攔截中間區域
也就是中間那 50% 不是 IME 視窗的一部分，底下 App 真正可見、可點、可互動。

你說做不到 那你能不能按浮動鍵盤的方式做
把鍵盤弄成兩個懸浮窗 一左一右

]



2026_0627_163244[
現在你實現這版有問題。
一是 不開分體正常用時 鍵盤少了最後一排、就是 `-=[]`開頭那一排按鍵
開了分體之後就不少

二是 分體開啓之後 你的工具欄和候選欄仍是在中間的 沒有拆成兩段分到兩邊
然後我說中間的50%部分要留出來  不是 IME 視窗的一部分，底下 App 真正可見、可點、可互動。
你這全是黑色遮蔽

然後就是開啓與關閉分體時特別慢 延遲特別大
你看看怎麼回事
]


2026_0627_165532[
現在這版是
正常豎屏不分體時仍然沒有最後一排按鍵、就是 `-=[]`開頭那一排按鍵
橫屏不分體就有最後一排按鍵

然後開啓分體或關閉分體時仍然非常卡頓
開啓分體之後 只有最後一排按鍵了 其他地方全都是黑的
中間空出來了沒問題
]



2026_0627_172520[

我現在要實現分體鍵盤模式

看ngan.ime
現在我想爲橫屏鍵盤做適配。

	原本合體鍵盤是
```
1234567890
qwertuiopy
asdfghjkl;
zxcvbnm,.'
```
這樣的

工具欄中加一個分體鍵盤的按鈕
點擊按鈕後 鍵盤沿中線被分成兩半

在安卓端、
兩半鍵盤各靠手指兩邊方便按、高度佔屏幕80% 寬度每邊佔25%

在桌面端、可以就直接拆成兩個獨立的window 每邊一半
現在優先要實現安卓端的


補充一下需求
分體就是
兩半鍵盤各靠手指兩邊、高度佔屏幕80% 寬度每邊佔25%
input欄 工具欄 候選欄也要被分開

輸入法記錄狀態
我手動點分體纔分
不點就不分
在豎屏狀態下也能點 反正點了你就走同樣的邏輯
剛剛漏強調一點東西了、就是 分體後、左右各佔屏幕50%寬度、中間剩50%是留出來顯示原來的內容的、不要遮擋

我希望的中間不遮擋是
真正不蓋住、不攔截中間區域
也就是中間那 50% 不是 IME 視窗的一部分，底下 App 真正可見、可點、可互動。

目前是按浮動鍵盤的方式做
把鍵盤弄成兩個懸浮窗 一左一右

現在問題是
一 開啓分體之後、只有工具欄和鍵盤最後一排按鍵顯示了、
鍵盤其他地方都是黑的 無法點擊
二是 分體模式 開啓和關閉都特別卡 延遲特別大

下面是上一任AI對話的交接文檔 僅供參考 要按我說的爲準


````md

1. 分體開關很卡
2. 分體後大面積發黑，出現只剩最後一排可見
3. 普通模式絕不能再被分體邏輯污染


- 不要再把普通模式改壞

**已確認的事**
- Android 宿主窗口普通模式目前仍是半屏，不是 80%
- 問題不太像“窗口高度被改成 80% 還殘留”，更像“普通模式內容 view 自己量測/裁剪錯了”
- 橫屏不分體有最後一排，豎屏不分體沒有，說明鍵位結構本身還在
- 分體黑屏/只剩最後一排，像是 split overlay 裏的測量或裁剪問題

**本輪剛做的修改**
改了：
- [Ngan.ImeInputMethodService.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.Android/Ngan.ImeInputMethodService.cs)

核心修改：
- 新增 `GetCurrentInputViewHeight()`
- 普通模式下，`AvaloniaView` 高度不再硬設為 `GetHalfScreenHeight()`，改為 `MatchParent`
- IME 窗口本身仍然維持半屏
- `UpdateInputWindowLayout()` 裏把“窗口高度”和“InputView 高度”分開：
  - 窗口高度：普通模式半屏；分體模式 1px placeholder
  - InputView 高度：普通模式 `MatchParent`；分體模式 placeholder 高度

我的判斷：
- 這是爲了避免 Android 實際分給 IME 內容區的高度小於 raw `DisplayMetrics.HeightPixels / 2`，而子 view 又硬塞半屏像素，導致豎屏最後一排被裁掉

**這一版是否已驗證**
沒有。只完成了編譯，還沒拿到用戶的實機反饋。

**編譯結果**
已通過：
```powershell
dotnet build E:\_code\CsRime\Ngan.Ime\Ngan.Ime.Frontend\proj\Ngan.Ime.Android\Ngan.Ime.Android.csproj
```

**和本問題直接相關的關鍵文件**
普通模式 / 宿主：
- [Ngan.ImeInputMethodService.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.Android/Ngan.ImeInputMethodService.cs)
- [ViewIme.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/Ime/ViewIme.cs)
- [ViewKeyBoard.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/KeyBoard/ViewKeyBoard.cs)
- [ViewKey.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/Key/ViewKey.cs)
- [UiCfg.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/UiCfg.cs)
- [ViewToolBar.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/ToolBar/ViewToolBar.cs)
- [ViewCandidatesBar.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/CandidatesBar/ViewCandidatesBar.cs)

分體相關：
- [SplitKeyboardOverlayManager.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.Android/SplitKeyboardOverlayManager.cs)
- [ViewSplitKeyboardHalf.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/SplitKeyboard/ViewSplitKeyboardHalf.cs)
- [ViewSplitTopOverlay.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/SplitKeyboard/ViewSplitTopOverlay.cs)
- [ViewSplitTopHalf.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/SplitKeyboard/ViewSplitTopHalf.cs)
- [ImeUiState.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/ImeUiState.cs)

**文檔**
已讀的對應文檔：
- [Catalog.cs](/E:/_code/CsRime/ngan.ime/Ngan.Ime.Doc/proj/Ngan.Ime.Doc/Catalog.cs)
- [AndroidImeHost.cs](/E:/_code/CsRime/ngan.ime/Ngan.Ime.Doc/proj/Ngan.Ime.Doc/Android/AndroidImeHost.cs)
- [KeyboardUi.cs](/E:/_code/CsRime/ngan.ime/Ngan.Ime.Doc/proj/Ngan.Ime.Doc/UI/KeyboardUi.cs)

注意：
- 文檔裏已經記了分體 overlay 方案，但當前運行表現仍不穩，後續如果實現變了需要同步修正文檔

**git / 工作樹狀態注意**
repo 路徑大小寫有混用：
- 實際用過的路徑有 `E:\_code\CsRime\Ngan.Ime` 和 `E:\_code\CsRime\ngan.ime`
- `git` 需要帶 safe.directory，否則會報 dubious ownership

可用命令樣式：
```powershell
git -C E:\_code\CsRime\Ngan.Ime -c safe.directory=E:/_code/CsRime/Ngan.Ime status --short
```

當時看到的變更狀態大致有：
- `Ngan.Ime.Frontend/proj/Ngan.Ime.Android/Ngan.ImeInputMethodService.cs`
- `Ngan.Ime.Frontend/proj/Ngan.Ime.Android/SplitKeyboardOverlayManager.cs`
- `Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/SplitKeyboard/ViewSplitKeyboardHalf.cs`
- `Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/SplitKeyboard/ViewSplitTopHalf.cs`
- 以及一個筆記文件 `.Note/.../SplitKeyboard.typ`

不要誤回退用戶已有更改。

**下一會話建議第一步**
先讓用戶驗證我剛改的兩個場景：
1. 豎屏不分體最後一排是否恢復
2. 橫屏不分體是否仍正常

**如果豎屏普通模式還沒好**
優先繼續查這三件事：
1. `ViewIme` 的 `preedit + topbar + body` 總高度是否超過普通模式窗口
2. `ViewKeyBoard` / `ViewKey` 是否存在最小高度把最後一排擠掉
3. Android IME 實際內容區高度是否比窗口高度更小，需要進一步打印 layout/bounds 日誌

**如果普通模式好了**
再單獨做分體，順序建議：
1. 修分體黑屏 / 只剩最後一排
2. 再查分體切換卡頓
3. 最後處理工具欄和候選欄是否拆成左右兩邊

**對分體黑屏的當前懷疑**
- `ViewSplitKeyboardHalf` 的裁剪/寬高同步有問題
- overlay 裏完整 `ViewKeyBoard` 被裁得只剩底部
- 分體 top overlay 和 side overlay 的高度同步可能不一致

**對分體卡頓的當前懷疑**
- 開關分體時仍有重掛 / detach / attach 成本
- overlay show/hide 和 Avalonia surface 重建成本過高
- 早前版本在 `IsSplitKeyboardEnabled` 切換時有 `ResetInputViewInstance()`，這很容易放大延遲；目前那段已被去掉，不要輕易加回去

**用戶溝通風格提醒**
- 用戶現在很不滿，回覆要直接、技術化
- 沒有把握不要說“已修好”
- 一旦對設計或路徑有疑問，要停下來問，不要硬推進
````
]


2026_0627_175715[

	現在截圖是這樣
	基本都能顯示出來了
	現在還有問題
	就是鍵盤第一排 qwert
	和頂欄工具欄之間有一斷甚麼也沒有的空黑

	第二個問題是切換仍然非常卡頓
	看看是做麼回事
]


2026_0627_181606[
現在截圖是這樣
	就是鍵盤第一排 qwert
	和頂欄工具欄之間的空黑沒有了
	但是現在變成 頂欄把鍵盤第一排qwerty的上面一小部分覆蓋住了

	還有就是切換卡頓仍然存在。 當前
	第一次安裝程序之後、全非分體切換到分體 感覺是不太卡的，後面每次切換都非常卡
]




2026_0627_184811[
「但是現在變成 頂欄把鍵盤第一排qwerty的上面一小部分覆蓋住了」
這個問題沒解決。
卡頓解決了
還有個新的問題就是
現在變成我點擊工具欄的任何地方 就會觸發分體與非分體的切換
看看是怎麼回事

再補充一點、非分體模式下點擊鍵盤上任意按鍵就進入分體模式
不只是點擊工具欄會這樣。


]

2026_0627_185651[
現在又發現新的問題了
就是輸入法啓動後 直接用是沒有問題的。
當我一次進入分體模式時、按按鍵區也是沒有問題的。
但按工具欄區就有問題了 就會在分體模式間切換了
然後切換回普通模式之後 非分體模式下點擊鍵盤上任意按鍵就進入分體模式
不只是點擊工具欄會這樣。
就是說 只要進過分體模式 那就是只能在分體模式下打字 一旦退出分體模式、那麼按任何鍵都會進入分體模式
]




2026_0627_194709[
當前問題是基本沒有了

現在還有兩個問題

看截圖

第一個問題是

我之前明確要求 工具欄上的按鈕要和下方按鍵對齊
就是 下方一排按鍵是 1234567890
工具欄第一個按鈕就要對齊1
第二個按鈕就要對齊2

之前一直都是能對齊的 自從新裝的這版之後就是對不齊了的

第二個問題 我之前說了很多次了 你沒一次是改好的 就是工具欄/候選欄把按鍵第一排(橫排數字鍵)的上方遮住了一小部分 的問題

看看怎這回事
]


2026_0627_201123[
我看到之前的問題又複發了 也可能是你沒改好 我驗證的時候沒觸發而已
```
但按工具欄區就有問題了 就會在分體模式間切換了
然後切換回普通模式之後 非分體模式下點擊鍵盤上任意按鍵就進入分體模式
不只是點擊工具欄會這樣。
就是說 只要進過分體模式 那就是只能在分體模式下打字 一旦退出分體模式、那麼按任何鍵都會進入分體模式
```


現在還發現了一個新問題
ngan.ime是有一個強行重口鍵盤的常駐通知的
當出現上面的 「只能在分體模式下打字」的問題時，我點了一下常駐通知 試圖重啓鍵盤
鍵盤確實重啓了、也能在非分體模式下打字了
但是 ascii模式和非ascii模式下的鍵盤分別變成了這樣子
#image("assets/2026-06-27-20-18-06.png")
#image("assets/2026-06-27-20-18-12.png")
就是 很多按鍵的圖標不顯示了
但還能正常點擊
你看看是怎麼回事

]
