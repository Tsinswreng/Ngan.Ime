using System;
using Ngan.Ime.UI;
using Avalonia;
using Avalonia.Media;

namespace Ngan.Ime.Desktop;

sealed class Program {
	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	public static void Main(string[] args) => BuildAvaloniaApp()
		.StartWithClassicDesktopLifetime(args);

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp() {
		var options = new FontManagerOptions {
			DefaultFamilyName = "孤鹜 筑紫明朝",  // 默认字体
			FontFallbacks = new[] { new FontFallback { FontFamily = "Microsoft YaHei" } } // 备选字体
		};
		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			//.WithInterFont()
			.With(options)
			.LogToTrace();
	}

}
