-T[2026_0712_205220][
	vscode中、typst的LSP插件(好像叫Tinymist)有個粘貼圖片的功能。
	但是、當我的路徑中 包含.開頭的文件夾時
	比如 `.Tsinswreng/Main.typ`、
	我編譯 Main.typ、按Ctrl+V來粘貼圖片、就不會奏效。
	怎麼解決這個問題?
][
````md
這多半不是你 `Typst` 文件本身的問題，而是 `Tinymist` 貼圖功能對 `.` 開頭目錄的兼容性有坑。
我剛看了你本機裝的擴展版本，是 `myriad-dreamin.tinymist-0.15.2`。它的貼圖邏輯會先跑 `tinymist.onPaste` 腳本，默認目標路徑是 `$root/assets`；如果它算出的路徑不在 workspace 內，就會直接失敗。官方文檔也說了貼圖是靠這個 hook 做的：[`Hook Scripts`](https://myriad-dreamin.github.io/tinymist/feature/script-hook.html)，Marketplace 頁面在這：[`Tinymist Typst`](https://marketplace.visualstudio.com/items?itemName=myriad-dreamin.tinymist)。

先這樣處理，按順序試：

1. 把 Tinymist 升到最新版，再重啟 VS Code。你現在是 `0.15.2`，先排除已修復的 bug。
2. 在 VS Code `settings.json` 裡明確設根目錄，不要讓它自己猜：
```json
{
  "tinymist.rootPath": "E:/你的項目根目錄",
  "tinymist.onPaste": "$root/assets"
}
```
改完後重啟 VS Code。
3. 如果還是不行，把 `Main.typ` 從 `.Tsinswreng/` 挪到非隱藏目錄。這是目前最穩的繞過方案。
4. 如果你就是必須放在 `.Tsinswreng/`，那就別用 Tinymist 的貼圖，改用專門的貼圖擴展，例如 `mushan.vscode-paste-image` 這類。

你還可以順手驗證一下是不是這個問題：
按一次 `Ctrl+V` 失敗後，看 VS Code 通知區有沒有類似 `cannot run custom paste handler` 的報錯。Tinymist 代碼裡確實會報這句。

如果你要，我下一步可以直接幫你：
1. 給你寫一份可直接貼的 `settings.json`；或者
2. 幫你定位 Tinymist 上游 issue，看看是不是已知 bug。
````
]

-T[2026_0712_205754][
E:\_code\CsRime\.Tsinswreng\a.typ  現在我打開的是 CsRime文件夾、我這個文件難道不在workspace內嗎?我也沒配在.gitignore裏啊
][

]

-T[2026_0712_210335][
正常按Ctrl+C時粘貼出來的圖片是這樣的
`#image("assets\2026-07-12-21-03-17.png")`

如果用Paste As命令就變成這樣了、不是我想要的效果。
`#image("image.png")`

試了一下、用Paste As 是在哪都能用的、
但Ctrl+C就在.開頭的文件夾裏就不行
][

]
