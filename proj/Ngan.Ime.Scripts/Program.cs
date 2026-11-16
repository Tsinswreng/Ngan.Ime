namespace Ngan.Ime.Scripts;

using System.Runtime.CompilerServices;
using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

[Doc($$"""
子命令共用的執行上下文。
dispatcher 統一建立並填入項目固定事實，子命令直接取用，不各自解析路徑。
""")]
public interface ISCtx{
	[Doc($$"""
	倉庫根目錄（`Ngan.Ime/`），各命令以此定位模組下的專案。
	""")]
	public Pth RootDir{get;set;}
	[Doc($$"""
	目標框架（TFM），與 Directory.Build.props 的 TargetFramework 同步；
	決定構建/發布輸出目錄名（如 net10.0），升級 TFM 時此處需同步更新。
	""")]
	public str Tfm{get;set;}
}

public class SCtx:ISCtx{
	public Pth RootDir{get;set;}
	[Doc($$"""
	默認空串，dispatcher 建立時必定覆蓋，故不允許為 null。
	""")]
	public str Tfm{get;set;} = "";
}

[Doc($$"""
維護腳本的命令列入口。
第一個引數選擇具體腳本；腳本本身負責完整的一次性流程。
""")]
internal static partial class Program{
	[Doc($$"""
	將命令列入口分派至具名腳本。
	""")]
	internal static async Task Main(str[] Args){
		var ct = default(CT);
		// 本檔位於 <根>/proj/Ngan.Ime.Scripts；上推兩級得到倉庫根（Ngan.Ime.Scripts → proj → Ngan.Ime）。
		var root = FullPath(DirName(OwnPath())/"../..");

		// step 1: 建立統一的子命令上下文，所有子命令都從中取得 RootDir、Tfm。
		ISCtx Ctx = new SCtx {
			RootDir = root,
			// 與 Directory.Build.props 的 TargetFramework 同步；升級 TFM 時兩處一起改。
			Tfm = "net10.0",
		};

		if(Args.Length == 0){
			PrintUsage();
			return;
		}

		// step 2: 依首個引數分派；新命令在此與 PrintUsage 各加一行。
		switch(Args[0]){
			case nameof(ProtoInfo):
				await ProtoInfo.Main(Ctx, ct);
				break;
			case nameof(Test):
				await Test.Main(Ctx, ct);
				break;
			case nameof(TestAotWin):
				await TestAotWin.Main(Ctx, ct);
				break;
			case nameof(GenAppJsonCtx):
				await GenAppJsonCtx.Main(Ctx, ct);
				break;
			case nameof(RunWin):
				await RunWin.Main(Ctx, ct);
				break;
			case nameof(DeployWinVm):
				await DeployWinVm.Main(Ctx, ct);
				break;
			case nameof(PubWinX86):
				await PubWinX86.Main(Ctx, ct);
				break;
			// 舊名／別名：使用者與文檔兩邊都有人叫，兩個都認，免得再撞一次。
			case "PubWin":
				await PubWinX86.Main(Ctx, ct);
				break;
			case nameof(CleanWin):
				await CleanWin.Main(Ctx, ct);
				break;
			default:
				throw new ArgumentException($"Unknown Ngan.Ime script: {Args[0]}.", nameof(Args));
		}
	}

	[Doc($$"""
	列出可由 dotnet run -- <entry> 呼叫的腳本名稱。
	""")]
	private static void PrintUsage(){
		Console.Error.WriteLine("Usage: dotnet run --project proj/Ngan.Ime.Frontend/Ngan.Ime.Scripts -- <entry>");
		Console.Error.WriteLine("Entries: ProtoInfo, Test, TestAotWin, GenAppJsonCtx, RunWin, DeployWinVm, PubWinX86（別名 PubWin）, CleanWin");
		Console.Error.WriteLine("ProtoInfo：印出協議版本、管道前綴與請求／回應樣本（key=value）；可加 -Dir <目錄> 印該目錄的管道名。");
		Console.Error.WriteLine("GenAppJsonCtx：改過 DTO／實體之後跑一次，重寫 Ngan.Ime.Core/Tsinswreng.SrcGen/Ngan.Ime.Gen.g.cs。");
		Console.Error.WriteLine("RunWin：把配置檔放到輸出目錄，再以 win-x86 啟動 Windows 服務端。");
		Console.Error.WriteLine("DeployWinVm：建置並部署 Windows 版到測試虛擬機（-Fast／-NoBuild／-StartVm／-OpenNotepad／-SkipSmoke／-DryRun）。");
		Console.Error.WriteLine("PubWinX86（別名 PubWin）：發布 Windows 安裝包（外殼 x64＋服務端 x86＋註冊工具＋安裝腳本，同一目錄；-Out／-KeepSymbols／-DryRun）。");
	}

	[Doc($$"""
	讓編譯器提供腳本源文件路徑，故腳本不依賴啟動時的當前目錄。
	""")]
	private static str OwnPath([CallerFilePath] str CallerPath = ""){
		return CallerPath;
	}
}
