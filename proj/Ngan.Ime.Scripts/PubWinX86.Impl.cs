using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace Ngan.Ime.Scripts;

/// PubWin 的流程實現。
internal static partial class PubWinX86{

	/// TSF 外殼專案（x64，被應用行程載入）。
	const str TsfCsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Tsf.Windows/Ngan.Ime.Tsf.Windows.csproj";

	/// 服務端專案（x86，載入 rime.dll）。
	const str ServerCsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Windows/Ngan.Ime.Windows.csproj";

	/// 註冊工具專案（x64，框架依賴）。
	const str RegCsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Tsf.Registration/Ngan.Ime.Tsf.Registration.csproj";

	/// 安裝腳本與說明的來源目錄。
	const str PackRelPath = "scripts/win-vm/pack";

	/// 預設輸出目錄（相對倉庫根）；`artifacts/` 已在 .gitignore 裡。
	const str DfltOutRelPath = "artifacts/publish/win";

	/// 帶時間戳發布時的根目錄（相對倉庫根）；`current.txt` 也放在這裡。
	const str PublishRootRelPath = "artifacts/publish";

	/// 三個產物的暫存目錄（相對輸出目錄）；攤平後刪掉。
	const str StageRelPath = "_stage";

	internal static async partial Task Main(ISCtx Ctx, CT Ct){
		var Opts = ParseOpts();
		if(Opts.OutDir.Length == 0){
			// 帶時間戳發布：每次都是新目錄，不會去刪使用中的舊目錄。
			Opts.OutDir = Opts.Snapshot
				? Ctx.RootDir/PublishRootRelPath/("win-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"))
				: Ctx.RootDir/DfltOutRelPath;
		}

		Console.WriteLine("[PubWinX86] 本次設定：");
		Console.WriteLine($"  輸出目錄: {Opts.OutDir}");
		Console.WriteLine("  內容    : 外殼 x64 + 服務端 x86 + 註冊工具 + 安裝腳本（同一層）");

		if(Opts.DryRun){
			Console.WriteLine("[PubWinX86] -DryRun：將發布三個專案並攤平到輸出目錄，不執行。");
			return;
		}

		// 整包重建：輸出目錄是產物，留著上一輪的檔案只會混淆。
		if(Directory.Exists(Opts.OutDir)){
			Directory.Delete(Opts.OutDir, recursive: true);
		}
		Directory.CreateDirectory(Opts.OutDir);

