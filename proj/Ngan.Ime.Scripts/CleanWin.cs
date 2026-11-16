namespace Ngan.Ime.Scripts;

using Tsinswreng.CsCore;
using Tsinswreng.CsSh;

[Doc($$"""
清理 `%LOCALAPPDATA%\NganIme\v*` 裏已經沒有人在用的舊版本目錄。

*為何需要*：TSF 外殼是行程載入的 DLL，安裝只能靠「換新路徑」即時生效；
舊版本目錄會一直累積。跑着的行程不放手就刪不掉，所以清理要由這個命令做。

*「在用」的判據有三個*，任一成立就保留：
+ 有任何行程從該目錄載入了模組（外殼 DLL 或服務端 exe）；
+ 註冊表裏有任何 CLSID 的 `InprocServer32` 指向該目錄（還註冊着，就是還在用）；
+ 有對應的排程工作（`NganIme-Server-v<版本>`，或舊版的單一工作 `NganIme-Server`）。

*為何不硬編碼 CLSID*：版本化安裝之後每個版本有自己的 CLSID，先前寫死一個 POC 期的值，
會把「目前正在註冊的那一版」當成可刪的舊版，刪掉之後 HKLM 指向不存在的 DLL，
輸入法直接失效。現在改成掃描整個 CLSID 樹。

*`-UninstallOld`*：預設只刪「已經三不管」的目錄；還註冊着的版本會被保留，
於是每次安裝都留一個輸入法條目與一個會自動啟動的服務端。
加上這個旗標就會對「除了最新版以外的每一版」反註冊（呼叫該版本自己的註冊工具）、
移除它的排程工作，然後才刪目錄。反註冊要管理員（HKLM），沒提權時會明確告訴你哪一步沒做。

用法：
```sh
dotnet run --project proj/Ngan.Ime.Scripts -- CleanWin
dotnet run --project proj/Ngan.Ime.Scripts -- CleanWin -DryRun
dotnet run --project proj/Ngan.Ime.Scripts -- CleanWin -UninstallOld
```
""")]
internal static partial class CleanWin{

	[Doc($$"""
	執行：掃描、（可選）反註冊舊版、刪除、報告。
	""")]
	internal static partial Task<nil> Main(ISCtx Ctx, CT Ct);

	[Doc($$"""
	解析旗標。
	""")]
	private static partial CleanOpts ParseOpts();

	[Doc($$"""
	找出某個目錄下是否有行程載入了檔案；有就回傳「行程名（pid）」清單。
	""")]
	private static partial List<str> FindHolders(str Dir);

	[Doc($$"""
	列出所有版本目錄。
	""")]
	private static partial List<str> ListVersionDirs(str Root);

	[Doc($$"""
	讀註冊表，取得所有指向 `%LOCALAPPDATA%\NganIme` 底下的外殼路徑（依版本目錄分組）。
	""")]
	private static partial Dictionary<str, List<str>> ListRegisteredShells(str Root);

	[Doc($$"""
	這個版本目錄有沒有對應的排程工作（含舊版的單一工作 `NganIme-Server`）。
	""")]
	private static partial bool HasScheduledTask(str Dir, str LegacyTaskText);

	[Doc($$"""
	呼叫該版本自己的註冊工具，把它從使用者清單與機器層註冊中移除。
	""")]
	private static partial bool UnregisterVersion(str Dir);

	[Doc($$"""
	移除該版本的排程工作（新版名稱與舊版單一工作都處理）。
	""")]
	private static partial void RemoveScheduledTasks(str Dir, str LegacyTaskText);

	[Doc($$"""
	查舊版單一工作 `NganIme-Server` 的內容；不存在時回空串。
	""")]
	private static partial str QueryLegacyTask();
}

[Doc($$"""
本命令的選項。
""")]
internal sealed partial class CleanOpts{

	[Doc($$"""
	true 時只印結果、不刪除。
	""")]
	public bool DryRun{get;set;}

	[Doc($$"""
	true 時對「除了最新版以外的每一版」反註冊並移除排程工作，之後才刪目錄。
	""")]
	public bool UninstallOld{get;set;}
}
