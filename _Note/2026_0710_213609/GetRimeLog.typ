-T[2026_0710_213626][
	先看skill。
	你研究一下怎麼把Rime引擎的日志 讓Avalime拿到手
][]


-T[2026_0710_215844][
	那你去翻weasel的代码 看他怎么输出日志的  E:\_code\_clone\rime\weasel
][
````md
我已经把 weasel 这边翻到了，结论比较明确。

weasel 分两套日志：

1. weasel 自己的日志
- `include/logging.h` 直接接的是 `glog`
- `WeaselLogPath()` 在 [WeaselUtility.h](/E:/_code/_clone/rime/weasel/include/WeaselUtility.h) 里被硬编码到 `%TEMP%\rime.weasel`
- `RimeWithWeasel.cpp`、`Configurator.cpp` 里的 `LOG(INFO)` 这些，走的是 weasel 自己的 glog
- `CHANGELOG.md` 也明确写过“日誌系統改用glog，輸出到 `%TEMP%\rime.weasel.*`”

2. librime 的日志
- weasel 在 [RimeWithWeasel.cpp](/E:/_code/_clone/rime/weasel/RimeWithWeasel/RimeWithWeasel.cpp:141) 和 [Configurator.cpp](/E:/_code/_clone/rime/weasel/WeaselDeployer/Configurator.cpp:46) 里，确实把 `RimeTraits.log_dir` 设成了 `WeaselLogPath()`
- 也就是说，weasel 对 librime 做的事情本质上只是：
  - 组 `RimeTraits`
  - 填 `app_name = "rime.weasel"`
  - 填 `log_dir = %TEMP%\rime.weasel`
  - 然后 `rime_api->setup(&weasel_traits)`

关键点在这里：
- 我没有在 weasel 侧看到“把 librime 原生日志回调出来”的代码
- weasel 主要拿的是 `set_notification_handler` 那条通知流，不是 glog 实时流
- 所以 weasel 也不是“直接从 librime 拿到一条条引擎日志再显示”
- 它更像是：
  - weasel 自己的日志写 `%TEMP%\rime.weasel`
  - librime 如果遵守 `traits.log_dir`，也应该往那边写
  - 宿主并没有专门做“日志桥接”

另外我想继续追 librime 内部到底怎么消费 `traits.log_dir`，但 `weasel/librime` 是个失效 junction，目标 `E:\_code\rime\librime` 在你这台机器上并不存在，所以这条源码链现在断了。

所以目前能成立的判断是：
- `log_dir` 在 weasel 里只是“传给 librime 的配置”，不是 weasel 自己额外处理的日志桥
- Avalime 现在如果想“稳定拿到 Rime 引擎日志”，不能照着 weasel 去找什么现成回调接口
- 真正可控的方向还是两条：
  - 继续拿 `set_notification_handler` 的通知流
  - 或者自己实现“读日志文件/别的输出通道，再喂给 `RimeLogStore`”

现在有个疑点必须跟你确认：
- 你要我继续去找 `rime.dll` 二进制/安装产物层面，确认它到底把日志落在哪
- 还是你已经接受“weasel 也只是传 `log_dir`，没有现成日志桥”，让我直接给 Avalime 设计一套接日志方案
````
]




