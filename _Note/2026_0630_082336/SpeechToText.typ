
-T[2026_0711_193254][
有甚么推荐的語音輸入引擎 API。是要在編程語言中能調用到的。
僅個人使用。識別要準確。最好能便宜點。最好能多支持一些語種。

][

]

-T[2026_0711_193125][
	先看skill。
	現在我想給Ngan.Ime這個項目弄一個語音輸入的功能。
	現在我在
][]

-T[2026_0711_194054][
	先看skill;
	看Ngan.Ime
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
[ViewIme.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/Ime/ViewIme.cs)
[ImeUiState.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/ImeUiState.cs)
[ViewToolBar.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/ToolBar/ViewToolBar.cs)

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
    - E:\_code\CsNgan.Dict\Doc\Spec\Frontend.typ
    - E:\_code\CsNgan.Dict\Ngan.Dict.Frontend\proj\Ngan.Dict.Ui\CodeTemplate\Sample\**
    - E:\_code\CsNgan.Dict\Ngan.Dict.Frontend\proj\Ngan.Dict.Ui\Infra\ViewModelBase.cs
    - E:\_code\CsNgan.Dict\Ngan.Dict.Frontend\proj\Ngan.Dict.Ui\Views\MainView.Decl.cs
````
你先給我按聲明與實現分離的步驟、先把聲明寫出來。

][

]


