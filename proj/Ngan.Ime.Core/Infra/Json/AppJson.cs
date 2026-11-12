namespace Ngan.Ime.Infra.Json;

using Tsinswreng.CsCore;
using Tsinswreng.CsJson;

[Doc($$"""
全應用共用的 JSON 序列化門面。

*為何要有它*：業務與界面到處都是靜態類，沒辦法在建構子注入 `IJsonSerializer`；
若各處自己 `new JsonSerializer(options)`，配置就散了。故這裡給一個「不方便注入時直接用」的入口，
而 DI 容器註冊的是*同一個實例*（見 `DiCore.SetupCore`），兩條路徑不會拿到兩份不一樣的 options。

*為何不直接調 `System.Text.Json.JsonSerializer` 靜態類*：那條路徑只吃 options，
在 NativeAOT 下必須 options 自帶源生成 resolver 才安全。`Tsinswreng.CsJson.IJsonSerializer`
把這件事收在庫裡，調用方只要 `AppJson.Inst.Serialize(x)` 就好。

#Eg[
	```cs
	var Json = AppJson.Inst.Serialize(Foo);
	```
]
""")]
public static class AppJson {
	[Doc($$"""
	全局唯一的 `IJsonSerializer`；選項固定用 {{nameof(AppJsonCtx.WireOptions)}}。

	*為何延遲建立*：靜態初始化有順序問題（{{nameof(AppJsonCtx)}} 的選項本身也要先組好），
	用 `field ??=` 讓它第一次被用到時才建，就沒有「誰先誰後」的問題。
	""")]
	public static IJsonSerializer Inst => field ??= new JsonSerializer(AppJsonCtx.WireOptions);
}