-T[2026_0710_215936][
	Avalime Windows 我啓動之後在控制檯上能看到正常的Rime引擎的日誌。
	Rime引擎源碼 在 `E:\_code\_clone\rime\librime`
	看看是怎麼回事。
][
````md
原因已经找到了，控制台里那批“正常的 Rime 引擎日志”不是意外。

关键代码在 [setup.cc](/E:/_code/_clone/rime/librime/src/rime/setup.cc:77)：

```cpp
FLAGS_minloglevel = min_log_level;
FLAGS_alsologtostderr = true;
if (log_dir) {
  if (log_dir[0] == '\0') {
    google::LogToStderr();
  } else {
    FLAGS_log_dir = log_dir;
  }
}
google::InitGoogleLogging(app_name);
```

这段的含义是：

- 只要启用了 logging，librime 就会**无条件**设 `FLAGS_alsologtostderr = true`
- 所以即使你传了非空 `log_dir`，日志也还是会**同时输出到 stderr**
- Avalime Windows 是 `dotnet run` 启动的控制台进程，DLL 往 `stderr` 打的内容你自然就在终端里看到了

调用链也对得上：
- [rime_api.cc](/E:/_code/_clone/rime/librime/src/rime_api.cc:57) 里 `RimeSetup()` 会在 `traits` 带了 `min_log_level/log_dir` 时调用 `SetupLogging(...)`
- Avalime 这边在 [RimeSetup.cs](/E:/_code/CsRime/Avalime/Avalime.Rime/proj/Avalime.Rime/RimeSetup.cs:225) 调的是 `apiFn.setup(traits)`
- 你在 [Avalime.Rw.jsonc](/E:/_code/CsRime/Avalime/Avalime.Frontend/proj/Avalime.Windows/Avalime.Rw.jsonc:9) 里配了 `log_dir`
- 所以 librime 已经按这条路径初始化 logging 了，只是它本来就会额外打 stderr

另外我也确认了你目录里的 `.log`：
- `D:\Program Files\Rime\Avalime\*.userdb\*.log`
- `commitHistory.ldb\*.log`

这些是 leveldb/userdb 的日志，不是 glog 的引擎日志文件。也就是说：
- “控制台能看到引擎日志”这件事，已经完全能用 `FLAGS_alsologtostderr = true` 解释
- “为什么 `log_dir` 下面没看到 glog 文件”是另一个问题，和“控制台为什么有日志”不是同一个问题

如果你要，我下一步就继续专门查第二个问题：
- glog 文件到底尝试写到哪
- 为什么没在 `D:\Program Files\Rime\Avalime\Log` 里生成
- 以及 Avalime 最稳妥该从 `stderr` 接，还是从文件接
````
]

-T[2026_0710_220539][
	那看一下weasel爲甚麼能使日誌輸出到文件 而我卻不行?
	看weasel是怎麼和rime引擎交互的 是走C Api 還是C++ API
][
````md
结果先摆出来。

weasel 和 Rime 的交互，走的是 **C API**，不是直接用 librime 的 C++ 对象接口。
证据很直接：

- [RimeWithWeasel.cpp](/E:/_code/_clone/rime/weasel/RimeWithWeasel/RimeWithWeasel.cpp:13) `#include <rime_api.h>`
- [RimeWithWeasel.cpp](/E:/_code/_clone/rime/weasel/RimeWithWeasel/RimeWithWeasel.cpp:86) `rime_api = rime_get_api();`
- 后面全是 `rime_api->setup(...)`、`initialize(...)`、`create_session()`、`process_key()`、`get_context()`、`get_status()` 这套 C API 调用
- [Configurator.cpp](/E:/_code/_clone/rime/weasel/WeaselDeployer/Configurator.cpp:48) 也是同样模式

所以 weasel 并没有绕开 `rime_api.h` 去直接 new `Service/Session/Deployer` 之类 C++ 对象。

再说日志。

weasel 确实会把日志落文件，而且我已经在你机器上看到了：
- `C:\Users\lenovo\AppData\Local\Temp\rime.weasel.Y7000P.lenovo.log.INFO...`
- `...WARNING...`
- `...ERROR...`

这和 weasel 代码完全一致：
- [WeaselUtility.h](/E:/_code/_clone/rime/weasel/include/WeaselUtility.h:34) 里 `WeaselLogPath()` 固定返回 `%TEMP%\rime.weasel`
- [RimeWithWeasel.cpp](/E:/_code/_clone/rime/weasel/RimeWithWeasel/RimeWithWeasel.cpp:141) 把这个路径塞进 `weasel_traits.log_dir`
- [RimeWithWeasel.cpp](/E:/_code/_clone/rime/weasel/RimeWithWeasel/RimeWithWeasel.cpp:140) 同时 `app_name = "rime.weasel"`

而 Avalime 这边，我也发现了一个关键事实：

它**其实也在落 glog 文件**，但不是落到你配置的 `D:\Program Files\Rime\Avalime\Log`，而是落到了 Temp：
- `C:\Users\lenovo\AppData\Local\Temp\rime.avalime.Y7000P.lenovo.log.INFO...`
- `...WARNING...`
- `...ERROR...`

这说明两件事：

1. Avalime 当前加载的 `rime.dll` 确实启用了 glog 文件输出，不是“完全不落盘”
2. 但它**没有按你以为的那组 traits 生效**，至少从结果看：
   - `app_name` 没体现成 `Avalime.Windows`
   - `log_dir` 也没体现成 `D:\Program Files\Rime\Avalime\Log`

这里出现了一个明显矛盾：

- 你运行时复制进去的 [Avalime.Rw.jsonc](/E:/_code/CsRime/Avalime/Avalime.Frontend/proj/Avalime.Windows/bin/Debug/net10.0/win-x86/Avalime.Rw.jsonc) 明明写的是
  - `app_name = "Avalime.Windows"`
  - `log_dir = "D:\\Program Files\\Rime\\Avalime\\Log"`
- 但实际产物却像是在按默认值/别的值走，落成了 `Temp\rime.avalime...`

按你的规则，这里我不能带着疑问继续硬猜。我现在能给出的可能性只有这几种：

1. 你当前加载的 `D:\ENV\Rime\weasel-0.15.0\rime.dll` 和 `E:\_code\_clone\rime\librime` 这份源码不是同一行为版本
2. Avalime 传给 `RimeSetup` 的 traits 实际值和你文件里看到的不一致
3. 这份 `rime.dll` 内部对 `app_name/log_dir` 做了别的默认化处理，导致最后仍落到 `Temp\rime.avalime`

如果你要，我下一步建议走这个顺序排：
1. 先在 Avalime 启动时把“实际传入 librime 的 `app_name/log_dir/min_log_level`”打印死
2. 再确认 `D:\ENV\Rime\weasel-0.15.0\rime.dll` 的来源和你本地 `librime` 源码是否同构
3. 必要的话，用同一份源码自己编一个 `rime.dll` 复现对比

你要我继续查哪条：
- 查 `rime.dll` 版本/来源差异
- 还是先在 Avalime 里把传给 librime 的 traits 值钉死验证
````
]

