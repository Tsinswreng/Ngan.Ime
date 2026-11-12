namespace Ngan.Ime.UI.Infra;

using CommunityToolkit.Mvvm.ComponentModel;
using Tsinswreng.CsCore;

[Doc($$"""
所有 ViewModel 的基類。

*為何要 `Init()`*：DI 建構子最後呼叫 `base.Init()` 標記「依賴都到位了」；
需要「依賴齊全後才算初始化完成」的邏輯（例如啟動一次背景載入）應該在 `Init()` 之後由視圖觸發，
而不是塞在建構子裡——建構子有副作用是先前 View 洩漏與「造對象＝啟動系統」的根源。

*非同步一律走 `Fire`*：`Fire` 會把例外交給 `HandleErr`，不會產生無人觀察的例外；
事件處理器（void）與生命週期回調都應改呼叫 `Fire(...)`。

#Eg[
	```cs
	public partial void Reload(){ Fire(LoadAsy(default)); }
	```
]
""")]
public abstract partial class AppVmBase : ObservableObject{

	[Doc("依賴是否已由建構子填入並完成初始化。")]
	public bool IsInitialized{get; private set;}

	[Doc("由 DI 建構子最後呼叫；重複呼叫無副作用。")]
	protected void Init(){
		IsInitialized = true;
	}

	[Doc($$"""
	統一的錯誤出口：寫日誌並保留堆棧。界面若需要提示使用者，由呼叫方另行設定狀態文字。
	#Prm[{{nameof(Ex)}}][例外。]
	""")]
	public void HandleErr(Exception Ex){
		AppLog.Error(Ex, "[" + GetType().Name + "] 操作失敗");
	}

	[Doc($$"""
	啟動一條非同步工作並觀察它的例外；呼叫方不等待。
	#Prm[{{nameof(Task)}}][要啟動的工作。]
	""")]
	protected void Fire(Task Task){
		_ = GuardAsy(Task);
	}

	[Doc("等待工作完成；失敗時交給 HandleErr。")]
	async Task GuardAsy(Task Task){
		try{
			await Task;
		}catch(Exception Ex){
			HandleErr(Ex);
		}
	}
}
