-Sesn[
-T[2026_0722_195910][
先看skill;
看Avalime
現在我想給輸入法加個語音輸入功能。
要支持多語言。
參考CsNgaq詞典的的語言選擇功能。
`E:\_code\CsNgaq\Ngaq.Frontend\proj\Ngaq.Ui\Views\Dictionary\ViewDictionary.cs`


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

下面是我的尝试记录
`E:\_code\CsRime\Avalime\_Note\2026_0630_082336\SttEngineExperienceRecord.typ`

][

]
]



-Sesn[
-T[2026_0726_110929][
先看skill;
看Avalime
現在我想給輸入法加個語音輸入功能。
要支持多語言。
參考CsNgaq詞典的的語言選擇功能。
`E:\_code\CsNgaq\Ngaq.Frontend\proj\Ngaq.Ui\Views\Dictionary\ViewDictionary.cs`


1.用http api聯網調別人的引擎。
支持的語種要多。
我會說中英日語、在學西班牙 意大利 法語、以後可能學德語。
不要翻牆的。

2.先做好windows的、我確認無誤 再把一些細節修好之後再弄到安卓上來。
在安卓上編譯麻煩。如果是調http api的話不需要太多系統專屬適配層。

現在我決定用火山引擎的流式語音識別。
前端UI已經寫了。
你去寫一下後端和其他部分。
寫一下Decl
][
````md
我已完成技能與項目記憶閱讀，也檢查了現有語音輸入前端骨架。目前先停在需求確認階段，尚未修改代碼。

請確認上面 3 個選項，以及是否同意把 `VmVoiceInput` 的構造函數改為注入語音 Provider。你確認後，我會先提交完整的後端 Decl（含詳細註釋和接口設計），不先寫 Impl。
````
]

-T[2026_0726_111348][
	1 配置文件
	2 要抽象
	3 要抽象

	其他的低級廢話問題屬于常識 不要爲了問而問
][

]


-T[2026_0726_161856][
	我稍微改了點東西、也用`//Note`做了些標注。你看一下
][
````md
我看完了。两处 `//Note` 的判断如下，同时发现几个 Decl 契约问题。

- [VoiceRecognitionContracts.cs](E:/_code/CsRime/Avalime/Avalime.Core/proj/Avalime.Core/Voice/VoiceRecognitionContracts.cs:48)：接口要求 `IReadOnlyList<INormLang>`，但火山实现声明为 `IReadOnlyList<NormLangDetail>`。隐式接口实现要求属性类型匹配，这里大概率无法编译；而且语言选择页需要 `NativeName/EnglishName`，应统一使用 `IReadOnlyList<INormLangDetail>`。
- [VmVoiceInput.cs](E:/_code/CsRime/Avalime/Avalime.Frontend/proj/Avalime.UI/Views/VoiceInput/VmVoiceInput.cs:27)：VM 只注入了 Provider，没有注入 `IAudioInputSource`，因此它能建立远端会话，却无法启动麦克风并把音频送进去。需要注入音频源，或者增加一个编排两者的语音服务门面。
- [VoiceRecognitionContracts.cs](E:/_code/CsRime/Avalime/Avalime.Core/proj/Avalime.Core/Voice/VoiceRecognitionContracts.cs:15)：新增的 `VoiceRecognitionResult` 与 UI 原有的 `VoiceRecognitionTextUpdate` 语义重复，但 `VmVoiceInput.ApplyRecognitionText` 仍接收后者。应统一为 Core 中的模型，否则实现阶段必须做没有价值的 DTO 映射。
- [VolcengineVoiceRecognitionProvider.cs](E:/_code/CsRime/Avalime/Avalime.Frontend/proj/Avalime.Windows/Voice/VolcengineVoiceRecognitionProvider.cs:6)：火山 HTTP/WebSocket Provider 放在 `Avalime.Windows` 会导致 Android 无法直接复用。Windows 专属部分只有音频采集；火山 Provider 和配置应放进独立的跨平台程序集，例如 `Avalime.Voice.Volcengine`。
- [AudioInputContracts.cs](E:/_code/CsRime/Avalime/Avalime.Core/proj/Avalime.Core/Voice/AudioInputContracts.cs:11)：`SampleRate` 和 `ChunkDurationMs` 改为 `i64` 没有实际收益。采样率和毫秒分片不可能接近 `i32` 上限，Windows 音频 API 通常也接收 `i32`，建议还原。

关于 `CurrentLangDisplay` 的 Note：

你的怀疑有道理，但不一定要用转换器。这个文本是 `CurrentLang` 的派生展示值，更合适的是只读计算属性：

```cs
public str CurrentLangDisplay{
	get{
		return CurrentLang is INormLangDetail Detail
			? $"{Detail.Code}  {Detail.NativeName}"
			: CurrentLang.Code;
	}
}
```

然后 `CurrentLang` 改变时通知 `CurrentLangDisplay`。这样不保存重复状态，也不会出现语言已更新、显示文字未同步的问题。转换器更适合多个页面共用同一种纯展示转换时再抽取。

关于 `ReadOnlyMemory<byte>` 的 Note：

这里不用 `Stream` 是合理的：

- WebSocket 需要一包一包发送，`ReadOnlyMemory<byte>` 能明确表达分片边界。
- 麦克风是持续产生数据的实时源，`IAsyncEnumerable` 更自然地表达“等待下一片”和取消。
- `Stream.ReadAsync` 只能表达字节流，不能保证一次读取对应一个预期的 100ms 音频包。
- 每个分片可以直接传给 WebSocket，避免拼装整段录音。

不过契约必须补充内存生命周期：底层若复用缓冲区，消费者能否在下一次枚举后继续持有该 `ReadOnlyMemory<byte>`。我建议规定“分片在下一次 `MoveNextAsync` 前有效，消费者不得保存”，后续实现可使用缓冲池，避免每 100ms 分配一个新数组。

以上属于 Decl 阶段需要先修正的设计问题。
````
]

-T[2026_0726_162229][
	統一用 IReadOnlyList<INormLang>。

音頻包這個地方、到底用哪個? 一會說 IAsyncEnumerable 一會又說 ReadOnlyMemory<byte> 的。
其他有些你說有問題的地方都是你自己寫的、不是我改了纔有的。我不知道。
][
````md
你说得对，那些问题多数来自我原先的 Decl，不是你修改造成的。我刚才的表达容易让人误解，责任在我。

语言列表按你的决定，统一为：

```cs
IReadOnlyList<INormLang>
```

音频这里不是二选一，最终类型是：

```cs
IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunks(CT Ct);
```

两层含义不同：

- `IAsyncEnumerable<T>`：持续异步产生很多音频包。
- `ReadOnlyMemory<byte>`：其中一个音频包的数据。

相当于：

```cs
IAsyncEnumerable<ReadOnlyMemory<byte>>
// 异步序列             单个音频包
```

我推荐保持这个设计，不使用 `Stream`。麦克风每隔约 100ms 产生一个明确分片，随后直接作为一个 WebSocket 音频包发送；`IAsyncEnumerable<ReadOnlyMemory<byte>>` 能自然保留分片边界、异步等待和取消语义。

契约可以明确为：

```cs
public interface IAudioInputSession : IAsyncDisposable{
	/// 持续输出独立、只读的 PCM 音频分片。
	/// 每个分片拥有稳定存储，消费者可以在当前发送操作完成前安全使用。
	public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunks(CT Ct);

	public Task<nil> Stop(CT Ct);
}
```

所以结论就是：外层用 `IAsyncEnumerable` 表示持续流，内层用 `ReadOnlyMemory<byte>` 表示单个包。后续我会自行修正我原 Decl 中的其余不一致，不再把它们混同为你的改动。
````
]

-T[2026_0726_162550][
	那你再去改一次Decl。
	已經確定了的`//Note`就去掉。
	確保所有Decl都寫完。等下我讓你實現windows版的時候你應該只能寫Impl函數實現
	別又回頭補Decl
][

]

-T[2026_0726_170801][
	再回去看一下`//Note`。
	還有、火山引擎這個 有必要專門建一個程序集嗎?
	還是弄Avalime.Backend 程序集、把語言服務後端代碼放裏面。
][
````md
看到了 4 个新 `//Note`。

关于 JSON：同意你的标注。火山 DTO 成员直接使用 JSON 字段名，例如 `user`、`uid`、`model_name`，删除 `JsonPropertyName`。这是协议 DTO，不按普通业务模型命名。

关于 `byte[]`：.NET 10 并没有“不应使用 `byte[]`”的规则，应按语义选择：

- 同步借用缓冲区：`Span<byte>` / `ReadOnlySpan<byte>`
- 跨异步调用传递：`Memory<byte>` / `ReadOnlyMemory<byte>`
- 方法创建并转移一块独立、定长、可长期持有的存储：`byte[]` 合理

这里调整为：

- `EncodeStartRequest`、`EncodeAudio` 返回 `ReadOnlyMemory<byte>`，表达只读完整消息。
- `Decode` 接收 `ReadOnlyMemory<byte>`，保持不变。
- `MkHeader` 不再返回数组，改为 `WriteHeader(Span<byte> Destination, ...)`，避免单独分配 4 字节数组。
- `Compress`、`Decompress` 仍需要产生一块独立、长度可变的存储。返回 `byte[]` technically 合理，但为了接口语义统一，可返回 `ReadOnlyMemory<byte>`；底层仍大概率是数组，并不会因此自动减少分配。

程序集方面，我推荐改成统一的 `Avalime.Backend`，目前没有必要为火山单独建立程序集。

建议结构：

```text
Avalime.Core
  Voice/
    IAudioInputSource
    IVoiceRecognitionProvider
    通用 DTO / 异常

Avalime.Backend
  Voice/
    Volcengine/
      Provider
      Session
      ProtocolCodec
      ProtocolModels
      Options

Avalime.Windows
  Voice/
    WindowsAudioInputSource
    WindowsAudioInputSession
    WindowsVoiceComposition
```

边界是：

- `Avalime.Backend`：跨平台、非 UI 的云服务后端和以后其他语言服务。
- `Avalime.Windows`：WASAPI、NAudio 等 Windows 专属适配。
- `Avalime.Android`：未来只实现 Android 音频采集，直接复用 `Avalime.Backend` 的火山代码。

以后增加阿里、翻译、TTS 等，可放：

```text
Avalime.Backend/Voice/Aliyun
Avalime.Backend/Translation
Avalime.Backend/TextToSpeech
```

这样比“一家供应商一个程序集”更适合当前规模。等某个后端依赖非常重、需要独立发布或存在依赖冲突时，再拆程序集即可。
````
]

-T[2026_0726_171353][
	那就是返回值的 多個byte能用byte[]、入侵要用Memory<>或別的。
	還有一件事、我記得NAudio對NativeAOT支持不佳來着。
	現在先不改、用着先。
	那你去改吧。
][

]


-T[2026_0726_192344][
	語音輸入與Rime引擎無關。
	Rime引擎正在部署的時候會鎖在日誌頁面、
	進不去語音輸入頁面。
	看看怎麼改。
][

]

-T[2026_0726_192617][
	我問你 看看怎麼改。 是讓你想方案、沒讓你動手改！
	日誌頁面不應該是鎖定的、只是在部署階段先進日誌頁面。
	部署完之後自動轉到鍵盤頁面。
	如果用戶自己主動跳轉到別的頁面就讓他跳。

	你覺得呢

][

]


-T[2026_0726_200724][
	一直卡在 等待最終識別結果、
	中途也沒見任何輸出。
	中途沒法取消、
	然後必報錯 等待火山引擎超時
][

]

-T[2026_0726_201227][
	一樣的表現。
	正在識別時改成點擊取消。
	你tm有病吧  我的意思是 結束錄音之後等待識別時 再點擊是取消。你tm這樣改完之後是直接取消錄音了  完全沒法正常結束錄音再提交 這麼蠢的嗎???? 傻屄東西 也不知道問
][

]



]
