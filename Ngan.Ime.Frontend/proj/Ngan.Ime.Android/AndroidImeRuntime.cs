using Android.OS;
using Android.Views.InputMethods;
using Tsinswreng.CsCore;

namespace Ngan.Ime.Android;


[Doc($$"""
Android 輸入法服務的執行期句柄：把「當前活著的服務」暴露給容器裡的服務使用。

*為何需要它*：{{nameof(AndroidOsKeyProcessor)}} 需要 `CurrentInputConnection`，
而那是 `InputMethodService` 的成員，只能在服務生命週期內取得；容器卻在 `Application` 期間就建好。
故用一個 singleton 當「可注入的取得器」，服務在 `OnCreate` 設定、`OnDestroy` 清空，
容器的註冊因此不必在執行期被改寫（舊版是把服務實例直接塞進 `ISvcIme.OsKeyProcessor` 屬性，
讓容器的註冊變成謊言）。

*線程*：`GetInputConnection` 可能在任意線程被呼叫；`Handler` 只在 UI 線程用。

#Eg[
	```cs
	AndroidImeRuntime.Inst.GetInputConnection = () => CurrentInputConnection;
	```
]
""")]
public sealed class AndroidImeRuntime{

	[Doc("全局唯一句柄；服務建立時設定、銷毀時清空。")]
	public static AndroidImeRuntime Inst{get;} = new();

	[Doc("取當前輸入連接；null 表示沒有活動的編輯框。")]
	public Func<IInputConnection?>? GetInputConnection{get;set;}

	[Doc("主線程 Handler；延遲建立以免在建構期就要求 Looper。")]
	public Handler MainHandler => field ??= new Handler(Looper.MainLooper!);

	[Doc("切回主線程執行。")]
	public void Post(Action Action){
		MainHandler.Post(Action);
	}

	[Doc("當前活著的輸入法服務；未建立時為 null。")]
	public WeakReference<ImeInputMethodService?> ServiceRef{get;} = new(null);

	[Doc("取當前服務；沒有活動服務時回 null。")]
	public ImeInputMethodService? GetService(){
		return ServiceRef.TryGetTarget(out var Service) ? Service : null;
	}

	[Doc("清空句柄；服務銷毀時呼叫。")]
	public void Clear(){
		GetInputConnection = null;
		ServiceRef.SetTarget(null);
	}
}
