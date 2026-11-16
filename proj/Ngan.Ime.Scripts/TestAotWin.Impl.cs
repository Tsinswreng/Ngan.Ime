using Tsinswreng.CsCore;
using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace Ngan.Ime.Scripts;

[Doc($$"""
TestAotWin 命令的流程實現；NativeAOT 開關由各專案自己的 csproj 提供。
""")]
internal static partial class TestAotWin{
	[Doc($$"""
	測試專案相對於倉庫根的路徑。
	""")]
	const str TestCsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Test/Ngan.Ime.Test.csproj";

	internal static async partial Task Main(ISCtx Ctx, CT Ct){
		var testProject = Ctx.RootDir/TestCsprojRelPath;

		// step 1: 以 Release 配置發布 win-x64 NativeAOT 測試 executable。
		await Exe("dotnet", ["publish", testProject, "-c", "Release", "-r", "win-x64"], Ct);

		// step 2: 運行 publish 目錄中的原生測試程式；目錄結構固定為 bin/Release/{Tfm}/{Rid}/publish。
		var publishExe = Ctx.RootDir/"proj/Ngan.Ime.Frontend/Ngan.Ime.Test/bin/Release"/Ctx.Tfm/"win-x64/publish/Ngan.Ime.Test.exe";
		await Exe(publishExe, [], Ct);
	}
}