-T[2026_0710_221546][
	現在我把trime的源碼也clone下來了
	再給你一個參考的前端
	你看trime裏怎麼拿rime引擎的日誌的
	"E:\_code\_clone\rime\trime"
][
````md
`trime` 这边我看完了，结论很清楚。

它和 weasel 一样，跟 Rime 引擎交互走的也是 **C API**，不是直接用 librime 的 C++ API。
核心文件是 [rime_jni.cc](/E:/_code/_clone/rime/trime/app/src/main/jni/librime_jni/rime_jni.cc)：
- `#include <rime_api.h>`
- `Rime() : rime(rime_get_api()) {}`
- 后面全是 `rime->setup(...)`、`initialize(...)`、`set_notification_handler(...)`、`process_key(...)`、`get_context(...)` 这套 API

它拿日志的方式也很明确，不是“Rime API 主动把引擎日志回调给前端”。

`trime` 的做法是：

1. 让 librime **不要写文件，只打 stderr/logcat**
- [rime_jni.cc](/E:/_code/_clone/rime/trime/app/src/main/jni/librime_jni/rime_jni.cc:49)
```cpp
trime_traits.log_dir = "";  // set empty log_dir to log to logcat only
trime_traits.app_name = "rime.trime";
```

2. 启动 librime
- [rime_jni.cc](/E:/_code/_clone/rime/trime/app/src/main/jni/librime_jni/rime_jni.cc:55)
```cpp
rime->setup(&trime_traits);
rime->initialize(&trime_traits);
rime->set_notification_handler(notificationHandler, GlobalRef->jvm);
```

3. 前端日志页根本不是读 notification，而是直接读 **Android logcat**
- [Logcat.kt](/E:/_code/_clone/rime/trime/app/src/main/java/com/osfans/trime/util/Logcat.kt:38)
- [Logcat.kt](/E:/_code/_clone/rime/trime/app/src/main/java/com/osfans/trime/util/Logcat.kt:55)

它实际跑的是：
```kotlin
subprocess("logcat", "--pid=$it", "-v", "time")
```

`LogActivity` 再把这个 `Logcat` 挂到 UI：
- [LogActivity.kt](/E:/_code/_clone/rime/trime/app/src/main/java/com/osfans/trime/ui/main/LogActivity.kt:118)
```kotlin
logView.setLogcat(Logcat())
```

所以 `trime` 的链路是：

- librime glog -> stderr
- Android 把 native stderr 收进 logcat
- Trime 自己再起一个 `logcat` 进程去读
- UI 展示 logcat 文本

另外它的 `set_notification_handler(...)` 只拿 `schema / option / deploy` 这些通知：
- [rime_jni.cc](/E:/_code/_clone/rime/trime/app/src/main/jni/librime_jni/rime_jni.cc:247)

也就是说：
- notification 不是它显示“引擎日志”的主渠道
- 真正的实时日志页来源是 `logcat`

这对 Avalime 的参考意义很直接：

- `weasel`：偏“文件 + stderr”
- `trime`：偏“stderr/logcat only”
- 两者都**不是**“从 Rime API 直接拿完整引擎日志流”

所以 Avalime 如果想学 `trime`，Windows 上对应思路就是：
- 不指望 notification
- 直接接宿主进程的 `stderr` / console 输出流

如果你要，我下一步就专门给你分析：
- 在 Avalime Windows 里，最像 `trime` 的实现路径应该怎么接 `stderr`
- 还是改成像 weasel 那样稳定读 glog 文件更合适
````
]

