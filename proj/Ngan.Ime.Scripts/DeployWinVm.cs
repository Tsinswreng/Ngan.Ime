namespace Ngan.Ime.Scripts;

using Tsinswreng.CsCore;
using Tsinswreng.CsSh;

[Doc($$"""
把 Windows 版一鍵部署到測試虛擬機的命令（宣告）。

為什麼是 CsSh 而不是一堆 .ps1：流程有八步、要做增量判斷、要解析日誌、要管版本化的 TSF DLL，
屬於「超過 16 行的複雜腳本」，按倉庫規範用 CsSh 寫；只有**必須在 guest 內執行**的部分
（排程工作、HKLM 註冊、以桌面使用者身分啟動）才以 PowerShell 腳本的形式丟進 VM 執行。

流程：確認 VM → 增量建置三個產物（TSF DLL／註冊工具／x86 服務端）→ 打包 → 複製進 guest →
執行 guest 端腳本（停舊服務端、解包、註冊、啟動、煙霧測試）→ 抓回日誌並回報。

用法：
  dotnet run --project proj/Ngan.Ime.Frontend/Ngan.Ime.Scripts -- DeployWinVm
  dotnet run --project proj/Ngan.Ime.Frontend/Ngan.Ime.Scripts -- DeployWinVm -Fast -OpenNotepad
  dotnet run --project proj/Ngan.Ime.Frontend/Ngan.Ime.Scripts -- DeployWinVm -DryRun
""")]
internal static partial class DeployWinVm{

	[Doc($$"""
	命令的完整流程：建置 → 部署 → 註冊 → 啟動 → 驗證。
	""")]
	internal static partial Task Main(ISCtx Ctx, CT Ct);

	[Doc($$"""
	解析命令列旗標；未知旗標直接報錯，避免「以為加了參數其實沒生效」。
	""")]
	private static partial WinVmOpts ParseOpts();

	[Doc($$"""
	確認 VM 已開機且 guest 操作通道可用；`-StartVm` 時會自動開機。
	""")]
	private static partial Task<nil> EnsureVmRunning(WinVmOpts O, CT Ct);

	[Doc($$"""
	依源碼時間戳決定哪些產物需要重新發布，並執行發布。
	""")]
	private static partial Task<nil> BuildArtifacts(WinVmOpts O, WinVmPaths P, CT Ct, bool DryRun);

	[Doc($$"""
	把發布輸出打包成 zip，並寫出 guest 端腳本與參數檔。
	""")]
	private static partial Task<nil> Stage(WinVmOpts O, WinVmPaths P, CT Ct, bool DryRun);

	[Doc($$"""
	建立本次部署的路徑集合（主機 staging 與 guest 收件位置）。
	""")]
	private static partial WinVmPaths MkPaths(ISCtx Ctx);

	[Doc($$"""
	產生 guest 端參數檔（key=value，避免在 vmrun 參數裡處理引號）。
	""")]
	private static partial str MkGuestArgs(WinVmOpts O);

	[Doc($$"""
	把 staging 檔案複製進 guest。
	""")]
	private static partial Task<nil> CopyToGuest(WinVmOpts O, WinVmPaths P, CT Ct);

	[Doc($$"""
	在 guest 內執行部署腳本（會阻塞到腳本結束）。
	""")]
	private static partial Task<nil> RunGuestDeploy(WinVmOpts O, WinVmPaths P, CT Ct);

	[Doc($$"""
	抓回 guest 的執行日誌與服務端日誌，印出尾段並決定退出碼。
	""")]
	private static partial Task<nil> CollectLogs(WinVmOpts O, WinVmPaths P, CT Ct);
}

[Doc($$"""
命令列選項；默認值對應目前這台 Win10 POC 虛擬機。
""")]
internal sealed partial class WinVmOpts{

	[Doc($$"""
	`vmrun.exe` 的完整路徑。
	""")]
	public str VmRunExe{get;set;} = @"D:\SOFT\VMware\VMware_Workstation17\vmrun.exe";

	[Doc($$"""
	虛擬機的 `.vmx` 路徑。
	""")]
	public str VmxPath{get;set;} = @"E:\_\VM\Win10\Windows10x64.vmx";

	[Doc($$"""
	guest 操作帳號（內建 Administrator 改名，可用於 HKLM 與排程工作）；
	可用環境變數 `NGAN_VM_GUEST_USER` 覆蓋。
	""")]
	public str GuestUser{get;set;} = Environment.GetEnvironmentVariable("NGAN_VM_GUEST_USER") ?? "Tsinswreng";

