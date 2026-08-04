using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Ngan.Ime.ViewModels;
using Ngan.Ime.UI.Views;
using Avalonia.Markup.Xaml;

namespace Ngan.Ime.UI;

public partial class App : Application
{
	/// 初始化 Ngan.Ime 的全局控件樣式。
	///
	/// 當前主要統一 Button 的普通背景與鼠標懸停邊框：普通狀態保持穩定深色背景，
	/// 懸停時僅以 `UiCfg.MainColor` 突出邊框，避免 FluentTheme 把按鈕內容區改成突兀的黑色。
	/// 具體樣式實現獨立放在 `App.Style.Impl.cs`，避免入口生命週期與視覺規則混雜。
	private partial void InitStyle();

	public override void Initialize(){
		AvaloniaXamlLoader.Load(this);
		InitStyle();
	}

	public override void OnFrameworkInitializationCompleted(){
		if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){
// #if DEBUG
// 			this.AttachDeveloperTools();
// #endif
			// Windows 桌面端保持標準系統窗口，保留標題欄與拖動能力。
			desktop.MainWindow = new MainWindow{
				DataContext = new MainViewModel(),
				Title = "Ngan.Ime",
				Width = 1920/4,
				MinWidth = 0,
				Height = 1080/4,
				MinHeight = 0,
				WindowDecorations = WindowDecorations.Full,
				ExtendClientAreaToDecorationsHint = false
			};
		}
		else if(ApplicationLifetime is IActivityApplicationLifetime activityLifetime){
			activityLifetime.MainViewFactory = () => new MainView{
				DataContext = new MainViewModel()
			};
		}
		else if(ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform){
			singleViewPlatform.MainView = new MainView{
				DataContext = new MainViewModel()
			};
		}
		base.OnFrameworkInitializationCompleted();
	}
}
