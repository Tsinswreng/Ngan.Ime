namespace Ngan.Ime.UI;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Ngan.Ime.Infra;
using Ngan.Ime.UI.Views;
using Tsinswreng.CsCore;

[Doc($$"""
Avalonia 應用入口。

*為何沒有 `App.axaml`*：本專案規定界面一律用純 C# 聲明式（`Avalonia.Markup.Declarative`），
主題與全域樣式因此也在代碼裡加：{{nameof(FluentTheme)}} ＋ `AppStyles`，
不再需要 `AvaloniaXamlLoader.Load(this)`。

*三種生命週期*：桌面（`IClassicDesktopStyleApplicationLifetime`，建主視窗）、
Android（`IActivityApplicationLifetime`，建主視圖交給 Activity）、
Browser／單視圖（`ISingleViewApplicationLifetime`）。三者共用 `MainView`。

*常駐模式*：TSF 服務端用 `--tsf` 啟動時不建主視窗（候選窗是另一個獨立視窗），
故 `ShutdownMode` 設為顯式退出，避免「沒有視窗就自動退出」。
""")]
public partial class App : Application{

	[Doc("載入主題與全域樣式；此處不建任何視窗。")]
	public override void Initialize(){
		Styles.Add(new FluentTheme());
		foreach(var One in AppStyles.Build()){
			Styles.Add(One);
		}
	}

	[Doc("依生命週期建立根視圖。")]
	public override void OnFrameworkInitializationCompleted(){
		if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime Desktop){
			// 常駐模式由容器裡的 OptHost 決定；設計器或沒有容器的預覽走預設（帶主視窗）。
			if(IsDaemonMode()){
				Desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
				return;
			}
			Desktop.MainWindow = new MainWindow();
		}else if(ApplicationLifetime is IActivityApplicationLifetime ActivityLifetime){
			ActivityLifetime.MainViewFactory = () => new MainView();
		}else if(ApplicationLifetime is ISingleViewApplicationLifetime SingleView){
			SingleView.MainView = new MainView();
		}
		base.OnFrameworkInitializationCompleted();
	}

	[Doc("讀取宿主選項；容器尚未建立或沒有註冊時回 false（照常建主視窗）。")]
	static bool IsDaemonMode(){
		return Di.SvcProviderOrNull?.GetService<OptHost>()?.DaemonMode ?? false;
	}
}
