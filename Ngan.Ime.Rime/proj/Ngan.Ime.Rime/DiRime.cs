namespace Ngan.Ime.Rime;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tsinswreng.CsCore;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;

[Doc($$"""
Rime 適配層的依賴注入裝配。

*為何收在這裡*：同一組「{nameof(RimeSetup)} ＋ Rime 日誌源 ＋ 按鍵處理器 ＋ SvcIme」
原本在 Windows 與 Android 各貼一份（Android 還有第二份），改一處漏三處。
宿主只要呼叫一次，剩下只有「本端的作業系統按鍵處理器」是各自不同的。

#Eg[
	```cs
	Svc.SetupRime();   // RimeSetup／IRimeLogSource／IImeKeyProcessor／ISvcIme
	```
]
""")]
public static class DiRime {
	[Doc($$"""
	註冊 Rime 引擎的默認實現。
	#Rtn[同一個 {nameof(IServiceCollection)}，便於串接。]
	""")]
	public static IServiceCollection SetupRime(
		this IServiceCollection z
	) {
		// 全域引擎：整個行程只有一個 librime 實例，故用 singleton。
		z.AddSingleton<RimeSetup>(_ => RimeSetup.Inst);
		// 日誌頁依賴抽象日誌源；實現留在 Rime 層，UI 只看介面。
		z.AddSingleton<IRimeLogSource>(_ => RimeLogStore.Inst);
		z.AddSingleton<IImeKeyProcessor, RimeKeyProcessor>();
		z.AddSingleton<ISvcIme, SvcIme>();
		return z;
	}
}
