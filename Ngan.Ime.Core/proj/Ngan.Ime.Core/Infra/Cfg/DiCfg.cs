namespace Ngan.Ime.Infra.Cfg;

using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCore;
using Tsinswreng.CsCfg;

[Doc($$"""
配置層的依賴注入裝配。

*寫法照文檔約定*：`public static class DiXxx` ＋ `SetupXxx(this IServiceCollection z)`。

*為什麼要傳入門面*：門面是 {{nameof(AppCfg)}} 這一個實例，這裡只管註冊，不決定它從哪來。

*同一個實例註冊四種寬度*：門面同時是讀者、訪問器與聚合層，
但注入方不該為了讀一個值而拿到寫入能力。故按需注入：

- `ICfgReader`：只要讀的服務。
- `ICfgAccessor`：要改配置的地方。
- `ILayeredCfgAccessor`：要問「這一項來自哪個源」的設定頁。
""")]
public static class DiCfg {
	[Doc($$"""
	把一個配置門面註冊成四種寬度，全部指向同一個實例。
	#Prm[{{nameof(Cfg)}}][已接好源的門面；尚未呼叫 Setup 的門面也能註冊，只是空的。]
	#Rtn[同一個 {{nameof(IServiceCollection)}}，便於串接。]
	""")]
	public static IServiceCollection SetupCfg(
		this IServiceCollection z
		,LayeredCfgAccessor Cfg
	) {
		// 必須是同一個實例: 事件訂閱與寫入路由都掛在那一個物件上，
		// 分成多個實例會出現「這邊寫了那邊讀不到」。
		// 先按執行期型別註冊一次，讓 GetRequiredService<AppCfg>() 也能解析；
		// 再用三個接口寬度各註冊一次，注入方只拿自己需要的權限。
		z.AddSingleton(Cfg.GetType(), Cfg);
		z.AddSingleton<LayeredCfgAccessor>(Cfg);
		z.AddSingleton<ICfgReader>(Cfg);
		z.AddSingleton<ICfgAccessor>(Cfg);
		z.AddSingleton<ILayeredCfgAccessor>(Cfg);
		return z;
	}
}
