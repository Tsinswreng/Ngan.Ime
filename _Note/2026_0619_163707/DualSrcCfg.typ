2026_0619_163759[
看 E:\_code\CsNgan.Dict\Ngan.Dict.Frontend\proj\Ngan.Dict.Android\MainActivity.cs
E:\_code\CsNgan.Dict\Doc\Spec\Cfg.typ
參照這個項目的雙源配置模式
在Ngan.Ime也引入一下。
用了Tsinswreng.CsCfg這個包。

結構先弄下面這些東西
```
Librime{
	AutoLinkDll = false
	DllPath
	RimeTraits{
		user_data_dir
		app_name
	}
}

```
注意兼容AOT

]

#[
````md
先停一下，我这边有几个关键不确定点，不能带着猜测往下做。

1. 双源配置的文件名和落盘位置
现在 Ngan.Dict 是：
- 只读配置：主配置文件
- 可写配置：由 `RwCfgPath` 决定

Ngan.Ime 这边你要我参照双源模式，但你还没给：
- 主配置文件名是不是类似 `Ngan.Ime.jsonc`
- 可写配置文件名默认是不是类似 `Ngan.Ime.Rw.jsonc`
- Android 上是放 `FilesDir`、`ExternalFilesDir`，还是别的位置

2. 配置作用域
你这次只提了：
```text
Librime{
  AutoLinkDll = false
  DllPath
  RimeTraits{
    user_data_dir
    app_name
  }
}
```
我不确定这是：
- 只给 Android 用
- 还是 Ngan.Ime 全平台统一配置结构，Android 只是先接入

3. “双源优先级”怎么取
Ngan.Dict 是 `RoCfg + RwCfg` 叠加。
Ngan.Ime 这里我不确定你要不要完全一样：
- `Rw` 覆盖 `Ro`
- 读不到时回默认值
- `DllPath` / `user_data_dir` / `app_name` 都允许被 `Rw` 覆盖

4. AOT 兼容的边界
你特地强调了 AOT，我需要确认你要的是：
- 只避免反射式动态访问配置节点
- 还是连 `JsonSerializer` / 泛型节点定义都要按最保守方式来写

5. 代码落点
我目前判断最自然的是：
- 在 Ngan.Ime 新增一套 `KeysCfg` / `AppCfg`
- 在 Android `Application.cs` 里初始化双源配置
- `RimeSetup` 改成从配置对象读取，而不是直接用静态字段硬编码

但这会把配置初始化放进 Android 宿主层。
我不确定你是否接受，还是你希望把配置初始化放到 `Ngan.Ime.UI` 公共层。

你先把这 5 点拍一下，尤其是第 1 点文件名/路径。
不然我现在动手，后面大概率要返工。
````
]

2026_0619_165224[
盡量仿照Ngan.Dict的行爲。
只讀配置 優先級最高
仿照
E:\_code\CsNgan.Dict\Ngan.Dict.Core\Infra\Cfg\KeysClientCfg.cs
把 RwCfgPath 加進頂層配置鍵來。

只讀配置 的文件名就 Ngan.Ime.Ro.jsonc、裏面只指定 RwCfgPath 。
然後 RwCfgPath 值 是 Ngan.Ime.Rw.jsonc

然後你還要準備一個寫好的 Ngan.Ime.Rw.jsonc。

在安卓上放的位置也仿照CsNgan.Dict的
先在 /data/裏面的私有目錄放一份原始的沒動過的
如果 /sdcard/Android對應位置沒有 就把/data/裏面的複製過來
然後讀和寫都在/sdcard/Android對應位置操作

配置是 Ngan.Ime 全平台统一配置结构，Android 只是先接入。

AOT兼容那個就看CsNgan.Dict 。那邊就是AOT兼容的
寫在 E:\_code\CsRime\Ngan.Ime\Ngan.Ime.Core\proj\Ngan.Ime.Core\Infra\KeysCfg.cs 裏

]
