namespace Ngan.Ime.Infra.Json;


[Doc($$"""
標記接口：實現它 ＝ 這個型別要進 JSON 源生成上下文（{{nameof(AppJsonCtx)}}）。

*為何要有這層標記*：NativeAOT 下沒有反射式序列化，凡是要上網線、或被按名讀寫成員的型別，
都必須在 {{nameof(AppJsonCtx)}} 上有 `[JsonSerializable]`。手寫那份清單早晚會漏
（漏了不會編譯錯，只會在 AOT 執行期才炸），故改成「型別自己聲明」＋「腳本掃描生成」。

*誰在掃*：`proj/Ngan.Ime.Frontend/Ngan.Ime.Scripts` 的 `GenAppJsonCtx`——用 Roslyn 載入
`Ngan.Ime.Core` 專案，收集所有實現／繼承本接口的型別，產出
`proj/Ngan.Ime.Core/Tsinswreng.SrcGen/Ngan.Ime.Gen.g.cs`。

*要打在哪*：打在「會被 JSON 碰到的介面或具體類型」上，而不是每個實作類各打一次。

#Eg[
	```cs
	public class SomeDto : IAppSerializable { /* … */ }
	```
]
""")]
public interface IAppSerializable{
}
