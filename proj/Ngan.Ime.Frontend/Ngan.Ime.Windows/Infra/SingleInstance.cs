namespace Ngan.Ime.Windows.Infra;

using System;
using System.Threading;
using Ngan.Ime.Tsf.Ipc;
using Tsinswreng.CsCore;

[Doc($$"""
常駐服務端的單實例互斥。

*為何需要*：外殼在連線探測連續失敗六次之後，會在背景拉起服務端
（`NativeTsf.Pipe.cs`，照 Weasel 的 `_EnsureServerConnected`）。
服務端崩掉時，幾十個各自載入外殼的應用會同時判定「服務端不在」，於是同時去拉——
沒有互斥時會起來好幾支服務端，各自開一個 Rime 引擎，共用同一份 user data、
搶同一條管道名，按鍵依連線對象不同落到不同引擎，症狀是「候選時有時無」。

*照 Weasel 的做法*：`WeaselIPCServer/WeaselServerImpl.cpp` 的 `ServerImpl::Start`
先 `CreateMutex(NULL, FALSE, instanceName)`，`ERROR_ALREADY_EXISTS` 就 `return 0`
（第二個實例直接退出）。名稱帶使用者名，故同一台機器上不同使用者各有一支。

*為何是 `Local\`*：`Local\` 是這次登入工作階段的名稱空間，不需要 `SeCreateGlobalPrivilege`
（一般使用者建立 `Global\` 物件會失敗）。代價是：同一使用者若同時有多個工作階段
（例如 RDP 與本機），每個工作階段會各有一支服務端，這與 Weasel 的行為相同。
要跨工作階段只留一支，得改用 `Global\` 並確認權限，目前不做。

*持有到行程結束*：互斥一旦取得就存進 {{nameof(Held)}}，不主動釋放——
釋放等於允許第二支服務端起來，而「服務端還在跑」正是我們要阻止的事。
行程結束時由作業系統回收。
""")]
public static class SingleInstance{

	[Doc("本行程持有的互斥；null 表示沒有取得（或取得失敗後照常啟動）。")]
	static Mutex? Held;

	[Doc($$"""
	嘗試取得本安裝目錄的單實例所有權。
	#Rtn[true 表示可以繼續啟動；false 表示已經有一支服務端在跑，呼叫端應直接退出。]
	""")]
	public static bool TryAcquire(){
		var Name = MutexNameFor(AppContext.BaseDirectory);
		try{
			var Gate = new Mutex(initiallyOwned: false, Name, out var CreatedNew);
			if(!CreatedNew){
				// 已經有同名互斥：另一支服務端活著（或正在啟動）。
				Gate.Dispose();
				return false;
			}
			Held = Gate;
			return true;
		}catch(Exception Ex){
			// 取不到互斥（權限、名稱非法）時照常啟動：寧可有兩支服務端，
			// 也不要讓輸入法完全起不來。這種情況一定要留痕跡。
			Console.Error.WriteLine("[SingleInstance] 取得單實例互斥失敗，照常啟動：" + Ex.Message);
			return true;
		}
	}

	[Doc($$"""
	本安裝目錄的互斥名稱。
	#Prm[{{nameof(BaseDir)}}][服務端 exe 所在目錄。]
	#Rtn[形如 `Local\NganIme.Server.<8hex>` 的名稱；與管道名用同一份版本識別。]
	""")]
	public static str MutexNameFor(str BaseDir){
		// 與管道名用同一個鍵（identity.txt 的 clsid，沒有時退回固定前綴），
		// 故「同一版的外殼與服務端」與「不同版本各自一套」這兩個性質同時成立。
		var PipeName = TsfIpc.PipeNameFor(BaseDir);
		var PrefixLength = TsfIpc.PipeNamePrefix.Length;
		// 沒有 identity.txt 時管道名就是前綴本身（開發／手動註冊情境）：
		// 那種情況共用一個 `dev` 名稱，與管道名「共用固定名稱」的語義一致。
		var Suffix = PipeName.Length > PrefixLength + 1
			? PipeName[(PrefixLength + 1)..]
			: "dev";
		return "Local\\NganIme.Server." + Suffix;
	}
}
