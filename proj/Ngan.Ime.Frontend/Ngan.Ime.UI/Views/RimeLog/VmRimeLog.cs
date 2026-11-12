namespace Ngan.Ime.UI.Views.RimeLog;

using System.Collections.ObjectModel;
using Ngan.Ime.UI.Infra;
using Tsinswreng.CsCore;

[Doc($$"""
引擎日誌頁 ViewModel：直接投影 {{nameof(EngineLogBuffer.Lines)}}。

*為何只是轉發*：日誌的收集、限長、切線程都在 `EngineLogBuffer`；本類不重複一遍，
否則會出現「兩份上限、兩套切線程」的維護負擔。

#Eg[
	```cs
	var Vm = VmRimeLog.Mk();
	```
]
""")]
public partial class VmRimeLog : AppVmBase, IMk<VmRimeLog>{

	[Doc("日誌行集合；與 EngineLogBuffer 共用同一個實例。")]
	public ObservableCollection<str> Lines{get;}

	[Doc("建立並取得共享緩衝。")]
	public VmRimeLog(EngineLogBuffer Buffer){
		Lines = Buffer.Lines;
		base.Init();
	}

	[Doc("兜底建立：空集合，供設計器預覽。")]
	protected VmRimeLog(){
		Lines = [];
		base.Init();
	}

	[Doc("兜底建立。")]
	public static VmRimeLog Mk(){
		return new VmRimeLog();
	}
}
