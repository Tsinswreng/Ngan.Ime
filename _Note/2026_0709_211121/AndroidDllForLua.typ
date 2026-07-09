-T[2026_0709_211140][
先看skill
看skill的流程來。
然後看
CsRimeLua
這個項目。
這個項目目前是 用AOT編譯出dll然後給lua調用的。
看
`D:\Program Files\Rime\User_Data\_tsToLua\src\Tsinswreng\_lib\CsRimeLua.ts`
及其他相關文件(不只這個一個文件)

目前已經成功在win-x86平臺上編譯出了dll 並成功調用了
之前我也研究過 編譯出安卓 linux-bionic-arm64的so
但是我太久沒弄了。
你研究一下
看看能不能AOT編譯出.so庫
然後給avalime這個項目的安卓版本上用。

.net 版本要.net 10的  如果不是.net10你就给我改成.net 10
自己開wsl編譯
][
````md
已经验证到可执行层面了：

`CsRimeLua.Android` 在 `.NET 10` 下，**可以**在 Ubuntu WSL 里成功 AOT 编出 Android 用的 `linux-bionic-arm64` `.so`。产物在 [CsRimeLua.Android/bin/Release/net10.0/linux-bionic-arm64/publish/CsRimeLua.Android.so](/E:/_code/CsRime/CsRimeLua/proj/CsRimeLua.Android/bin/Release/net10.0/linux-bionic-arm64/publish/CsRimeLua.Android.so)，我也确认了它确实导出了你要的两个符号：`GetUnixTimeMs` 和 `GetWeekOfYear`。

另外我查了依赖，当前这个 `.so` 需要 `libdl.so`、`libm.so`、`libc.so` 和 `libc++_shared.so`。其中 `libc++_shared.so` 你在 [Application.cs](/E:/_code/CsRime/Avalime/Avalime.Frontend/proj/Avalime.Android/Application.cs:67) 已经有预装载逻辑，这部分方向是对的。

但这里有一个必须停下来跟你确认的点，不确认我不能继续改：

现在有**文件名/路径设计冲突**。
- AOT 产物名字是 `CsRimeLua.Android.so`
- Avalime 当前拷贝/期待的是 `libCsRimeLua.so`，见 [Application.cs](/E:/_code/CsRime/Avalime/Avalime.Frontend/proj/Avalime.Android/Application.cs:67)
- Lua 侧 `linux-bionic-arm64` 分支现在写死的是 Trime 包名路径 `/data/data/com.osfans.trime/.../libCsRimeLua.so`，见 [LibPath.ts](</D:/Program Files/Rime/User_Data/_tsToLua/src/Tsinswreng/LibPath.ts:19>)

也就是说，“能编出来”已经成立，但“给 Avalime 安卓版直接用”这一步，当前路径约定还是 Trime 时代的，不一定对 Avalime 适用。

我建议你在下面两种方案里选一个，我再继续：

1. 我按 Avalime 现状来收口
把 AOT 产物统一重命名/复制成 `libCsRimeLua.so`，并把 Lua 侧安卓路径从 `com.osfans.trime` 改成 Avalime 实际包内可访问路径。

2. 我先不改 Lua 路径，只做 Avalime 内部装载验证
也就是先确认 Avalime 自己能把这个 `.so` 放进内部目录并成功 `loadlib`，等你决定最终包名/用户目录策略后，再改 `LibPath.ts`。

如果你要我继续，我下一步会直接做方案 1 或 2，不再停在调研。
````
]



-T[2026_0709_212459][
	你先看.ts裏的
	告訴我是在哪裏加載dll的
][

]


