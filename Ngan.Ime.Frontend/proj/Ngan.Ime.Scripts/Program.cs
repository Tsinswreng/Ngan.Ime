namespace Ngan.Ime.Scripts;

using System.Runtime.CompilerServices;
using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

/// 子命令共用的執行上下文。
/// dispatcher 統一建立並填入項目固定事實，子命令直接取用，不各自解析路徑。
public interface ISCtx{
	/// 倉庫根目錄（`Ngan.Ime/`），各命令以此定位模組下的專案。
	public Pth RootDir{get;set;}
	/// 目標框架（TFM），與 Directory.Build.props 的 TargetFramework 同步；
	/// 決定構建/發布輸出目錄名（如 net10.0），升級 TFM 時此處需同步更新。
	public str Tfm{get;set;}
}

public class SCtx:ISCtx{
	public Pth RootDir{get;set;}
	/// 默認空串，dispatcher 建立時必定覆蓋，故不允許為 null。
	public str Tfm{get;set;} = "";
}

/// 維護腳本的命令列入口。
/// 第一個引數選擇具體腳本；腳本本身負責完整的一次性流程。
internal static partial class Program{
	/// 將命令列入口分派至具名腳本。
	internal static async Task Main(str[] Args){
		var Ct = default(CT);
		// 本檔位於 <根>/Ngan.Ime.Frontend/proj/Ngan.Ime.Scripts；上推三級得到倉庫根。
		var Root = FullPath(DirName(OwnPath())/"../../..");

		// step 1: 建立統一的子命令上下文，所有子命令都從中取得 RootDir、Tfm。
		ISCtx Ctx = new SCtx {
			RootDir = Root,
			// 與 Directory.Build.props 的 TargetFramework 同步；升級 TFM 時兩處一起改。
			Tfm = "net10.0",
		};

		if(Args.Length == 0){
			PrintUsage();
			return;
		}

		// step 2: 依首個引數分派；新命令在此與 PrintUsage 各加一行。
		switch(Args[0]){
			case nameof(Test):
				await Test.Main(Ctx, Ct);
				break;
			case nameof(TestAotWin):
				await TestAotWin.Main(Ctx, Ct);
				break;
			case nameof(GenAppJsonCtx):
				await GenAppJsonCtx.Main(Ctx, Ct);
				break;
			case nameof(RunWin):
				await RunWin.Main(Ctx, Ct);
				break;
			default:
				throw new ArgumentException($"Unknown Ngan.Ime script: {Args[0]}.", nameof(Args));
		}
	}

	/// 列出可由 dotnet run -- <entry> 呼叫的腳本名稱。
	private static void PrintUsage(){
		Console.Error.WriteLine("Usage: dotnet run --project Ngan.Ime.Frontend/proj/Ngan.Ime.Scripts -- <entry>");
		Console.Error.WriteLine("Entries: Test, TestAotWin, GenAppJsonCtx, RunWin");
		Console.Error.WriteLine("GenAppJsonCtx：改過 DTO／實體之後跑一次，重寫 Ngan.Ime.Core/Tsinswreng.SrcGen/Ngan.Ime.Gen.g.cs。");
		Console.Error.WriteLine("RunWin：把配置檔放到輸出目錄，再以 win-x86 啟動 Windows 宿主。");
	}

	/// 讓編譯器提供腳本源文件路徑，故腳本不依賴啟動時的當前目錄。
	private static str OwnPath([CallerFilePath] str CallerPath = ""){
		return CallerPath;
	}
}