		await Publish(Ctx, Opts, Ct);
		await Flatten(Opts);
		await CopyPack(Ctx, Opts);
		await StripSymbols(Opts);
		await Verify(Opts);
		if(Opts.Snapshot){
			await WriteCurrentPointer(Opts);
		}
	}

	private static partial PubOpts ParseOpts(){
		var Opts = new PubOpts();
		var Args = Environment.GetCommandLineArgs().Skip(1).ToList();
		for(var I = 0; I < Args.Count; I++){
			var Arg = Args[I];
			if(!Arg.StartsWith("-", StringComparison.Ordinal)){
				continue;
			}
			switch(Arg.TrimStart('-').ToLowerInvariant()){
				case "out":
					if(I + 1 >= Args.Count){
						throw new ArgumentException("-Out 需要一個目錄參數。");
					}
					Opts.OutDir = Args[++I];
					Opts.OutDirExplicit = true;
					break;
				case "keepsymbols":
					Opts.KeepSymbols = true;
					break;
				case "snap":
				case "timestamp":
					Opts.Snapshot = true;
					break;
				case "dryrun":
					Opts.DryRun = true;
					break;
				case "help":
				case "h":
					Console.Error.WriteLine("用法: dotnet run --project proj/Ngan.Ime.Scripts -- PubWinX86 [-Snap] [-Out <目錄>] [-KeepSymbols] [-DryRun]");
					Environment.Exit(0);
					break;
				default:
					throw new ArgumentException($"未知選項：{Arg}。可用：-Snap -Out -KeepSymbols -DryRun");
			}
		}
		return Opts;
	}

	private static async partial Task<nil> Publish(ISCtx Ctx, PubOpts Opts, CT Ct){
		var Stage = Opts.OutDir/StageRelPath;

		// 外殼：x64 NativeAOT，產物只有一個 DLL。
		var ShellOut = Stage/"shell";
		Mkdir(ShellOut);
		Console.WriteLine("[PubWinX86] 發布外殼（win-x64, NativeAOT）…");
		await Exe("dotnet", ["publish", (string)(Ctx.RootDir/TsfCsprojRelPath), "-c", "Release", "-r", "win-x64", "-o", (string)ShellOut], Ct);

		// 服務端：x86 NativeAOT，要跟它自己的原生依賴一起攤平。
		var ServerOut = Stage/"server";
		Mkdir(ServerOut);
		Console.WriteLine("[PubWinX86] 發布服務端（win-x86, NativeAOT）…");
		await Exe("dotnet", ["publish", (string)(Ctx.RootDir/ServerCsprojRelPath), "-c", "Release", "-r", "win-x86", "-o", (string)ServerOut], Ct);

		// 註冊工具：x64 框架依賴。它不是性能路徑，沒必要 AOT；且 AOT 會把它自己變成 20 MB。
		var RegOut = Stage/"reg";
		Mkdir(RegOut);
		Console.WriteLine("[PubWinX86] 發布註冊工具（win-x64, framework-dependent）…");
		await Exe("dotnet", [
			"publish", (string)(Ctx.RootDir/RegCsprojRelPath), "-c", "Release", "-r", "win-x64",
			"--self-contained", "false", "-p:PublishAot=false", "-o", (string)RegOut,
		], Ct);
		return NIL;
	}

	private static partial Task<nil> Flatten(PubOpts Opts){
		var Stage = Opts.OutDir/StageRelPath;
		if(!Directory.Exists(Stage)){
			throw new InvalidOperationException($"找不到暫存目錄：{Stage}");
		}
		foreach(var One in Directory.EnumerateFiles(Stage, "*", SearchOption.AllDirectories)){
			var Name = Path.GetFileName(One);
			var Dest = Opts.OutDir/Name;
			if(File.Exists(Dest)){
				// 兩個產物會帶同一份依賴的 .pdb 與托管 dll（例如 Ngan.Ime.Core）；
				// 內容相同就留一份，大小不同才是真的衝突，寧可失敗也不要默默覆蓋。
				var First = new FileInfo(Dest).Length;
				if(First == new FileInfo(One).Length){
					continue;
				}
				throw new InvalidOperationException($"攤平時檔名衝突：{Name}（既有 {First} bytes，新的 {new FileInfo(One).Length} bytes）");
			}
			File.Copy(One, Dest);
		}
		Directory.Delete(Stage, recursive: true);
		return Task.FromResult(NIL);
	}

	private static partial Task<nil> CopyPack(ISCtx Ctx, PubOpts Opts){
		var Pack = Ctx.RootDir/PackRelPath;
		if(!Directory.Exists(Pack)){
			throw new InvalidOperationException($"找不到安裝腳本來源：{Pack}");
		}
		foreach(var One in Directory.EnumerateFiles(Pack, "*", SearchOption.TopDirectoryOnly)){
			File.Copy(One, Opts.OutDir/Path.GetFileName(One), overwrite: true);
		}
		return Task.FromResult(NIL);
	}

	private static partial Task<nil> StripSymbols(PubOpts Opts){
		if(Opts.KeepSymbols){
			return Task.FromResult(NIL);
		}
		var Removed = 0;
		foreach(var One in Directory.EnumerateFiles(Opts.OutDir, "*.pdb", SearchOption.TopDirectoryOnly)){
			File.Delete(One);
			Removed++;
		}
		Console.WriteLine($"[PubWinX86] 已剝離 {Removed} 個 .pdb（要保留請加 -KeepSymbols）。");
		return Task.FromResult(NIL);
	}

	private static partial Task<nil> Verify(PubOpts Opts){
		var Required = new[] {
			"Ngan.Ime.Tsf.dll",
			"Ngan.Ime.Windows.exe",
			"Ngan.Ime.Tsf.Registration.exe",
			"install.ps1",
			"install-user.cmd",
			"uninstall.ps1",
			"StartServer.vbs",
			"README.txt",
		};
		foreach(var Name in Required){
			var Path = Opts.OutDir/Name;
			if(!File.Exists(Path)){
				throw new InvalidOperationException($"發布包缺少必要檔案：{Path}");
			}
		}
		var Size = new DirectoryInfo(Opts.OutDir)
			.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
			.Sum(One => One.Length);
		var Count = Directory.EnumerateFiles(Opts.OutDir).Count();
		Console.WriteLine($"[PubWinX86] 完成：{Opts.OutDir}");
		Console.WriteLine($"[PubWinX86] 共 {Count} 個檔案、{Size / (1024.0 * 1024.0):F1} MB。");
		Console.WriteLine("[PubWinX86] 安裝：管理員執行 install.ps1 -RimeDll <rime.dll>，再以本人身分執行 install-user.cmd。");
		return Task.FromResult(NIL);
	}

	/// <summary>指向「最近一次成功發布目錄」的檔案名；放在發布根目錄下。</summary>
	const str CurrentPointerName = "current.txt";

	/// <summary>
	/// 寫下本次發布目錄的絕對路徑，供安裝腳本與排程工作讀取。
	/// </summary>
	/// <remarks>
	/// 帶時間戳發布時，目錄名每次都不同；若不留下指針，使用端就得自己猜哪個是最新的。
	/// 指針只在上一步驗證通過後才寫，故它一定指向一份完整的包。
	/// </remarks>
	private static partial Task<nil> WriteCurrentPointer(PubOpts Opts){
		var Dir = Path.GetDirectoryName((string)Opts.OutDir);
		if(string.IsNullOrEmpty(Dir)){
			throw new InvalidOperationException($"輸出目錄沒有上層目錄：{Opts.OutDir}");
		}
		var Pointer = Path.Combine(Dir, CurrentPointerName);
		File.WriteAllText(Pointer, ((string)Opts.OutDir).Replace('\\', '/'));
		Console.WriteLine($"[PubWinX86] 已更新指針：{Pointer}");
		return Task.FromResult(NIL);
	}
}
