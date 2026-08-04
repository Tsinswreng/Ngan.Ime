我的電腦設置了不同的按鍵映射
具體看 E:\_code\_Tvs\Mvn\打字工具配置\打字工具配置.typ

現在我有個需求 就是用手機遠程控制電腦
這樣的話 我在手機輸入法上按的鍵 到電腦上之後就會過一層映射 導致不一致

比如我在手機輸入法上按退格鍵、到電腦上就變成右Alt了

你在Ngan.Ime工具欄上加個映射按鍵、圖標就一個「映」字

開啓時爲藍色 關閉時爲白色 默認不開啓

開啓後 對輸入法上的按鍵做映射 和電腦上的一樣。
現在先只做 輸入法的退格鍵 -> 右alt 的映射 別的先不做

看看有沒有問題?


2026_0627_192133[
映射層次放在 系統按鍵處理上。
就是說 本來 未被輸入法引擎處理的按鍵 會再交由OS處理
你就在這一層做映射。
這樣就不會影響輸入法引擎的按鍵處理。
不然你直接在根源做映射的話 按的按鍵進到輸入法引擎裏就變了、輸入法引擎就會收到錯誤的按鍵、就會出現問題。

注意架構 設計原則 設計模式等。雖然我現在只要你做退格鍵
但你也要按正規的架構給我實現這個功能
不能只偷懶搞if else
不然後續我要添加映射就很麻煩了
先說你的實現方案
]



2026_0627_200302[
不是能做成O(1)的嗎 源按鍵到目標按鍵弄個字典 你性能弄這麼差整得我卡卡的  同時注意性能和架構設計
]

2026_0627_200321[
說一下預期行爲
在非ascii中、按下的按鍵會進入引擎、引擎產生候選詞。
如果我不選中候選詞直接按回車、那用戶輸入的按鍵就會直接上屏

在ascii模式中、按下的按鍵也會進入引擎、但引擎一般不處理按鍵而是直接交給OS、然後直接上屏

當前 在手機的輸入框中 一切正常

但是當我使用UU遠程的時候
在非ascii模式下 先輸入字符 再打字 能上屏
但是切到ascii模式下 輸入字母就都不能上屏

符號鍵(不包括數字鍵) 不管是不是ascii模式 即使是引擎不處理的符號 就都能上屏

然後 上下左右四個方向鍵 和 Home End鍵 按了都沒用
退格鍵 按了也沒用 開了映射也不行

看看是怎麼回事

]

#[
```
看下來，問題基本就在 `AndroidOsKeyProcessor` 這一層，不在 Rime。

當前代碼的行為是：

- 普通字母/數字、退格、方向鍵、`Home/End` 這些，走 `InputConnection.sendKeyEvent(...)`
  - 代碼在 [AndroidOsKeyProcessor.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.Android/AndroidOsKeyProcessor.cs:24)
- 其他很多符號，走 `InputConnection.commitText(...)`
  - 同文件 [AndroidOsKeyProcessor.cs](/E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.Android/AndroidOsKeyProcessor.cs:32)

所以你現在觀察到的現象正好對上：

- 非 `ascii` 模式能上屏：因為那時很多內容是 Rime 處理後直接 `commit` 出來的，不依賴 OS 未處理鏈
- `ascii` 模式字母不能上屏：因為字母被轉去走 `sendKeyEvent(...)`，UU 遠程那個輸入框大概率不吃這類 key event
- 符號能上屏：因為符號多半走的是 `commitText(...)`
- 方向鍵、`Home/End`、退格都沒用：因為它們也都走 `sendKeyEvent(...)`
- 開了退格映射也沒用：因為只是把 `Backspace` 換成了 `Alt_R`，最後仍然還是走 `sendKeyEvent(...)`，而不是換成別的輸入通道

這和 Android 文檔語義也一致：

- `sendKeyEvent(...)` 是“把 key event 發給當前附着進程/焦點 view”，不是保證“像文本一樣插入”
  來源：[InputConnection.sendKeyEvent](https://developer.android.com/reference/android/view/inputmethod/InputConnection#sendKeyEvent(android.view.KeyEvent))
- `commitText(...)` 才是“把文本提交到文本框”
  來源：[InputConnection.commitText](https://developer.android.com/reference/android/view/inputmethod/InputConnection#commitText(java.lang.CharSequence,int))
- 刪除文本則應走 `deleteSurroundingText(...)`
  來源：[InputConnection.deleteSurroundingText](https://developer.android.com/reference/android/view/inputmethod/InputConnection#deleteSurroundingText(int,int))

結論：

- 你的本機普通輸入框“正常”，是因為普通編輯器通常同時能處理這兩條路
- UU 遠程的那個輸入框，很像只可靠支持 `commitText(...)` 這種“文本提交”接口
- 它對 `sendKeyEvent(...)` 這條路支持很差或根本不支持，所以字母/退格/方向鍵/`Home/End` 全失效
```
]
