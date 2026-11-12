namespace Ngan.Ime.Rime;

using Tsinswreng.CsCore;

[Doc($$"""
在背景線程預先建立 {{nameof(RimeSetup)}}，避免第一次彈鍵盤時才同步初始化 librime。

*為何需要*：`RimeSetup` 建構子會載入動態庫、`initialize`、並 `join_maintenance_thread`
等待詞典部署，可能長達數秒；放在按鍵路徑上就是肉眼可見的卡頓。

*為何只有一份實現*：舊版這裡有一份、`SvcIme.ConnectAsy` 裡又手寫一份（`Task.Run` + 鎖 + 快取任務），
兩份各自維護快取與失敗處理。現在 `ConnectAsy` 直接 `await EnsureWarmAsy()`，
「預熱」與「連接」共用同一個任務，也不會重複初始化。

*失敗可重試*：失敗的任務不會被快取（下次呼叫重建）；同時 `SvcIme` 會呼叫 {{nameof(RimeSetup.Reset)}}。

#Eg[
	```cs
	_ = RimeWarmup.EnsureWarmAsy(); // 入口處即預熱
	```
]
""")]
public static class RimeWarmup {

	[Doc("任務快取的鎖。")]
	static readonly Lock Sync = new();

	[Doc("當前的預熱任務；失敗或取消後會被重建。")]
	static Task<nil>? WarmTask;

	[Doc("觸發一次可重入的後台預熱；多個入口重複呼叫只會初始化一次。")]
	public static Task<nil> EnsureWarmAsy(){
		lock(Sync){
			if(WarmTask is null || WarmTask.IsFaulted || WarmTask.IsCanceled){
				// 共享任務不綁定任何呼叫方的取消令牌：否則第一個呼叫方一取消，
				// 其他人等待的同一個任務就被連坐取消。
				WarmTask = Task.Run(WarmCoreAsy);
			}
			return WarmTask;
		}
	}

	[Doc("真正的預熱：構造單例。異常照常往外拋，由 EnsureWarmAsy 的快取判斷允許重試。")]
	static async Task<nil> WarmCoreAsy(){
		// 明確切到線程池：建構子是阻塞的，若直接在此方法內同步執行，
		// 第一個呼叫方（可能是 UI 線程）仍會被阻塞。
		await Task.Run(() => {
			_ = RimeSetup.Inst;
		});
		AppLog.Info("[Rime] 預熱完成");
		return NIL;
	}
}
