using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace Ngan.Ime.Scripts;

/// RunWin 的流程實現。
internal static partial class RunWin{
	/// Windows 宿主專案相對於倉庫根的路徑。
	const str CsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Windows/Ngan.Ime.Windows.csproj";

	/// 宿主專案目錄相對於倉庫根的路徑（配置檔就在這裡）。
	const str ProjectDirRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Windows";

	/// 要跟隨執行檔一起放到輸出目錄的配置檔。
	/// 宿主的儲存區根目錄＝可執行檔所在目錄，故配置必須複製到 `bin/.../win-x86/`。
	static readonly str[] CfgFileNames = ["Ngan.Ime.Ro.jsonc", "Ngan.Ime.Rw.jsonc"];

	/// 目標運行時識別碼；與本機 rime.dll 的字寬一致。
	const str Rid = "win-x86";

	internal static async partial Task Main(ISCtx Ctx, CT Ct){
		var ProjectDir = Ctx.RootDir/ProjectDirRelPath;
		var OutDir = ProjectDir/"bin/Debug"/Ctx.Tfm/Rid;

		// step 1: 輸出目錄可能還沒建過（第一次跑或清過 bin），先確保它存在。
		Directory.CreateDirectory(OutDir);

		// step 2: 複製配置檔。覆蓋是刻意的：開發時改了專案目錄裡的配置，下一次啟動就該生效。
		foreach(var FileName in CfgFileNames){
			var Src = ProjectDir/FileName;
			if(!File.Exists(Src)){
				// 缺檔不致命的，但必須出聲；否則宿主會安靜地用鍵上默認值啟動。
				Console.Error.WriteLine($"[RunWin] 缺少配置檔，跳過複製：{Src}");
				continue;
			}
			File.Copy(Src, OutDir/FileName, overwrite: true);
		}

		// step 3: 啟動宿主。輸出一律轉送終端，故 GUI 崩在啟動期時看得到原因。
		await Exe("dotnet", ["run", "--project", Ctx.RootDir/CsprojRelPath, "-r", Rid], Ct);
	}
}