-T[2026_0710_222116][
	那你說我該怎麼從stderr拿到日誌
	同時考慮Windows和Android平臺
][
````md
那就别把方案建立在 `stderr` 上。**跨 Windows 和 Android 的共同可行解，应该是“宿主层采集日志，再统一喂给 `IRimeLogSource`”**，但两端采集手段不同。

**我理解的需求是**：你要一套同时适用于 Avalime Windows 和 Android 的 Rime 引擎日志接入方案，目标是 UI 里的日志页能看到真实引擎日志，而不是只看 notification。

**核心目标**：统一 UI 抽象，平台分别采集。不要把 UI 绑定到 Windows 控制台，也不要把 Android 绑定到 logcat 这种平台特性。

**隐含需求清单**
- `[纳入]` UI 层继续只依赖 `IRimeLogSource`
- `[纳入]` Windows 和 Android 共用同一个日志缓冲/展示模型
- `[纳入]` 平台层各自负责“怎么拿日志”
- `[待定]` 是否要求“历史日志回放”
- `[待定]` 是否要求“导出完整日志文件”

**范围界定**
- 做：设计跨平台日志链路
- 不做：现在立刻改完整实现
- 不做：继续赌 `notification handler` 能提供完整引擎日志
- 不做：进程内回读自身 `stderr`

**关键边界条件**
- Android 没有普通文件控制台，天然更适合 `stderr -> logcat`
- Windows 当前已观察到 glog 文件落到 `%TEMP%`
- `log_dir` 行为和你预期不一致，不能先假设它稳定可控
- 同一平台上宿主日志和引擎日志可能混在一起，需要过滤

**业务影响**
- 影响 `Avalime.Windows`、`Avalime.Android` 宿主启动
- 影响 `Avalime.Rime` 的日志源接入方式
- UI 层改动应很小，主要复用现有 `RimeLogStore / RimeLogBuffer`

我建议的设计是：

**方案 A，推荐**
- `Avalime.Core` 保持 `IRimeLogSource`
- `Avalime.Rime` 保持 `RimeLogStore` 作为统一汇聚点
- Windows 宿主新增 `WindowsRimeLogCollector`
  - 直接 tail 实际 glog 文件
  - 把新增行 append 到 `RimeLogStore`
- Android 宿主新增 `AndroidRimeLogCollector`
  - 读取 `logcat` 中 `rime.trime`/`rime.avalime` 相关行
  - 把新增行 append 到 `RimeLogStore`

优点：
- UI 完全统一
- 平台差异被压在宿主层
- 不依赖“进程内读 stderr”这种脆方案
- Android 参考 `trime`，Windows 参考 glog 文件事实输出

缺点：
- 两个平台各要写一个 collector
- Windows 还得先钉死“实际 glog 文件路径怎么发现”

**方案 B，不推荐**
- 两端都强行走 `stderr`
- Windows 进程内重定向 `stderr`
- Android 继续读 logcat

问题：
- Windows 很脆
- 实现和维护成本高
- 不如文件 tail 稳定

如果你认可，我下一步就进入设计细化，给出：
1. 新增哪些类和接口
2. 放在哪些程序集
3. Windows/Android 各自的采集流程
4. 怎么接到现有 `RimeLogStore` 上

先确认一件事：
你接受“Windows 用文件采集、Android 用 logcat 采集”的平台分治方案吗？
````
]