-T[2026_0709_212825][

我分別在Avalime.Android 和 weasel 0.15.0上執行`$RimeInfo`命令(`D:\Program Files\Rime\User_Data\_tsToLua\src\Tsinswreng\mod\cmd2\basicCmds\RimeInfo.ts`)

分別得到如下輸出
````json
{
	"user_id": "56730ff0-27cf-4b75-93af-e031cab1c7c4",
	"sync_dir": "/storage/emulated/0/Android/data/Tsinswreng.Avalime/files/UserData/sync",
	"rime_version": "1.11.2",
	"shared_data_dir": ".",
	"distribution_code_name": "",
	"distribution_version": "",
	"user_data_dir": "/storage/emulated/0/Android/data/Tsinswreng.Avalime/files/UserData",
	"distribution_name": ""
}
````


```json
{
	"distribution_version": "0.15.0.0",
	"distribution_name": "小狼毫",
	"sync_dir": "d:\\Program Files\\Rime\\User_Data\\sync",
	"shared_data_dir": "D:\\ENV\\Rime\\weasel-0.15.0\\data",
	"user_id": "56730ff0-27cf-4b75-93af-e031cab1c7c4",
	"distribution_code_name": "Weasel",
	"user_data_dir": "d:\\Program Files\\Rime\\User_Data",
	"rime_version": "1.8.5"
}
```

你看 avalime中的 RimeAPI設置
看看能不能在這裏入手。

看看有沒有能設置 distribution_code_name 的地方
把avalime android 的 distribution_code_name 設成 `Tsinswreng.Avalime.Android`

然後再在ts中用`distribution_code_name` 來判斷。
先不動代碼 先幫我分析

][
````md
可以，从 Avalime 这里入手，而且路径很清楚。

`$RimeInfo` 这边不是自己拼的，它直接读 librime API：
- [getRimeVersionInfo.ts](</D:/Program Files/Rime/User_Data/_tsToLua/src/Tsinswreng/getRimeVersionInfo.ts:4>) 里 `distribution_code_name = rime_api.get_distribution_code_name()`
- 所以你看到的空字符串，说明是 **librime 初始化时没有拿到这个 trait**

上游 librime 本身支持这个字段：
- [rime_api.h](/E:/_code/_clone/rime/librime/src/rime_api.h:82) 的 `RimeTraits` 明确有
  `distribution_name`
  `distribution_code_name`
  `distribution_version`
- [setup.cc](/E:/_code/_clone/rime/librime/src/rime/setup.cc:58) 里 `SetupDeployer()` 会把这几个字段从 `RimeTraits` 灌进 deployer
- [deployment_tasks.cc](/E:/_code/_clone/rime/librime/src/rime/lever/deployment_tasks.cc:146) 里会把它们写进 `installation.yaml`

Avalime 绑定层也已经有这些字段，不缺 ABI：
- [RimeTraits.cs](/E:/_code/CsRime/CsRimeApi/proj/Rime.Api/RimeTraits.cs:23) 已经定义了
  `distribution_name`
  `distribution_code_name`
  `distribution_version`

现在 Avalime 的问题不是“librime 不支持”，而是“Avalime 没有往里填”：
- [KeysCfg.cs](/E:/_code/CsRime/Avalime/Avalime.Core/proj/Avalime.Core/Infra/Cfg/KeysCfg.cs:16) 目前只给 `RimeTraits` 暴露了
  `user_data_dir`
  `app_name`
- [RimeSetup.cs](/E:/_code/CsRime/Avalime/Avalime.Rime/proj/Avalime.Rime/RimeSetup.cs:116) 也只把
  `user_data_dir`
  `app_name`
  写进 `traits`
- [Application.cs](/E:/_code/CsRime/Avalime/Avalime.Frontend/proj/Avalime.Android/Application.cs:142) 启动时也只往配置里写了
  `user_data_dir`
  `app_name`

所以结论是：

**能设，而且就该在 Avalime 的 `RimeTraits` 初始化链上设。**
`app_name` 不是 `distribution_code_name`，这两个是不同字段。你现在把 `app_name` 设成包名，只会影响日志/应用标识，不会让 `$RimeInfo` 里的 `distribution_code_name` 变成你想要的值。

如果后面要改，最自然的落点是这三层一起补：
1. `KeysCfg.Librime.RimeTraits` 增加 `distribution_name / distribution_code_name / distribution_version`
2. `Application.cs` 给 Android 默认写入 `distribution_code_name = "Tsinswreng.Avalime.Android"`
3. `RimeSetup._setupRimeTraits()` 把这几个值写入 `traits->...`

然后 TS 侧再改成优先看 `distribution_code_name` 来判断宿主。

还有一个你应该注意的点：
- 这几个字段还会参与 `installation.yaml` 的部署信息判断，见 [deployment_tasks.cc](/E:/_code/_clone/rime/librime/src/rime/lever/deployment_tasks.cc:126)
- 所以后续你一旦改了 `distribution_code_name`，首次启动时很可能会触发一次 installation info 更新，这通常是正常现象，不是副作用

目前我的判断是：这个方案方向对，而且比继续依赖路径猜测更稳。
如果你要，我下一步可以只做“改动点设计单”，先不下手改代码。
````
]

-T[2026_0709_213840][
	那你先實現 在avalime中把 code_name 填上去 這個東西。
	先想想怎麼實現 先不動代碼。
	不同平臺 後綴要不一樣。
	比如在android 就叫 Tsinswreng.Avalime.Android
][

]
