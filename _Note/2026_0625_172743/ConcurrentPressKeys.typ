在Ngan.Ime項目中
````
找到了！


06-25 17:16:11.714 E/libc++abi(32674): Pure virtual function called!
舊程序 PID 32674 崩了，是 C++ libc++abi 報的 "Pure virtual function called!"。這是在 RimeKeyProcessor start 之後立刻發生的 — 崩在 Rime 原生層。

但是這個崩在舊程序（改動前的程式碼）。讓我看新程序 2084 有沒有類似問題：

Bash Check new process 2084 for C++ crashes
IN
adb logcat -d -v time --pid=2084 2>&1 | grep -i "pure virtual\|SIGSEGV\|SIGABRT\|libc++abi\|tombstone\|DEBUG\|crash\|FATAL" | head -15

OUT
06-25 17:16:16.480 E/nswreng.Ngan.Ime( 2084): Not starting debugger since process cannot load the jdwp agent.
06-25 17:16:16.505 W/System.err( 2084): 	at android.os.perfdebug.PerfDebugMonitorImpl.monitorVersionControl(PerfDebugMonitorImpl.java:204)
06-25 17:16:16.505 W/System.err( 2084): 	at android.os.perfdebug.PerfDebugMonitorImpl.prepareMonitor(PerfDebugMonitorImpl.java:185)
06-25 17:16:19.502 W/libc    ( 2084): Access denied finding property "vendor.migl.debug"
新程序 2084 沒有 C++ 崩潰。舊程序 32674 崩在 Rime 原生層。看看崩之前最後做了什麼：

Bash Check PID 32674 context before crash
IN
adb logcat -d -v time --pid=32674 2>&1 | tail -40