-T[2026_0710_222528][
	Android logcat採集 具體是怎麼做的
][
````md
`trime` 的做法很直接：**不从 librime API 拿完整日志，而是把 librime 的日志打进 logcat，再由应用自己起一个 `logcat` 子进程去读。**

链路是这样：

1. JNI 启动 librime 时，把 `log_dir` 设为空串
   [rime_jni.cc](/E:/_code/_clone/rime/trime/app/src/main/jni/librime_jni/rime_jni.cc:49)

```cpp
trime_traits.log_dir = "";  // set empty log_dir to log to logcat only
trime_traits.app_name = "rime.trime";
```

这利用了 librime 的语义：`log_dir == ""` 时，glog 不写文件，只写 `stderr`。

2. Android 会把 native 层写到 `stderr` 的内容收进系统日志
   于是这些 glog 行就能在 `logcat` 里看到，tag 会带上 `rime.trime`

3. App 侧起一个 `logcat` 进程持续读取
   [Logcat.kt](/E:/_code/_clone/rime/trime/app/src/main/java/com/osfans/trime/util/Logcat.kt:55)

```kotlin
subprocess("logcat", pid?.let { "--pid=$it" } ?: "", "-v", "time")
    .also { process = it }
    .asFlow()
    .collect { flow.emit(it) }
```

它做的事就是：
- 执行 `logcat`
- 可选按 `pid` 过滤
- 用 `-v time` 输出带时间的文本行
- 一行一行读出来，发到 `SharedFlow<String>`

4. UI 日志页订阅这个 flow
   [LogView.kt](/E:/_code/_clone/rime/trime/app/src/main/java/com/osfans/trime/ui/main/log/LogView.kt:71)
   [LogActivity.kt](/E:/_code/_clone/rime/trime/app/src/main/java/com/osfans/trime/ui/main/LogActivity.kt:118)

所以“Android logcat 采集”具体做法就是：

- 让 librime `stderr` 输出进入 logcat
- App 自己执行 `logcat`
- 读 stdout
- 把每行文本塞进内存缓冲/UI

如果要在 Avalime.Android 里照着做，最小实现会是：

1. 启动 librime 时把 `log_dir` 设成 `""`
2. Android 宿主加一个 `LogcatCollector`
3. 它启动：
```text
logcat --pid=<当前进程pid> -v time
```
或更严格一点：
```text
logcat -v time -s rime.avalime
```
前提是你确认 tag 稳定
4. 逐行读取输出
5. 每行 `RimeLogStore.Inst.Append(...)`

要注意两个点：

- `--pid` 过滤的是**整个进程**日志，不只 librime，所以可能混进 Avalime 自己的日志
- 按 tag 过滤更干净，但前提是确认 Android 上 glog tag 真的是你设的 `app_name`

如果你要，我下一步可以直接帮你分析：
- Avalime.Android 里该把这个 `LogcatCollector` 挂在哪个生命周期
- 以及 Windows 对应该怎么做成同构接口
````
]

-T[2026_0710_222959][
	Android 会把 native 层写到 stderr 的内容收进系统日志
于是这些 glog 行就能在 logcat 里看到，tag 会带上 rime.trime

這是甚麼意思? 能不能講清楚點
][
````md
意思是：**在 Android 上，so 库里往 `stderr` 写的内容，不会像 Windows 控制台那样显示在黑窗口里，而是通常会被系统日志机制接管，最终能用 `logcat` 看见。**

你可以把它理解成这条路：

1. `librime` 内部用 glog 打日志
2. `trime_traits.log_dir = ""`
3. librime/glog 因此选择“不要写文件，只写 `stderr`”
4. Android 进程没有你平时理解的“控制台窗口”
5. 这些 `stderr` 输出会被 Android runtime / zygote / libc 那套日志通道接到系统日志里
6. `logcat` 是系统日志查看器，所以你能在 `logcat` 里看到这些行

“tag 会带上 `rime.trime`” 是因为：
- trime 启动 librime 时设了
  `app_name = "rime.trime"`
- glog 生成日志文件前缀/程序名标识时会用这个名字
- 在 Android 被转进 logcat 后，这个名字通常会体现在日志来源标识里，所以 trime 才会用类似：
  ```text
  logcat -s rime.trime:W
  ```
  去筛

但这里要注意一件事：
**这不是 Rime API 的功能，也不是 trime 主动“把日志发给 logcat”**。
而是：

- librime 只负责往 `stderr` 写
- Android 平台负责把进程的 `stderr` 并入系统日志
- trime 只是再去读 `logcat`

你可以把 Windows 和 Android 对比着看：

- Windows
  `stderr` -> 当前控制台窗口
- Android
  `stderr` -> 系统日志缓冲区 -> `logcat`

所以 `trime` 不是“直接拿到了 librime 日志对象”，而是“间接读到了 Android 已经收集好的系统日志”。

如果你愿意，我下一条可以继续讲更细一点：
- Android 上到底是哪个层把 `stderr` 收进 logcat
- 以及为什么 `log_dir=""` 能让 librime 只打 logcat 不落文件
````
]

