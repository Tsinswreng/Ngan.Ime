namespace Ngan.Ime.Windows.Tsf;

using System;
using Ngan.Ime.Core.Ime;
using Tsinswreng.CsCore;

[Doc($$"""
TSF 管道服務端的契約。

*為何要介面*：`Program` 只需要「啟動」與「停止」，不需要知道管道怎麼開；
測試可以塞一個只記錄呼叫的替身，驗證啟動時機（例如重複啟動不該開兩條監聽）。

#Eg[
	```cs
	Svc.GetRequiredService<ISvcTsfPipeServer>().Start();
	```
]
""")]
public interface ISvcTsfPipeServer{

	[Doc("開始監聽；重複呼叫無副作用（已啟動時直接返回）。")]
	public void Start();

	[Doc("停止監聽並斷開現有連線；未啟動時無副作用。")]
	public void Stop();
}
