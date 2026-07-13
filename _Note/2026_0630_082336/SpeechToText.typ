
-T[2026_0711_193254][
有甚么推荐的語音輸入引擎 API。是要在編程語言中能調用到的。
僅個人使用。識別要準確。最好能便宜點。最好能多支持一些語種。

][

]

-T[2026_0711_193125][
	先看skill。
	現在我想給Avalime這個項目弄一個語音輸入的功能。
	現在我在
][]

-T[2026_0711_194054][
	先看skill;
	看Avalime
現在我想給輸入法加個語音輸入功能
你先把前端做好
我的想法是 在頂工具欄上加個語音輸入入口
進入後把下方的鍵盤UI替換成語音輸入UI
UI的替換邏輯類似于剪貼板的邏輯
中間有個錄音按鈕
錄音按鈕弄成圓形
非錄音狀態是白色
正在錄音的狀態是UiCfg.MainColor
看看有甚麼問題
先想想方案

][
````md
先停在方案確認，因爲我看到兩個關鍵模糊點，不能直接假設。

**我理解的需求是**
在頂部工具欄加一個語音輸入入口；點進去後，把下方原本的鍵盤區換成語音輸入 UI。語音頁中間有一個圓形錄音按鈕，未錄音時白色，錄音中用 `UiCfg.MainColor`。你現在先要前端方案，不先碰完整後端語音能力。

**我看到的現狀**
現有切換骨架已經有了，核心點在：
[ViewIme.cs](/E:/_code/CsRime/Avalime/Avalime.Frontend/proj/Avalime.UI/Views/Ime/ViewIme.cs)
[ImeUiState.cs](/E:/_code/CsRime/Avalime/Avalime.Frontend/proj/Avalime.UI/ImeUiState.cs)
[ViewToolBar.cs](/E:/_code/CsRime/Avalime/Avalime.Frontend/proj/Avalime.UI/Views/ToolBar/ViewToolBar.cs)

現在 body 區已經是 `keyboard / clipboard / rimeLog` 疊層切換，所以加 `voiceInput` 是順路的。

**我建議的前端方案**
1. 在 `ImeUiState` 新增語音頁狀態，例如 `IsVoiceInputVisible`，互斥規則和 clipboard/rimeLog 一樣。
2. 新增 `VmVoiceInput + ViewVoiceInput`，只負責前端狀態：
   - `IsRecording`
   - 中間圓形按鈕
   - 一行狀態文案，例如“點擊開始語音輸入 / 正在錄音...”
   - 可再加一個佔位區，後面接識別結果。
3. 在 `ViewIme` 的 body overlay 裏加 `ViewVoiceInput`，讓它和 `ViewKeyBoard`、`ViewClipboard`、`ViewRimeLog` 同級切換。
4. 在 `ViewToolBar` 增加語音入口按鈕；頂欄入口我建議直接用 mic SVG，不用文字。
5. 錄音按鈕做成正圓：
   - idle: 白底
   - recording: `UiCfg.MainColor`
   - 背景仍沿用現在黑底風格，和現有工具欄/剪貼板頁一致。

**目前有兩個問題必須先定**
1. 你說“像剪貼板邏輯”，但你又說“替換下方鍵盤 UI”。這兩句在現有實作裏不是一回事。
   - 現在 clipboard 模式不只是換 body，還會把 `preedit / candidates` 也一起隱掉。
   - 我更推薦語音頁只替換 body，頂部的 preedit + toolbar/candidates 不動，這樣穩定，也更像“模式切換”。
   - 你要哪個？

2. 分體鍵盤模式要怎麼處理？
   - 現在 split mode 是 Android overlay，頂條和左右半鍵盤是另一套拼裝。
   - 語音頁如果進來，最省事的做法是“進語音頁時暫時退出分體模式”。
   - 另一個做法是 split mode 下也專門適配語音頁，但這會多一層宿主改動。
   - 我推薦先用前者。你確認一下。

**另外一個現實約束**
倉庫裏現在沒有任何語音/錄音服務抽象，只有像 `IClipboardService` 這種平台接口。所以如果你說“先把前端做好”，我建議這一輪先做純前端狀態切換，不先接真錄音；下一輪再補 `IVoiceInputService`。

如果你確認這兩點：
1. 語音頁是“只替換 body”還是“像 clipboard 那樣整頁切”
2. split mode 進語音頁時是否允許先自動退出分體

我再進下一步，把前端結構方案細化成具體改哪些類。
````
]


-T[2026_0712_110710][
	當前一試了一下、分體鍵盤模式下是不能進入剪貼板/日誌等視圖的。
	那你就先不用適配。
你說  - 現在 clipboard 模式不只是換 body，還會把 `preedit / candidates` 也一起隱掉。
   - 我更推薦語音頁只替換 body，頂部的 preedit + toolbar/candidates 不動，這樣穩定，也更像“模式切換”。
這本來不就是衝突的嗎?? 工具欄/候選欄那個位置、他是重合的、要麼就顯示工具欄、要麼就只顯示候選欄。
沒有候選的時候顯示工具欄、有候選的時候顯示候選欄、你怎麼還能不動candidate?  方便的話preedit可以不動。也可以去掉 看你怎麼方便。
前端代碼規範在
````md
    - E:\_code\CsNgaq\Doc\Spec\Frontend.typ
    - E:\_code\CsNgaq\Ngaq.Frontend\proj\Ngaq.Ui\CodeTemplate\Sample\**
    - E:\_code\CsNgaq\Ngaq.Frontend\proj\Ngaq.Ui\Infra\ViewModelBase.cs
    - E:\_code\CsNgaq\Ngaq.Frontend\proj\Ngaq.Ui\Views\MainView.Decl.cs
````
你先給我按聲明與實現分離的步驟、先把聲明寫出來。

][

]



-T[][][]

-T[][][]
