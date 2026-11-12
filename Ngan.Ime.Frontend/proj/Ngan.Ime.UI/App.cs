namespace Ngan.Ime.UI;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
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

*常駐模式*：TSF 宿主用 `--tsf` 啟動時不建主視窗（候選窗是另一個獨立視窗），
故 `ShutdownMode` 設為顯式退出，避免「沒有視窗就自動退出」。
""")]
public partial class App : Application{

	[Doc("TSF 常駐模式：不建立主視窗，關閉視窗也不會結束行程。")]
	public static bool DaemonMode{get;set;}

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
			if(DaemonMode){
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
}
