namespace Ngan.Ime.UI;

using System.Collections.ObjectModel;
using Avalonia.Threading;
using Ngan.Ime.Infra.Log;
using Tsinswreng.CsCore;

[Doc($$"""
界面側的引擎日誌緩衝：把 {{nameof(IEngineLogSource)}} 的事件搬成可綁定的集合。

*為何不直接綁引擎的存儲*：引擎存儲在引擎層（可能是任意線程寫入），
而 `ObservableCollection` 只能在 UI 線程改；本類負責「切線程 + 限長」。

*與引擎存儲的關係*：引擎存儲保留 200 行供回放；本類只鏡像同一份內容給當前界面，
上限相同，故不會比引擎那份多。

#Eg[
	```cs
	foreach(var Line in Buffer.Lines){ /* 顯示 */ }
	```
]
""")]
public class EngineLogBuffer : AppVmBase, IMk<EngineLogBuffer>, IDisposable{

	[Doc("最多保留的行數；與引擎側一致。")]
	public const i32 MaxLines = 200;

	[Doc("引擎日誌源；null 表示本宿主沒有引擎（骨架宿主）。")]
	readonly IEngineLogSource? Source;

	[Doc("日誌行；只在 UI 線程修改。")]
	public ObservableCollection<str> Lines{get;} = [];

	[Doc("新日誌事件處理器；Dispose 時解除。")]
	readonly EventHandler<EngineLogEntryEventArgs>? OnLogAppended;

	[Doc("無引擎的實例：集合為空，供設計器與測試使用。")]
	protected EngineLogBuffer(){
		base.Init();
	}

	[Doc("建立緩衝並回放既有日誌。")]
	public EngineLogBuffer(IEngineLogSource Source){
		this.Source = Source;
		foreach(var Line in Source.Snapshot()){
			Lines.Add(Line);
		}
		OnLogAppended = (_, Args) => Append(Args.FormattedLine);
		Source.OnLogAppended += OnLogAppended;
		base.Init();
	}

	[Doc("兜底建立：無依賴空實例。")]
	public static EngineLogBuffer Mk(){
		return new EngineLogBuffer();
	}

	[Doc("追加一行：切回 UI 線程並維持上限。")]
	public void Append(str Line){
		if(!Dispatcher.UIThread.CheckAccess()){
			Dispatcher.UIThread.Post(() => Append(Line));
			return;
		}
		Lines.Add(Line);
		while(Lines.Count > MaxLines){
			Lines.RemoveAt(0);
		}
	}

	[Doc("解除引擎日誌訂閱。")]
	public void Dispose(){
		if(Source is not null && OnLogAppended is not null){
			Source.OnLogAppended -= OnLogAppended;
		}
		GC.SuppressFinalize(this);
	}
}
