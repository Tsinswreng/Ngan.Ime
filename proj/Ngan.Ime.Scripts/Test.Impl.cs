using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace Ngan.Ime.Scripts;

[Doc($$"""
Test 命令的流程實現。
""")]
internal static partial class Test{
	[Doc($$"""
	測試專案相對於倉庫根的路徑。
	""")]
	const str TestCsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Test/Ngan.Ime.Test.csproj";

	internal static async partial Task Main(ISCtx Ctx, CT Ct){
		// dotnet run 會自動還原並建置；輸出必須轉送終端，否則看不到測試結果。
		await Exe("dotnet", ["run", "--project", Ctx.RootDir/TestCsprojRelPath], Ct);
	}
}
