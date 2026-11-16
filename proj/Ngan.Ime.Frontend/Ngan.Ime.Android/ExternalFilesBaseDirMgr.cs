namespace Ngan.Ime.Android;

using Ngan.Ime.Infra.Storage;

[Doc($$"""
Android 的應用儲存區根目錄：應用的外部私有目錄。

*為何用它*：`GetExternalFilesDir` 給的是 `外部儲存/Android/data/包名/files`，
寫入不需要任何權限，且應用被卸載時由系統一併清理，
不會在別人的手機上留下垃圾目錄——這是 Android 上放日誌／緩存／下載的事實標準。

*Context 從哪來*：由 {{nameof(Application)}} 在建構子注入，而不是自己去取靜態的全局 Context。
理由與 {{nameof(Application)}} 的註釋一致：那個 Application 實例本來就是現成的 Context，
再去找一個靜態全域的等於多一個不必要的全局狀態。

#Eg[
	```cs
	// 1: Application 建構子把 Context 交進來（早於建立任何視圖）。
	ExternalFilesBaseDirMgr.Inst._Ctx = this;

	// 2: 入口處註冊，之後需要寫檔的元件向容器取根目錄即可，不必自己碰 Android API。
	Svc.AddSingleton<I_GetBaseDir>(ExternalFilesBaseDirMgr.Inst);
	```
]
""")]
public class ExternalFilesBaseDirMgr : I_GetBaseDir {
	[Doc("全局單例；Android 入口註冊進容器時用的就是它。")]
	public static ExternalFilesBaseDirMgr Inst => field ??= new ExternalFilesBaseDirMgr();

	[Doc("Android 的 Context，由 Application 建構子注入；未注入時取根目錄會拋出。")]
	public global::Android.Content.Context? _Ctx { get; set; }

	[Doc($$"""
	取根目錄：每次呼叫都重新問系統，因為外部儲存可能被拔出或重新掛載，快取會拿到失效路徑。
	#Rtn[`GetExternalFilesDir(null)` 的絕對路徑。]
	#Throw[{{nameof(TypedErr)}}][未注入 Context（KeysErr.Storage.CtxNotInjected），或系統拒絕提供目錄（KeysErr.Storage.DirUnavailable）。]
	""")]
	public str GetBaseDir() {
		// 1: 沒有 Context 就無從查詢儲存位置；這是啟動順序寫錯，不是執行期可恢復的狀態。
		var Ctx = _Ctx ?? throw KeysErr.Storage.CtxNotInjected.ToErr();
		// 2: 問系統要外部私有目錄；null 代表目前沒有可用的外部儲存。
		var Dir = Ctx.GetExternalFilesDir(null)?.AbsolutePath;
		if (Dir is null) {
			throw KeysErr.Storage.DirUnavailable.ToErr();
		}
		return Dir;
	}
}
