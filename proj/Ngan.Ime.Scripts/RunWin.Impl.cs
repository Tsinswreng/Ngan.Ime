using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace Ngan.Ime.Scripts;

[Doc($$"""
RunWin 的流程實現。
""")]
internal static partial class RunWin{

	[Doc($$"""
	Windows 服務端專案相對於倉庫根的路徑。
	""")]
	const str CsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Windows/Ngan.Ime.Windows.csproj";

	[Doc($$"""
	目標運行時識別碼；與本機 rime.dll 的字寬一致。
	""")]
	const str Rid = "win-x86";

	[Doc($$"""
	先建置再啟動；配置檔由 csproj 的 Content 項複製到輸出目錄。
	*為何不再自己複製配置*：`Ngan.Ime.Windows.csproj` 已把 `ExternalRsrc.Private/**`
	標成 `CopyToOutputDirectory=PreserveNewest`，`dotnet build`／`dotnet run`／`dotnet publish`
	三條路都會帶上。先前這裡另寫一份複製邏輯，與 `PubWinX86` 的那份重複，
	而且「先建置還是先複製」只能從呼叫序讀出來。
	
	*為何先 build 再 `run --no-build`*：`dotnet run` 會自己建置；分開做是為了讓
	「建置失敗」與「程式啟動後崩潰」在輸出上分得開，排查時不必猜是哪一段。
	""")]
	internal static async partial Task Main(ISCtx Ctx, CT Ct){
		// step 1: 建置（含把私有配置複製到輸出目錄）。
		await Exe("dotnet", ["build", Ctx.RootDir/CsprojRelPath, "-r", Rid], Ct);

		// step 2: 啟動服務端。輸出一律轉送終端，故 GUI 崩在啟動期時看得到原因。
		await Exe("dotnet", ["run", "--no-build", "--project", Ctx.RootDir/CsprojRelPath, "-r", Rid], Ct);
	}
}
