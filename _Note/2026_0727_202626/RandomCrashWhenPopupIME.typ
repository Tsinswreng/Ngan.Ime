-Sesn[
	-T[2026_0727_202651][
		先看skill和記憶。
		然後看Ngan.Ime。
		Ngan.Ime有個機制就是Rime引擎還沒加載好的時候、
		輸入法會先顯示日誌界面、一加載完就顯示鍵盤界面。
		現在一直有個bug、
		就是在安卓上 點擊輸入框、將要彈出輸入法的時候、
		看到輸入法彈出了日誌界面、過了幾秒纔轉到鍵盤界面、
		說明引擎崩潰了。
		這個問題找不到必然重現的規律、但他出現概率也不低。
		剛剛就出現了兩次。
		你看看怎麼回事。
		你可以用adb看日誌。注意手機上日誌緩存有限制、
		發現了相關日誌就儘早弄到電腦上先。
	][
````md
已定位。這不是 Rime 引擎崩潰，而是 Avalonia/Skia 的 Android 渲染 surface 崩潰。

- 20:23:56 和 20:24:22 兩次均為 `SIGSEGV`，崩潰線程是 Avalonia 渲染線程 `Thread-8`。
- native 棧固定落在 `libSkiaSharp.so`，入口包括 `sk_surface_draw`、`sk_canvas_clear`。
- 崩潰前 Android 正在銷毀 `SurfaceView[InputMethod]`，同時 [Ngan.ImeInputMethodService.cs](E:/_code/CsRime/Ngan.Ime/Ngan.Ime.Frontend/proj/Ngan.Ime.Android/Ngan.ImeInputMethodService.cs:190) 又執行 `RemoveView + SetInputView`，把同一個 `AvaloniaView` 立即 detach/attach。
- 渲染線程仍在使用舊 surface，主線程卻已令其失效，形成 use-after-free 類型的競態。
- 進程崩潰後 Android 重啟 IME；新進程重新初始化 Rime 約 3 秒，所以表面看起來像「日誌頁停了幾秒、Rime 崩潰後恢復」。

而且 [HideKeyboard.typ](E:/_code/CsRime/AvlnImeDemo/HideKeyboard.typ:96) 其實已經記錄過完全相同的結論：手動重掛能改善黑屏，但可能使 `libSkiaSharp.so` 在 `sk_surface_draw` 崩潰。Ngan.Ime 後來仍採用了這條已知有風險的方案。

日誌已保存：

- [完整 logcat](E:/_code/CsRime/_Note/Diagnostics/Ngan.ImeRimeCrash/logcat-20260727-current.txt)
- [崩潰上下文](E:/_code/CsRime/_Note/Diagnostics/Ngan.ImeRimeCrash/ngan.ime-crash-contexts.txt)
- [進程及崩潰時間線](E:/_code/CsRime/_Note/Diagnostics/Ngan.ImeRimeCrash/process-and-crash-lines.txt)
- [Rime 初始化時間線](E:/_code/CsRime/_Note/Diagnostics/Ngan.ImeRimeCrash/rime-timeline-lines.txt)

現在存在設計取捨，我按你的規則停下確認：要我繼續實施修復嗎？我建議先移除每次 `OnStartInputView` 的強制重掛，改成不復用失效 surface、必要時新建 `AvaloniaView`；代價是需要重新處理之前的黑屏與重建性能問題。
````
	]
]