	[Doc($$"""
	guest 操作帳號的密碼。
	*不再寫死在版控裡*：改由環境變數 `NGAN_VM_GUEST_PASS` 提供；
	未設定時部署會直接停下並提示，避免測試機密碼隨倉庫流傳。
	""")]
	public str GuestPass{get;set;} = Environment.GetEnvironmentVariable("NGAN_VM_GUEST_PASS") ?? "";

	[Doc($$"""
	實際登入桌面的使用者；輸入法要裝進這個使用者的 CTF 清單。
	""")]
	public str DesktopUser{get;set;} = @"DESKTOP-VSSDIT1\22900";

	[Doc($$"""
	guest 端的工作根目錄。
	""")]
	public str WorkDir{get;set;} = @"C:\NganImeVm";

	[Doc($$"""
	服務端要載入的 rime.dll 在 guest 的位置。
	""")]
	public str RimeDll{get;set;} = @"C:\NganImeVm\rime\rime.dll";

	[Doc($$"""
	Rime 用戶資料在 guest 的位置。
	""")]
	public str RimeUserData{get;set;} = @"C:\NganImeVm\rime-user";

	[Doc($$"""
	首次部署時，若 guest 缺 `rime.dll`，從主機這個路徑複製。
	""")]
	public str RimeDllOnHost{get;set;} = @"D:\ENV\Rime\weasel-0.15.0\rime.dll";

	[Doc($$"""
	首次部署時，若 guest 缺用戶資料，從主機這個目錄複製。
	""")]
	public str RimeUserDataOnHost{get;set;} = @"D:\Program Files\Rime\User_Data";

	[Doc($$"""
	排程工作名前綴。
	""")]
	public str TaskPrefix{get;set;} = "NganIme";

	[Doc($$"""
	true 時跳過建置，直接用上次的發布輸出。
	""")]
	public bool NoBuild{get;set;}

	[Doc($$"""
	true 時服務端用 framework-dependent（快，但 guest 需要 x86 .NET 10 runtime）。
	""")]
	public bool Fast{get;set;}

	[Doc($$"""
	true 時自動開機 VM（`vmrun start … nogui`）。
	""")]
	public bool StartVm{get;set;}

	[Doc($$"""
	true 時部署完在桌面開一個記事本，方便立刻測試。
	""")]
	public bool OpenNotepad{get;set;}

	[Doc($$"""
	true 時連 explorer 一起重啟（保險，但桌面會閃一下）。
	""")]
	public bool RestartExplorer{get;set;}

	[Doc($$"""
	true 時跳過管道煙霧測試。
	""")]
	public bool SkipSmoke{get;set;}

	[Doc($$"""
	true 時只印步驟、不建置也不碰 VM。
	""")]
	public bool DryRun{get;set;}

	[Doc($$"""
	這次部署的 TSF DLL 版本標籤；避免覆寫被 explorer 鎖住的舊檔。
	""")]
	public str TsfVersion{get;set;} = DateTime.Now.ToString("yyyyMMddHHmmss");
}

[Doc($$"""
這次部署用到的所有路徑；集中一處，避免各步驟各自拼路徑。
""")]
internal sealed partial class WinVmPaths{

	[Doc($$"""
	倉庫根（`Ngan.Ime/`）；所有相對路徑都以它為基準，故命令可在任何工作目錄執行。
	""")]
	public Pth RepoRoot{get;set;} = default!;

	[Doc($$"""
	主機端的 staging 根目錄（不入版控）。
	""")]
	public Pth OutRoot{get;set;} = default!;

	[Doc($$"""
	TSF 的發布目錄。
	""")]
	public Pth TsfPublish{get;set;} = default!;

	[Doc($$"""
	註冊工具的發布目錄。
	""")]
	public Pth RegPublish{get;set;} = default!;

	[Doc($$"""
	服務端的發布目錄。
	""")]
	public Pth ServerPublish{get;set;} = default!;

	[Doc($$"""
	三個產物打包後的位置。
	""")]
	public Pth TsfZip{get;set;} = default!;

	[Doc($$"""
	註冊工具的 zip。
	""")]
	public Pth RegZip{get;set;} = default!;

	[Doc($$"""
	服務端的 zip。
	""")]
	public Pth ServerZip{get;set;} = default!;

	[Doc($$"""
	guest 端腳本與參數檔。
	""")]
	public Pth GuestScript{get;set;} = default!;

	[Doc($$"""
	guest 端參數檔。
	""")]
	public Pth GuestArgs{get;set;} = default!;

	[Doc($$"""
	抓回的執行日誌。
	""")]
	public Pth LastRunLog{get;set;} = default!;
}
