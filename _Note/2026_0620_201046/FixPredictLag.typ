2026_0620_201057[
看
D:\Program Files\Rime\User_Data\_tsToLua\src\Tsinswreng\mod\predict3\PredictMod.ts

在同文輸入法(trime)中使用這個模塊時沒有任何問題
ngan.ime與trime都使用相同的rime動態庫
但在ngan.ime中 開啓聯想詞後、起初沒有問題。
當我多次隱藏鍵盤又彈出鍵盤後、聯想詞上屏會越來越卡

看看是怎麼回事

不要瞎猜 缺信息就自己想辦法收集信息


]

#[
````
Rime 侧 (PredictMod)
_init() 只在 Processor.init(env) 中调用一次（Ngan.Ime 只创建一个 Rime session）
on_commitFn 每次 commit 都会触发（不管 predict 开关是否开启），执行 updateState + updateDynamicStorage（写 Memory）
只有 mkEtAssignPreparedPredictCtxs（生成联想候选）受 predict 开关控制
历史记录 bounded（CyclicArray(4)），不会无限增长
动态词典 Memory 是持久化的，随时间使用会积累条目
Ngan.Ime C# 侧
Rime session 在 RimeSetup 构造时创建一次，永不销毁（destroy_session 只是 TODO）
每次键盘隐藏 → OnFinishInputView → 强制 _shouldRecreateInputView = true
每次键盘弹出 → 重建整个 Avalonia UI（MainView → ViewIme → VmIme 等）
每次按键后 AfterInput → SyncStateFromRime() → Rime.get_context() 读取候选
我的疑问
从代码分析中，我没有找到一个明显的「每次 hide/show 会积累」的 bug。UI 重建时的 dispose 链看起来是干净的，Rime session 也没有被重建。

在我开始加诊断代码之前，想确认几个问题：

「聯想詞上屏會越來越卡」具体是指哪个环节变慢？ 是：A) 点击联想候选后文字出现在目标 app 中的延迟变长，还是 B) 联想候选列表出现的延迟变长，还是 C) 两者都有？

大概 hide/show 多少次后会开始感觉到卡？ 5次？20次？越多越明显？

如果关掉 predict 开关（打完字不出联想词的那种状态下打字上屏），是否也会变卡？ 这个可以帮我判断是 PredictMod 的处理变慢了、还是 UI/sync 的路径变慢了。

Ngan.Ime 的 user_data_dir 配置和 Trime 是同一个目录吗？ 这会决定两边的动态词典是否共享。

这些问题会帮我精准定位应该在哪个环节添加诊断代码。
````
]


2026_0626_122938[

具體卡的表現是 按下幾個字母按鍵 然後按空格上屏 需要等明顯的時間 纔上屏+出現聯想候選。
當前候選字上屏+新的聯想候選彈出 看起來是同時發生的。

隱藏又彈出後 次數越多越卡

關掉聯想就不怎麼卡

聯想詞的用戶詞庫 我都會定期清理。當前 Ngan.Ime的UserDataDir是直接從Trime的複製過去的、
聯想詞用戶詞庫文件夾大小不到1M 不應該卡

這個聯想詞插件在windows端的小狼毫輸入法(weasel)上也用着 也是一點問題都沒有

我又發現一個問題、
關了聯想詞功能、多次隱藏再彈出  直接按按鍵也會卡、只是卡頓延遲積攢的程度比較慢

之前這個問題更加嚴重、後面是我讓AI加了dispose機制纔緩解。
但現在看來只是緩解了、問題還是一樣的問題、就是多次產生的時候有些東西沒清幹淨 他還是一直累積了一些東西


]



2026_0627_104644[
看
D:\Program Files\Rime\User_Data\_tsToLua\src\Tsinswreng\mod\predict3\PredictMod.ts

同文輸入法(trime)和小狼毫輸入法都是rime的成熟的前端(發行版)、我在trime和weasel中中使用這個模塊時沒有任何問題

ngan.ime與trime都使用相同的rime動態庫
但在ngan.ime中 開啓聯想詞後、有時候聯想詞上屏會很卡 有時候又不怎麼卡

具體卡的表現是 按下幾個字母按鍵 然後按空格之後、 需要等明顯的時間 纔上屏+出現聯想候選。
候選字上屏+新的聯想候選彈出 看起來是同時發生的。

我找不到卡與不卡的規律

舉個例子

我在試驗之前 連續輸入了多次「關係關係關係...」這樣的字串、
動態聯想詞庫中記錄了 「關係」->「關係」的映射
所以我上屏「關係」後、聯想出來的第一個候選也是「關係」
然後當前這個聯想詞插件的邏輯是拿候選詞去查庫
也就是 假如我按下 「kdkg」後、候選詞的首個候選是「關係」
然後我按空格上屏、接下來彈出的首個聯想候選也是「關係」
如果我一直多次按空格、那我就會一直上屏「關係」。
這裏我已經盡量控制變量了、我每次上屏的候選詞都是「關係」、
聯想詞插件每次都拿「關係」去查庫、
就打幾次 動態聯想詞庫的大小不會有明顯變化
聯想出來的首個候選也是「關係」。

但就是這樣 每次上屏關係的時候 卡頓時間都不一樣
有時候基本感覺不到卡頓、有時候又明顯卡頓

聯想詞的用戶詞庫 我都會定期清理。當前 Ngan.Ime的UserDataDir是直接從Trime的複製過去的、
聯想詞用戶詞庫文件夾大小不到1M 不應該卡

看看是怎麼回事。

主要看ngan.ime、大概率不是插件的問題

不要瞎猜 缺信息就自己想辦法收集信息


]


2026_0627_124733[
剛剛試打了很多字
有個新發現
當我第一次 adb install 安裝輸入法後
第一次彈出鍵盤 開聯想 怎麼按好像都不卡

隱藏過一次鍵盤之後再彈出 他就開始卡了
但好像不是隱藏-彈出次數越多越卡

]