-Sesn[
-T[2026_0720_213852][
先看skill;
	看Ngan.Ime
現在我想給輸入法加個語音輸入功能。
要支持多語言。
參考CsNgan.Dict詞典的的語言選擇功能。
`E:\_code\CsNgan.Dict\Ngan.Dict.Frontend\proj\Ngan.Dict.Ui\Views\Dictionary\ViewDictionary.cs`
現在前端的聲明只寫了一小部分。
你繼續推進。

][
````md
已看完相關 skill、Ngan.Ime 文檔、現有語音聲明及 CsNgan.Dict 參考實現。現在有一個關鍵設計點不能自行猜測，需要你確認後我才能繼續寫聲明。

CsNgan.Dict 的語言選擇並不是單一控件：`ViewDictionary` 會打開 `ViewNormLangPage`，其語言資料來自 `ISvcNormLang` 和詞典資料庫，另外還有可配置的快捷語言 Tag。Ngan.Ime 目前沒有相應的語言模型、目錄或服務，不能直接照搬。

我建議本輪需求定為：

1. 語音頁只替換鍵盤 body；保留 preedit，頂欄仍按既有 toolbar/candidates 規則切換。
2. 分體鍵盤暫不適配。
3. 語言使用 BCP 47 語音地區標籤，例如 `zh-CN`、`zh-TW`、`en-US`、`ja-JP`，而不是較粗的 `zh`、`en`。
4. 語音頁增加「當前語言按鈕 + 快捷語言 Tag + 語言選擇頁」。
5. 語言選擇頁只展示語音引擎實際支持的語言；前端依賴抽象提供列表，不自行硬編碼完整列表。
6. 本輪只完成聲明，包括語言 DTO、語言目錄抽象、`VmVoiceInput`、`ViewVoiceInput`、語言選擇 View/VM 以及各入口的聲明；你審查後再寫實現。
7. 真正錄音、語音識別、權限申請、識別文字提交到輸入框，留到後續實現階段。

需要你裁定兩點：

- 語言 UI 是否需要像 CsNgan.Dict 一樣支持「可編輯的快捷語言 Tag」，還是只要一個當前語言按鈕，點擊後進完整列表？
- 本輪聲明是否要連未來的語音服務接口一起設計，例如開始錄音、停止錄音、識別結果事件和支持語言列表；還是嚴格只聲明純前端 UI？
````
]

-T[2026_0720_214825][
	先把前端畫出來。狀態啥的先不做。
	把CsNgan.Dict的NormLang搬過來。

][

]


-T[2026_0721_200752][
	剪貼版或日誌界面打開的時候、
	你也把那個圖標改成MainColor。像你現在在語音輸入視圖這裏做的一樣。

][

]

-T[2026_0721_203940][
	接下來去設計剩餘部分。
][
````md
目前不能直接進入具體架構設計，還有三個會根本改變方案的需求缺口需要你裁定。

我理解的剩餘需求是：

- 完成語言列表、搜索、選擇與持久化。
- 接入麥克風權限、錄音與語音識別。
- 顯示錄音中、識別中、錯誤等狀態。
- 將最終識別文字通過 `IKeyboardHost.CommitText` 上屏。
- 關閉語音頁、輸入法隱藏或宿主銷毀時，可靠停止錄音並釋放資源。
- 保持 `Ngan.Ime.UI -> Core 抽象 <- Android/Windows 實現` 的分層。

需要你確認：

1. 第一版使用哪種識別引擎？

   - Android 系統 `SpeechRecognizer`：通常免費、實現較快、多語言能力取決於手機上的語音服務，可能聯網且不同設備表現不一致。
   - 雲端 API：準確度和語言支持較可控，但涉及密鑰、費用、音頻上傳及網絡錯誤。
   - 同時設計可替換引擎抽象，第一個實現 Android `SpeechRecognizer`。我推薦這個。

2. 第一版平台範圍？

   - 先完整支持 Android；Windows 只提供不可用的 stub，後續再接引擎。我推薦這個。
   - Android 和 Windows 本輪一起支持。這需要再選 Windows 語音引擎。

3. 識別結果如何上屏？

   - 錄音期間顯示臨時結果，停止或識別完成後一次性上屏。我推薦這個，避免反覆修改目標輸入框。
   - 每次臨時結果都立即上屏。這需要維護替換/撤回邏輯，容易破壞已有文字。
   - 完成後先顯示結果，讓用戶再點「上屏」。

請回答這三點；確認後我會給出正式需求確認單，再提供多套架構方案供你選擇。
````
]

-T[2026_0721_204412][
	1.用http api聯網調別人的引擎。
	支持的語種要多。
	我會說中英日語、在學西班牙 意大利 法語、以後可能學德語。
	不要翻牆的。

	2.先做好windows的、我確認無誤 再把一些細節修好之後再弄到安卓上來。
	在安卓上編譯麻煩。如果是調http api的話不需要太多系統專屬適配層。

	3 把前端改一下吧。
	至少留一半空間給文本輸入框。識別上來的文字先展示在文本框裏。
	點上屏了纔能上屏。因爲windows沒接系統輸入法api所以上屏不了。
	直接直接生成候選又容納不下。

	最好要支持流式的。就是實時響應顯示文本。
	如果太複雜不好弄就算了。但是做的時候要預留未來擴展。

	你先去調研一下 哪些識別引擎好用。
	識別要準確。最好能便宜點。
	最好有能在線試用的。

][
````md
調研結論：第一版最值得試的是火山引擎「豆包大模型流式语音识别」，阿里云作为第二适配器预留。

**推荐顺序**

| 服务 | 你的语言覆盖 | 流式 | 价格/试用 | 判断 |
|---|---|---|---|---|
| 火山引擎 | 明确列出中、英、日、西、意、法、德，共25种 | WebSocket | 免费20小时/半年；活动价30小时66元 | 第一版首选 |
| 阿里云 NLS | 宣称50余种语言，包含英、日等 | 实时流式 | 公开页未清晰展示当前具体价格 | 第二适配器 |
| 腾讯云 ASR | 宣称15个小语种，但公开实时体验主要是中、英、粤 | WebSocket | 新用户5小时免费；30小时72元活动价 | 便宜，但不优先 |
| 讯飞语音听写 | 宣称65种语言 | 流式，短音频≤60秒；另有不限时长产品 | 免费试用较多；多个小语种正式价格各2000元/年 | 覆盖广，但个人使用偏贵 |

**为什么首选火山**

- 官方流式 API 明确支持：

  `zh-CN`、`en-US`、`ja-JP`、`es-MX`、`it-IT`、`fr-FR`、`de-DE`。

- 还支持自动语种识别，共25种语言。
- 官方直接将“游戏语音输入、手机输入法”列为应用场景。
- 免费额度是20小时、有效期半年，足够完成 Windows 原型与准确率测试。
- 产品页提供直接使用浏览器麦克风的在线体验。
- API 是 WebSocket。它从 HTTP 握手升级连接，适合持续上传音频并接收增量结果。

官方资料：

- [在线能力体验及价格](https://www.volcengine.com/product/asr)
- [流式识别 WebSocket API](https://www.volcengine.com/docs/6561/1354869)
- [免费额度与计费](https://www.volcengine.com/docs/6561/1359369)

**需要注意的火山限制**

火山的多语种参数目前只适用于文档所称的 `bigmodel_nostream`“流式输入模式”。它仍然通过 WebSocket 分包上传，但返回策略不像双向流式模式那样尽快逐字刷新：

- 双向流式：尽快返回字符，但外语 `language` 参数存在限制。
- 流式输入模式：累计约15秒或收到最后一包时返回；官方同时称平均5秒音频可在300–400ms内返回。

因此它能实现流式架构，但外语是否能达到你期望的“边说边明显更新”，必须实际试听验证。这是第一项验收重点。

**其他服务**

阿里云 NLS：

- 官方宣称实时流式识别支持50余种语言和20余种方言。
- 宣称通用准确率90%以上，部分模型可达99%。
- 支持中英自由说。
- 有[通义听悟在线体验](https://tingwu.aliyun.com/)。
- 但公开产品页没有把全部语言代码、模型限制和当前单价集中列清楚，需要注册后在控制台验证。

官方页：[阿里云实时语音识别](https://ai.aliyun.com/nls/trans)

腾讯云：

- 实时识别支持不限时长、“边说边出文字”。
- 新用户可领实时识别5小时，30小时活动资源包72元。
- 在线体验目前公开展示普通话、英语、粤语和普方英大模型。
- 虽然宣称15种小语种，但对你需要的日、西、意、法、德是否都能用于同一个实时接口，公开页证据不如火山明确。

官方页：[腾讯云语音识别](https://cloud.tencent.com/product/asr)

讯飞：

- 语言数量最多，提供网页在线体验和免费试用。
- 普通实时听写限制单段不超过60秒；另有不限时长的实时转写大模型。
- 小语种正式套餐较贵，页面显示日、西、意、法、德等分别为2000元/年，不适合个人先期使用。

官方页：[讯飞实时语音听写](https://www.xfyun.cn/services/voicedictation)

**建议的下一步**

先以火山引擎作为首个实现，但架构抽象为可替换 Provider：

```text
Windows 麦克风
    ↓ PCM 音频流
IVoiceRecognitionProvider
    ↓
VolcengineVoiceRecognitionProvider
    ↓ WebSocket
增量识别结果
    ↓
语音页 TextBox
    ↓ 用户点“上屏”
IKeyboardHost.CommitText
```

前端按你的要求调整为：

- 上半部分：多行识别文本框，至少占语音页一半高度。
- 下半部分：语言选择、圆形录音按钮、状态文字、“清空”和“上屏”按钮。
- 临时结果持续覆盖当前未确定片段，确定片段累加，避免重复文字。
- Windows 的 `IKeyboardHost.CommitText` 仍是 stub，所以第一阶段“上屏”按钮可禁用或显示“Windows 宿主尚未接入”；复制文本仍可作为调试出口。
- API Key 从本地配置读取，不能硬编码进源码。

在正式设计前，还需你确认一个选择：第一版是否确定采用火山引擎，并接受先实测它的外语增量返回延迟；如果效果不够实时，再增加阿里云适配器？
````
]

-T[2026_0721_210212][
	一會我去試。趁現在你先把前端改了。
	向我輸出匯報設計的同時也寫Decl.
	Decl上的註釋要詳細、讓Decl充當設計方案書和文檔的角色。
][
````md
前端设计与 Decl 已同步完成；Decl 本身作为详细设计文档，尚未修改实现。

**页面布局**

```text
┌──────────────────────────────┐
│ 多行识别文本框               │
│                              │  至少占 50%
│ 稳定文本 + 当前临时文本      │
├──────────────────────────────┤
│ [清空]   Windows无法上屏提示 │
│                     [上屏]   │
├──────────────────────────────┤
│       [zh-CN 中文（中国）]   │
│                              │
│          录音状态            │
│        （圆形录音按钮）       │
│          错误信息            │
└──────────────────────────────┘
```

主要控件、尺寸约束和交互职责写在 [ViewVoiceInput.cs](E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/VoiceInput/ViewVoiceInput.cs:10)。

**状态设计**

新增识别阶段：

- `Idle`：可选语言、编辑、清空、上屏。
- `Starting`：初始化录音设备及网络连接。
- `Recording`：持续录音和接收流式结果。
- `Finalizing`：停止录音，等待服务端最终结果。
- `Completed`：恢复人工编辑，允许上屏。
- `Failed`：保留已有文本和错误信息，允许修改或重试。

详见 [EVoiceInputPhase.cs](E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/VoiceInput/EVoiceInputPhase.cs:6)。

**流式文本**

Provider 不向 UI 发送需要盲目追加的字符串，而是发送完整快照：

- `FinalizedText`：已经稳定、不会再改写的文本。
- `InterimText`：下一包可能整体替换的临时尾句。
- `IsCompleted`：本轮识别是否最终完成。

VM 每次使用 `FinalizedText + InterimText` 重建草稿，避免产生重复句子。契约见 [VoiceRecognitionTextUpdate.cs](E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/VoiceInput/VoiceRecognitionTextUpdate.cs:6)。

**ViewModel 设计**

[VmVoiceInput.cs](E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/VoiceInput/VmVoiceInput.cs:12) 已声明：

- `DraftText`、稳定文本和临时文本。
- 状态、错误及宿主上屏能力。
- 编辑、清空、语言选择和上屏的可用条件。
- 开始、停止、取消识别。
- 接收流式更新、清空草稿和用户确认上屏。

录音与等待尾包期间文本框只读；完成或失败后允许人工修改。

**Windows 降级**

`IKeyboardHost` 新增 `CanCommitText`：

- Windows 原型返回 `false`，禁用上屏并显示明确提示。
- Android 将来有有效输入连接时返回 `true`。
- 识别结果不会因为宿主无法上屏而丢失。

见 [IKeyboardHost.cs](E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/IKeyboardHost.cs:3)。

**语言选择**

语言列表将来自当前识别 Provider 的真实支持语言，不再由 UI 假设：

- 搜索 BCP 47 代码、本地名称和英文名称。
- 搜索结果保持 Provider 原始排序。
- 增加显式关闭按钮。
- 无匹配项时显示提示。
- 选择与取消通过事件返回主页面。

详见 [VmVoiceInputLangSelector.cs](E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/VoiceInput/VmVoiceInputLangSelector.cs:11) 和 [ViewVoiceInputLangSelector.cs](E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.UI/Views/VoiceInput/ViewVoiceInputLangSelector.cs:10)。

当前没有运行编译：新增 partial 声明尚无对应实现，现有 `.Impl.cs` 也还未迁移到新布局；这是声明审查阶段的预期状态。
````
]

-T[][][]


]




