namespace Ngan.Ime.Scripts;

using Tsinswreng.CsSh;

/// 發布 Windows 版的完整安裝包：所有執行時產物放在同一個目錄，整包拷走即可安裝。
///
/// *與 DeployWinVm 的分工*：DeployWinVm 是「改完代碼 → 丟進測試 VM 試」；
/// 本命令只產出可交付的目錄，不碰任何虛擬機。
///
/// *為何是「同一目錄」而不是分子目錄*：Windows 版運行時是兩個位數的組合，
/// 外殼（x64，載入應用行程）與服務端（x86，載入 rime.dll）必須成對出現；
/// 安裝腳本、註冊工具、啟動器也都指向同一個目錄。分成 shell/server/reg 三層
/// 只會讓「整包拷走」變成「先想清楚誰在哪」，安裝腳本還得處理相對路徑。
///
/// *產物*：
/// - `Ngan.Ime.Tsf.dll`：TSF 外殼，x64 NativeAOT；
/// - `Ngan.Ime.Windows.exe` 與其執行時依賴：服務端，x86 NativeAOT；
/// - `Ngan.Ime.Tsf.Registration.exe`：註冊工具，x64 框架依賴（需 .NET 10 桌面執行時）；
/// - `install.ps1`／`install-user.cmd`／`uninstall.ps1`／`StartServer.vbs`／`README.txt`。
///
/// 用法：
/// ```sh
/// dotnet run --project proj/Ngan.Ime.Scripts -- PubWin
/// dotnet run --project proj/Ngan.Ime.Scripts -- PubWin -Out D:\tmp\ngan
/// dotnet run --project proj/Ngan.Ime.Scripts -- PubWin -KeepSymbols
/// ```
internal static partial class PubWinX86{

	/// 執行：發布三個產物、攤平到同一目錄、補安裝腳本、印出結果。
	internal static partial Task Main(ISCtx Ctx, CT Ct);

	/// 解析命令列旗標；未知旗標直接報錯，避免「以為加了參數其實沒生效」。
	private static partial PubOpts ParseOpts();

	/// 發布三個產物到 staging。
	private static partial Task<nil> Publish(ISCtx Ctx, PubOpts Opts, CT Ct);

	/// 把 staging 的檔案攤平進輸出目錄（同一層，不保留子目錄）。
	private static partial Task<nil> Flatten(PubOpts Opts);

	/// 複製安裝腳本與說明。
	private static partial Task<nil> CopyPack(ISCtx Ctx, PubOpts Opts);

	/// 剝離 .pdb；發布目錄是給人拷走的，偵錯符號對使用者沒有用。
	private static partial Task<nil> StripSymbols(PubOpts Opts);

	/// 檢查關鍵產物存在並印出目錄大小；缺檔視為失敗。
	private static partial Task<nil> Verify(PubOpts Opts);
}

/// 本命令的選項。
internal sealed partial class PubOpts{

	/// 輸出目錄；空字串表示用預設位置（`artifacts/publish/win`）。
	public str OutDir{get;set;} = "";

	/// true 表示目錄是使用者用 `-Out` 指定的。
	public bool OutDirExplicit{get;set;}

	/// true 時保留 .pdb。
	public bool KeepSymbols{get;set;}

	/// true 時只印步驟、不真的發布。
	public bool DryRun{get;set;}
}