OUT
06-25 17:16:10.950 D/Ngan.Ime (32674): [Perf] VmCandidatesBar.AfterInput done: 0ms, candidates: 10
06-25 17:16:10.950 D/Ngan.Ime (32674): [Perf] VmInput.AfterInput start: 0ms
06-25 17:16:10.950 D/Ngan.Ime (32674): [Perf] VmInput.AfterInput done: 0ms, preedit: ^
06-25 17:16:10.950 D/Ngan.Ime (32674): [Perf] VmCandidatesBar.AfterInput start: 0ms
06-25 17:16:10.950 D/Ngan.Ime (32674): [Perf] VmCandidatesBar.AfterInput done: 0ms, candidates: 10
06-25 17:16:10.950 D/Ngan.Ime (32674): [Perf] VmInput.AfterInput start: 0ms
06-25 17:16:10.950 D/Ngan.Ime (32674): [Perf] VmInput.AfterInput done: 0ms, preedit: ^
06-25 17:16:10.950 D/Ngan.Ime (32674): [Perf] VmCandidatesBar.AfterInput start: 0ms
06-25 17:16:10.950 D/Ngan.Ime (32674): [Perf] VmCandidatesBar.AfterInput done: 0ms, candidates: 10
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmInput.AfterInput start: 0ms
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmInput.AfterInput done: 0ms, preedit: ^
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmCandidatesBar.AfterInput start: 0ms
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmCandidatesBar.AfterInput done: 0ms, candidates: 10
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmInput.AfterInput start: 0ms
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmInput.AfterInput done: 0ms, preedit: ^
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmCandidatesBar.AfterInput start: 0ms
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmCandidatesBar.AfterInput done: 0ms, candidates: 10
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmInput.AfterInput start: 0ms
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmInput.AfterInput done: 0ms, preedit: ^
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmCandidatesBar.AfterInput start: 0ms
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] VmCandidatesBar.AfterInput done: 0ms, candidates: 10
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] ImeState.Input AfterInput done: 1ms
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] ImeState.Input OsKeyProcessor done: 0ms, unhandled: 1
06-25 17:16:10.951 D/Ngan.Ime (32674): [Perf] ImeState.Input total done: 12ms
06-25 17:16:10.980 I/Ngan.Ime (32674): [IME] CommitText: 提速
06-25 17:16:11.614 D/MIUIInput(32674): [MotionEvent] ViewRootImpl windowName 'InputMethod', { action=ACTION_DOWN, id[0]=0, pointerCount=1, eventTime=168855305, downTime=168855305, phoneEventTime=17:16:11.610 } moveCount:0
06-25 17:16:11.615 D/Ngan.Ime (32674): [Key] Pressed, hasLongPress=False, isRepeat=False
06-25 17:16:11.619 D/MIUIInput(32674): [MotionEvent] ViewRootImpl windowName 'InputMethod', { action=ACTION_POINTER_DOWN(1), id[0]=0, id[1]=1, pointerCount=2, eventTime=168855312, downTime=168855305, phoneEventTime=17:16:11.617 } moveCount:0
06-25 17:16:11.619 D/Ngan.Ime (32674): [Key] Pressed, hasLongPress=False, isRepeat=False
06-25 17:16:11.698 D/MIUIInput(32674): [MotionEvent] ViewRootImpl windowName 'InputMethod', { action=ACTION_POINTER_UP(0), id[0]=0, id[1]=1, pointerCount=2, eventTime=168855391, downTime=168855305, phoneEventTime=17:16:11.695 } moveCount:0
06-25 17:16:11.699 D/Ngan.Ime (32674): [Perf] OnPointerReleased→Click start: 0ms
06-25 17:16:11.699 D/Ngan.Ime (32674): [Perf] OnPointerReleased→Click done: 0ms
06-25 17:16:11.699 D/Ngan.Ime (32674): [Perf] ImeState.Input start: 0ms
06-25 17:16:11.699 D/Ngan.Ime (32674): [Perf] RimeKeyProcessor start: 0ms
06-25 17:16:11.699 D/MIUIInput(32674): [MotionEvent] ViewRootImpl windowName 'InputMethod', { action=ACTION_UP, id[0]=1, pointerCount=1, eventTime=168855391, downTime=168855305, phoneEventTime=17:16:11.696 } moveCount:0
06-25 17:16:11.700 D/Ngan.Ime (32674): [Perf] OnPointerReleased→Click start: 0ms
06-25 17:16:11.700 D/Ngan.Ime (32674): [Perf] OnPointerReleased→Click done: 0ms
06-25 17:16:11.700 D/Ngan.Ime (32674): [Perf] ImeState.Input start: 0ms
06-25 17:16:11.700 D/Ngan.Ime (32674): [Perf] RimeKeyProcessor start: 0ms
06-25 17:16:11.714 E/libc++abi(32674): Pure virtual function called!
結果
崩潰在舊程序（PID 32674），不是我們改的新程式碼。

崩潰點

17:16:11.699 [Perf] RimeKeyProcessor start: 0ms      ← 第一個按鍵
17:16:11.700 [Perf] RimeKeyProcessor start: 0ms      ← 第二個按鍵（僅 1ms 後）
17:16:11.714 E/libc++abi(32674): Pure virtual function called!  ← 崩了
根因
兩個 process_key 幾乎同時進了 Rime C++ 引擎（1ms 間隔），Rime session 不是線程安全的，並發調用導致 vtable 損壞，觸發 Pure virtual function called!。

這是一個既有的並發問題，跟我們的改動無關。InputSafely → Task.Run → Input 這條路徑沒有串行化保護，快速連續按鍵時就可能撞上。

新程序（PID 2084）
新程式碼跑在 PID 2084，到現在還在線，沒有任何崩潰紀錄。

不過這次改動確實可能間接增加了這個並發問題的觸發概率 — 因為之前 RimeConnectionState.SyncFromImeState() 用 Dispatcher.UIThread.Post 把狀態同步延遲到了 UI 線程，現在 ObservableObject.SetProperty 直接在 Rime 處理線程上引發 PropertyChanged，UI 綁定回調如果在同線程做重操作會加劇競爭。但這不是新 bug，只是暴露了既有的線程安全問題。

要我順便看一下 InputSafely / Input 的串行化問題嗎？
````

你看看是怎麼回事
不用再看日誌了 看看代碼哪裏有問題 然後再想想應該怎麼改

