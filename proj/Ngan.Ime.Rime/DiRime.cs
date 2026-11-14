namespace Ngan.Ime.Rime;

using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCore;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;

[Doc($$"""
Rime 適配層的依賴注入裝配。

*為何收在這裡*：同一組「{{nameof(RimeSetup)}} ＋ 引擎日誌源 ＋ 按鍵處理器 ＋ {{nameof(SvcIme)}}」
原本在 Windows 與 Android 各貼一份（Android 還有第二份），改一處漏三處。
服務端只要呼叫一次，剩下只有「本端的作業系統按鍵處理器」是各自不同的。

*為何用 Func<RimeSetup> 而不是直接注入 {{nameof(RimeSetup)}}*：它的建構子會阻塞數秒做
librime 初始化；直接注入等於「解析服務 = 阻塞當前線程」。用工廠把初始化推遲到
{{nameof(SvcIme.ConnectAsy)}}／`RimeWarmup` 真的需要它的時候。

*同一個實例註冊四個寬度*：具體型別給需要 `RimeSetup` 細節的地方，
另外三個介面給界面層各自只拿需要的能力（見 `IImeState`／`IImeInput`／`IImeOptions`）。

#Eg[
	```cs
	Svc.SetupRime();   // RimeSetup／IEngineLogSource／IImeKeyProcessor／SvcIme＋三個窄介面
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
		// 全域引擎：整個行程只有一個 librime 實例；工廠只是把初始化推遲，不改變唯一性。
		z.AddSingleton<Func<RimeSetup>>(_ => () => RimeSetup.Inst);
		// 日誌頁依賴引擎中立的抽象；實現留在 Rime 層，界面只看介面。
		z.AddSingleton<IEngineLogSource>(_ => RimeLogStore.Inst);
		z.AddSingleton<IImeKeyProcessor, RimeKeyProcessor>();
		// SvcIme 同時是狀態、入口與選項；按能力拆成三個介面，避免介面隔離原則被破壞。
		z.AddSingleton<SvcIme>();
		z.AddSingleton<IImeState>(Svc => Svc.GetRequiredService<SvcIme>());
		z.AddSingleton<IImeInput>(Svc => Svc.GetRequiredService<SvcIme>());
		z.AddSingleton<IImeOptions>(Svc => Svc.GetRequiredService<SvcIme>());
		return z;
	}
}
