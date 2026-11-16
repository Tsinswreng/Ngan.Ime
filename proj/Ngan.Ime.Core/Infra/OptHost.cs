namespace Ngan.Ime.Infra;


[Doc($$"""
宿主運行模式的選項。

*為何不是靜態布林*：先前用 `App.DaemonMode` 這個全域可變旗標，
由入口設定、被 DI 裝配與應用生命週期各自讀取。
它讓「這個行程是常駐還是帶主視窗」變成隱式的全域狀態，
既無法在測試裡切換，也讓裝配順序變成隱性契約。
現在由入口建立一份、註冊進容器，需要的人自己取。

*為何放在 Core*：它描述的是「宿主怎麼跑」，與界面框架無關；
`App`（UI）與 `DiWindows`（Windows）都要讀，Core 是兩者共同的下游。

#Eg[
	```cs
	Svc.AddSingleton(new OptHost{ DaemonMode = true });
	```
]
""")]
public sealed class OptHost{

	[Doc($$"""
	true 表示常駐模式：不建立主視窗，關閉視窗也不結束行程。

	TSF 服務端以 `--tsf` 啟動時就是這個模式；此時未處理的按鍵必須交回外殼，
	不能由宿主自己合成按鍵，否則同一顆鍵會在系統與引擎之間形成迴圈。
	""")]
	public bool DaemonMode{get;set;}
}
