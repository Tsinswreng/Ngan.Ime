namespace Ngan.Ime;

using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCore;
using Tsinswreng.CsJson;
using Ngan.Ime.Infra.Json;

[Doc($$"""
Core 程序集（抽象層與基礎設施）的依賴注入裝配。

*命名與寫法照文檔約定*：每個需要配 DI 的程序集各有 `Di<程序集>` 類，
裡面是 `Setup<程序集>(this IServiceCollection z)`；這裡是 Core 本體，故叫 {{nameof(DiCore)}}。

*放什麼*：只放「本程序集提供的東西」——JSON 序列化門面。
宿主專屬的實現（儲存區、日誌內層）不屬於這裡，由各宿主自己的 `DiXxx` 註冊。

#Eg[
	```cs
	// Ui 層與各宿主都會先呼叫它
	z.SetupCore();
	```
]
""")]
public static class DiCore {
	[Doc($$"""
	註冊共享層提供的服務。
	#Rtn[同一個 {nameof(IServiceCollection)}，便於串接。]
	""")]
	public static IServiceCollection SetupCore(
		this IServiceCollection z
	) {
		// JSON 序列化門面。注入的就是 AppJson.Inst 那一個——
		// 靜態場合與注入場合共用同一份 options 與轉換器，
		// 不會出現「同一段 JSON 兩條路徑寫出不同結果」。
		z.AddSingleton<IJsonSerializer>(_ => AppJson.Inst);

		// 配置門面不在這裏註冊：它由宿主在 Setup 之後呼叫 DiCfg.SetupCfg 決定，
		// 見 Ngan.Ime.Infra.Cfg.DiCfg 的說明。
		return z;
	}
}
