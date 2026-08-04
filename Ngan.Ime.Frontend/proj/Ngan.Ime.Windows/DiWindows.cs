using Ngan.Ime.Core.Keys;
using Ngan.Ime.Core.Infra.Log;
using Ngan.Ime.Rime;
using Ngan.Ime.UI;
using Microsoft.Extensions.DependencyInjection;

namespace Ngan.Ime.Windows;

public static class DiWindows{
	extension(ServiceCollection z){
		public ServiceCollection SetupWindows(){
	// 與實際 Windows 入口保持一致，避免切回輔助註冊入口時再次漏掉 Rime 日誌源。
	z.AddSingleton<IRimeLogSource>(_ => RimeLogStore.Inst);
	z.AddSingleton<
		IOsKeyProcessor
		, WindowsKeyProcessor
	>();

	z.AddSingleton<
		IImeKeyProcessor
		, RimeKeyProcessor
	>();

	z.AddSingleton<
		ISvcIme
		, SvcIme
	>();

	z.AddSingleton<RimeLogBuffer>();
	return z;
		}

	}
}
