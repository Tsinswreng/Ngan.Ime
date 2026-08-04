2026_0613_220817[
先看代碼規範
````
    - E:\_code\CsNgan.Dict\.agents\skills\write-csharp-code\SKILL.md
    - E:\_code\CsNgan.Dict\Doc\Spec\Common.typ
    - E:\_code\CsNgan.Dict\Doc\Spec\Frontend.typ
    - E:\_code\CsNgan.Dict\Ngan.Dict.Frontend\proj\Ngan.Dict.Ui\CodeTemplate\Sample\**
    - E:\_code\CsNgan.Dict\Ngan.Dict.Frontend\proj\Ngan.Dict.Ui\Infra\ViewModelBase.cs
    - E:\_code\CsNgan.Dict\Ngan.Dict.Frontend\proj\Ngan.Dict.Ui\Views\MainView.Decl.cs
    - E:\_code\CsNgan.Dict\Doc\Spec\IView.typ
````

````yaml
File: d:\Program Files\Rime\User_Data\TswG-3.3.10.trime.yaml
1100:
1101: #键盘布局
1102: preset_keyboards:
1103:   default:
1104:     author: "Tsinswreng"
1105:     name: TswG
1106:     ascii_mode: 0
1107:     width: *TswG_width
1108:     height: 30

````

看這個文件
這個文件非常大
只挑你要看的部分來看 其他地方別看

重點看 TswG 的鍵盤佈局。

然後你把

E:\_code\CsRime\Ngan.Ime\proj\Ngan.Ime.UI\Views\KeyBoard\ViewKeyBoard.cs

的佈局和功能也改成和他的一樣。


]


2026_0618_185004[
键盘长按弄了吗?? 自己看 yaml裏面long click的部分

]


2026_0618_190003[
ascii_mode 也弄一下。
Y 鍵 向左滑動是切換ascii模式與非ascii模式

ascii 模式下 鍵盤所有按鍵都顯示爲小寫拉丁字母
比如 A ∑ 就 顯示成a s
ascii模式是引擎的概念 不是純前端的概念

]


2026_0618_193031[
我还没打字呢 我就连接rime之后 滑了几次y键 就闪退了 看看甚么原因。还有 你只用把字母改成小写字母 别的甚么都不用动 我看你连方向键 回车 退格 啥的显示都变了 不要这样
]


2026_0618_222731[
看Ngan.Ime这个项目。
滑動Y鍵是在ascii模式間切換。
安卓端
我还没打字呢 我就连接rime之后 滑了几次y键 就闪退了 看看甚么原因。
]


2026_0618_224933[
A
Z
X
C
Y
這幾個按鍵 長按或向右滑 效果是相當於 ctrl+該鍵
如 長按A鍵 或 A鍵右滑 就是全選
這幾個功能你也沒加上。
這幾個鍵的hint也沒有。
````yaml
File: d:\Program Files\Rime\User_Data\TswG-3.3.10.trime.yaml
4171:   select_all: {label: '☑', functional: false, send: Control+a}
4172:   cut: {label: '✁', functional: false, send: Control+x}
4173:   copy: {label: '❐', functional: false, send: Control+c}
4174:   paste: {label: '▣', functional: false, send: Control+v}
4175:   paste_clip: {label: 粘贴, send: function, command: clipboard}
4176:   paste_text: {label: 貼上文本, send: Control+Shift+Alt+v} #>=Android 6.0
4177:   share_text: {label: 分享文本, send: Control+Alt+s} #>=Android 6.0
4178:   redo: {label: '↷', functional: false, send: Control+Shift+z} #>=Android 6.0
4179:   undo: {label: '↶', functional: false, send: Control+z} #>=Android 6.0

````
上面的label就是他的hint
無關的別看。
上面這些功能鍵的hint放下面。


第一排數字鍵的hint都沒顯示。
把第一排數字鍵的hint顯示在按鍵的右上角




]


2026_0618_234234[
	當前 文本編輯那幾個功能鍵 全都沒用。
	比如我長按A鍵 或右滑A鍵 他應該是全選 但是實際效果卻和單擊一樣。

]


2026_0619_184514[
`[`和`]`的hint沒顯示
左右箭頭的hint也沒顯示
hint是這兩個 ⇤⇥

shift 鍵實現不對。
當前效果我看着像 切換ascii mode
目標效果應該是 鎖定左 shift
然後M鍵 下滑 相當于 `$m,` +空格
右滑 相當於 `$m,i`+ 空格
左滑 相當於 `$m,j`+ 空格

再把shift鍵改成`$`鍵、上滑纔是鎖成shift
hint也改成shift的標誌、按鍵標籤改成`$`
]


2026_0619_185816[
`$` 鍵上滑鎖定shift未生效。沒有任何變化/效果。 你把他修好。另外、
參考 E:\_code\CsNgan.Dict\Ngan.Dict.Frontend\proj\Ngan.Dict.Ui\UiCfg.cs
你也弄個UiCfg。

其中的MainColor 定義成 和 按鍵被按下時的背景色 一樣。
然後 把 按鍵被按下時的背景色 改成從MainColor引用

當shift鎖定開啓時、把`$`鍵 的背景色改成MainColor。
]


2026_0619_192725[
把按鍵字號改大點
候選字的字號和按鍵字號一樣大
當前選中的候選字要高亮 背景色改成MainColor
]


2026_0619_195920[
右下角數字鍵盤 那個123 字體沒統一 明顯比別的大。也可能是我的錯覺
你檢查一下
然後進入數字鍵盤之後 原先地方不要寫「返回」。寫成「qwe」
]


2026_0619_200240[
123 qwe 這個鍵的字號弄小點。
然後hint的字號整體弄大點。
所有字號都要基于 BaseFontSize 改
比如 BaseFontSize*0.9 這種。
再加個功能、退格鍵上滑後隱藏鍵盤
]