-T[2026_0710_223122][
	那你現在用adb看我手機上的avalime 能不能看到rime引擎的日誌
][
````md
我用 adb 看了你手机上的现机情况。
结论先说：
目前我没有在 logcat 里看到 raw librime/glog 日志，只看到了普通的 Android / Avalime 应用日志。也就是说，现在还不能证明 Avalime 在 Android 上已经“拿到了 Rime 引擎原生日志”。
我确认到的事实：
设备在线：899PKZQWJ7VCBEWK
包名：Tsinswreng.Avalime
当前 PID：8413
Android 版本：13
Avalime Android 配置里确实是：Avalime.Rw.jsonc
log_dir = ""
app_name = "Tsinswreng.Avalime"

这说明源码配置是按“stderr -> logcat”这条路在走，和 trime 的思路一致。
但我实际用 adb logcat 搜了这些特征串：
I202
W202
E202
registry.cc
opencc.cc
core_module.cc
rime.lua
结果都是空。抓到的只有这类日志：
I/Avalime ...
D/Avalime ...
Android 系统自己的 Activity/Looper/WindowManager 日志
所以现在更准确的说法是：
Avalime 自己的应用日志能在 logcat 里看到
Rime 引擎那种 Windows 上的 glog 原生日志，目前没看到
这里有个不确定点，我得停下来跟你确认一下：
要不要我下一步做一次实时抓取？
做法是：
我先 adb logcat -c 清空缓冲
我挂着 adb logcat 实时监听
你在手机上手动打开 Avalime、切到输入法、随便打一段字
我再看启动和输入那一刻，raw librime 日志有没有冒出来
这一步最靠谱，因为现在这次是读已有缓冲，不能完全排除“日志早被冲掉”或者“你刚才没触发到引擎初始化”的情况。
````
]

-T[2026_0710_223858][
	我幫你 adb logcat -c又打了幾個字 你再找找  要是再找不到那就是真沒有了
][
````md
这次可以下结论了：

你手机上当前这版 Avalime，logcat 里没有 Rime 引擎的 raw 日志。

我看的是你 logcat -c 之后、重新打字产生的这一波新日志。结果是：

能看到 Avalime 自己的日志
例如 D/Avalime (8413): [Perf] RimeKeyProcessor process_key(...)
说明输入过程确实发生了，Rime 也确实在工作
但完全没看到 librime / glog 那种原生日志特征
没有 I202...
没有 W202...
没有 E202...
没有 registry.cc
没有 opencc.cc
没有 core_module.cc
没有 rime.lua
所以现在不是“没找对地方”，而是现状就是没有进 logcat。

结合前面源码，现状可以归纳成一句话：

Android 端配置虽然是 log_dir = ""，理论上是想走 stderr -> logcat
但你这台设备 / 这套运行方式下，librime 写到 stderr 的内容并没有以 raw glog 行出现在 adb logcat 里
这就意味着，Avalime 不能指望像 trime 那样，直接从系统 logcat 拿到 librime 原生日志。至少你当前这版、当前设备上不行。

接下来只有两条靠谱路：

改 librime
直接给 librime 加一个日志回调接口
或者把 glog sink 接出来
Avalime 直接收结构化日志
这是最稳的方案
在 Avalime native/bridge 层主动接管 stderr
但 Android 上这条路未必稳定，平台差异大
你现在这台机的实测已经说明，不能把它当可靠方案
如果你要，我下一步可以继续两件事里的一个：

我去查 trime 为什么能看到，而你这里看不到，是不是它 JNI / native 层还有额外处理
或者我直接给你设计一套 Avalime 可控的日志方案，不再依赖 logcat 运气
````
]
-T[][][]
-T[][][]
-T[][][]

