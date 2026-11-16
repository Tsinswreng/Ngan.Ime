using Tsinswreng.CsCore;
using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace Ngan.Ime.Scripts;

[Doc($$"""
PubWin 的流程實現。
""")]
internal static partial class PubWinX86{

	[Doc($$"""
	TSF 外殼專案（x64，被應用行程載入）。
	""")]
	const str TsfCsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Tsf.Windows/Ngan.Ime.Tsf.Windows.csproj";

	[Doc($$"""
	服務端專案（x86，載入 rime.dll）。
	""")]
	const str ServerCsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Windows/Ngan.Ime.Windows.csproj";

	[Doc($$"""
	註冊工具專案（x64，框架依賴）。
	""")]
	const str RegCsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Tsf.Registration/Ngan.Ime.Tsf.Registration.csproj";

	[Doc($$"""
	安裝腳本與說明的來源目錄。
	""")]
	const str PackRelPath = "scripts/win-vm/pack";

	[Doc($$"""
	機器私有資源目錄（正式設定檔的來源；不進版控）。
	""")]
	const str PrivRsrcRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Windows/ExternalRsrc.Private";

	[Doc($$"""
	預設輸出目錄（相對倉庫根）；`artifacts/` 已在 .gitignore 裡。
	""")]
	const str DfltOutRelPath = "artifacts/publish/win";

	[Doc($$"""
	帶時間戳發布時的根目錄（相對倉庫根）；`current.txt` 也放在這裡。
	""")]
	const str PublishRootRelPath = "artifacts/publish";

	[Doc($$"""
	三個產物的暫存目錄（相對輸出目錄）；攤平後刪掉。
	""")]
	const str StageRelPath = "_stage";

	[Doc($$"""
	外殼產物檔名（x64）。
	""")]
	const str ShellFileName = "Ngan.Ime.Tsf.dll";

	[Doc($$"""
	服務端產物檔名（x86）。
	""")]
	const str ServerFileName = "Ngan.Ime.Windows.exe";

	[Doc($$"""
	註冊工具的 apphost 檔名（x64，框架依賴）。
	""")]
	const str RegExeFileName = "Ngan.Ime.Tsf.Registration.exe";

	[Doc($$"""
	註冊工具的托管組件檔名（真正的代碼在這裏）。
	""")]
	const str RegDllFileName = "Ngan.Ime.Tsf.Registration.dll";

	[Doc($$"""
	服務端啟動時讀的配置檔名；安裝腳本要靠它拿 rime.dll 路徑。
	""")]
	const str RwCfgFileName = "Ngan.Ime.Rw.jsonc";

	[Doc($$"""
	只讀配置檔名；與可寫配置一起放進包裡。
	""")]
	const str RoCfgFileName = "Ngan.Ime.Ro.jsonc";

	internal static async partial Task Main(ISCtx Ctx, CT Ct){
		var opts = ParseOpts();
		if(opts.OutDir.Length == 0){
			// 帶時間戳發布：每次都是新目錄，不會去刪使用中的舊目錄。
			opts.OutDir = opts.Snapshot
				? Ctx.RootDir/PublishRootRelPath/("win-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"))
				: Ctx.RootDir/DfltOutRelPath;
		}

		Console.WriteLine("[PubWinX86] 本次設定：");
		Console.WriteLine($"  輸出目錄: {opts.OutDir}");
		Console.WriteLine("  內容    : 外殼 x64 + 服務端 x86 + 註冊工具 + 安裝腳本（同一層）");

		if(opts.DryRun){
			Console.WriteLine("[PubWinX86] -DryRun：將發布三個專案並攤平到輸出目錄，不執行。");
			return;
		}

		// 整包重建：輸出目錄是產物，留著上一輪的檔案只會混淆。
		// *但只刪「看起來是上一次發布的目錄」*：使用者可以用 `-Out` 指到任何路徑，
		// 無條件遞歸刪除會把他自己放的東西一起刪掉。判據是「含本次發布的核心產物」。
		if(Directory.Exists(opts.OutDir)){
			EnsureLooksLikePublishDir(opts);
			Directory.Delete(opts.OutDir, recursive: true);
		}
		Directory.CreateDirectory(opts.OutDir);

		await Publish(Ctx, opts, Ct);
		await Flatten(opts);
		await CopyPack(Ctx, opts);
		// 私有配置最後放：它是機器特定的真值，不該被 pack 目錄裏的空模板蓋掉。
		await CopyPrivateRes(Ctx, opts);
		await StripSymbols(opts);
		await Verify(opts);
		if(opts.Snapshot){
			await WriteCurrentPointer(opts);
		}
	}

	private static partial PubOpts ParseOpts(){
		var opts = new PubOpts();
		var args = Environment.GetCommandLineArgs().Skip(1).ToList();
		for(var i = 0; i < args.Count; i++){
			var arg = args[i];
			if(!arg.StartsWith("-", StringComparison.Ordinal)){
				continue;
			}
			switch(arg.TrimStart('-').ToLowerInvariant()){
				case "out":
					if(i + 1 >= args.Count){
						throw new ArgumentException("-Out 需要一個目錄參數。");
					}
					opts.OutDir = args[++i];
					opts.OutDirExplicit = true;
					break;
				case "keepsymbols":
					opts.KeepSymbols = true;
					break;
				case "snap":
				case "timestamp":
					opts.Snapshot = true;
					break;
				case "dryrun":
					opts.DryRun = true;
					break;
				case "help":
				case "h":
					Console.Error.WriteLine("用法: dotnet run --project proj/Ngan.Ime.Scripts -- PubWinX86 [-Snap] [-Out <目錄>] [-KeepSymbols] [-DryRun]");
					Environment.Exit(0);
					break;
				default:
					throw new ArgumentException($"未知選項：{arg}。可用：-Snap -Out -KeepSymbols -DryRun");
			}
		}
		return opts;
	}

	private static async partial Task<nil> Publish(ISCtx Ctx, PubOpts Opts, CT Ct){
		var stage = Opts.OutDir/StageRelPath;

		// 外殼：x64 NativeAOT，產物只有一個 DLL。
		var shellOut = stage/"shell";
		Mkdir(shellOut);
		Console.WriteLine("[PubWinX86] 發布外殼（win-x64, NativeAOT）…");
		await Exe("dotnet", ["publish", (str)(Ctx.RootDir/TsfCsprojRelPath), "-c", "Release", "-r", "win-x64", "-o", (str)shellOut], Ct);

		// 服務端：x86 NativeAOT，要跟它自己的原生依賴一起攤平。
		var serverOut = stage/"server";
		Mkdir(serverOut);
		Console.WriteLine("[PubWinX86] 發布服務端（win-x86, NativeAOT）…");
		await Exe("dotnet", ["publish", (str)(Ctx.RootDir/ServerCsprojRelPath), "-c", "Release", "-r", "win-x86", "-o", (str)serverOut], Ct);

		// 註冊工具：x64 框架依賴。它不是性能路徑，沒必要 AOT；且 AOT 會把它自己變成 20 MB。
		var regOut = stage/"reg";
		Mkdir(regOut);
		Console.WriteLine("[PubWinX86] 發布註冊工具（win-x64, framework-dependent）…");
		await Exe("dotnet", [
			"publish", (str)(Ctx.RootDir/RegCsprojRelPath), "-c", "Release", "-r", "win-x64",
			"--self-contained", "false", "-p:PublishAot=false", "-o", (str)regOut,
		], Ct);
		return NIL;
	}

	private static partial Task<nil> Flatten(PubOpts Opts){
		var stage = Opts.OutDir/StageRelPath;
		if(!Directory.Exists(stage)){
			throw new InvalidOperationException($"找不到暫存目錄：{stage}");
		}
		// 同名檔案只在「內容完全相同」時留一份。用大小判斷是不夠的：
		// 兩個不同內容、剛好同大小的檔案會被靜默丟掉一份，而 Verify 只看檔案在不在。
		var seen = new Dictionary<str, str>(StringComparer.OrdinalIgnoreCase);
		foreach(var one in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories)){
			var name = Path.GetFileName(one);
			var dest = Opts.OutDir/name;
			if(seen.TryGetValue(name, out var firstPath)){
				if(!HasSameContent(firstPath, one)){
					throw new InvalidOperationException($"攤平時檔名衝突：{name}（{firstPath} 與 {one} 內容不同）");
				}
				continue;
			}
			File.Copy(one, dest, overwrite: true);
			seen[name] = one;
		}
		Directory.Delete(stage, recursive: true);
		return Task.FromResult(NIL);
	}

	[Doc($$"""
	兩個檔案內容是否完全相同（SHA-256）；用於攤平時的同名去重。
	""")]
	static bool HasSameContent(str Left, str Right){
		using var leftStream = File.OpenRead(Left);
		using var rightStream = File.OpenRead(Right);
		var leftHash = System.Security.Cryptography.SHA256.HashData(leftStream);
		var rightHash = System.Security.Cryptography.SHA256.HashData(rightStream);
		return leftHash.AsSpan().SequenceEqual(rightHash);
	}

	[Doc($$"""
	確認輸出目錄看起來是上一次的發布目錄；不是就拒絕遞歸刪除。
	`-Out` 是使用者給的任意路徑。舊版直接 `Directory.Delete(recursive: true)`，
	指到任何既有目錄就會把裡面的東西全刪掉。判據刻意放寬：只要看得到任何一個本次產物
	就當成上一次的發布（舊版發布可能缺其中一兩個檔案）；空目錄也放行（沒有東西可刪）。
	#Prm[{{nameof(Opts)}}][本次選項。]
	""")]
	static void EnsureLooksLikePublishDir(PubOpts Opts){
		if(!Directory.EnumerateFileSystemEntries(Opts.OutDir).Any()){
			return;
		}
		var markers = new[] { ShellFileName, ServerFileName, RegExeFileName, RwCfgFileName, "install.ps1" };
		foreach(var one in markers){
			if(File.Exists(Opts.OutDir/one)){
				return;
			}
		}
		throw new InvalidOperationException(
			$"輸出目錄不是上一次的發布目錄，拒絕刪除：{Opts.OutDir}"
			+ $"（裡面看不到 {str.Join("、", markers)} 任何一個）。"
			+ "請改指定一個新的 `-Out` 目錄，或先自行清空它。");
	}

	private static partial Task<nil> CopyPack(ISCtx Ctx, PubOpts Opts){
		var Pack = Ctx.RootDir/PackRelPath;
		if(!Directory.Exists(Pack)){
			throw new InvalidOperationException($"找不到安裝腳本來源：{Pack}");
		}
		foreach(var one in Directory.EnumerateFiles(Pack, "*", SearchOption.TopDirectoryOnly)){
			var name = Path.GetFileName((str)one);
			var dest = Opts.OutDir/name;
			// 配置檔不覆蓋：pack 目錄裏那兩份是空模板（給第一次安裝用的骨架），
			// 真正要出貨的是機器私有配置（CopyPrivateRes）。覆蓋順序若反了，
			// 安裝腳本就會拿到空的 DllPath 而直接失敗。
			if(IsCfgFileName(name) && File.Exists(dest)){
				Console.WriteLine($"[PubWinX86] 保留已生成的配置：{name}");
				continue;
			}
			File.Copy(one, dest, overwrite: true);
		}
		return Task.FromResult(NIL);
	}

	[Doc($$"""
	這個檔名是不是服務端的配置檔。
	""")]
	static bool IsCfgFileName(str Name){
		return Name.Equals(RwCfgFileName, StringComparison.OrdinalIgnoreCase)
			|| Name.Equals(RoCfgFileName, StringComparison.OrdinalIgnoreCase);
	}

	[Doc($$"""
	把機器私有的配置放進包裡；服務端以執行檔目錄為儲存區根，故必須與 exe 同層。
	""")]
	private static partial Task<nil> CopyPrivateRes(ISCtx Ctx, PubOpts Opts){
		var srcDir = Ctx.RootDir/PrivRsrcRelPath;
		if(!Directory.Exists(srcDir)){
			// 不算錯誤：可能是第一次在新機器上發布，服務端會用鍵上默認值。
			// 但安裝腳本需要 DllPath，所以這裡要出聲，否則使用者只會在安裝時看到失敗。
			Console.Error.WriteLine($"[PubWinX86] 沒有私有資源目錄，跳過：{srcDir}");
			Console.Error.WriteLine("[PubWinX86] 提醒：install.ps1 需要 Ngan.Ime.Rw.jsonc 裏的 Librime.DllPath。");
			return Task.FromResult(NIL);
		}
		var Count = 0;
		foreach(var one in Directory.EnumerateFiles(srcDir, "*", SearchOption.TopDirectoryOnly)){
			File.Copy(one, Opts.OutDir/Path.GetFileName(one), overwrite: true);
			Count++;
		}
		Console.WriteLine($"[PubWinX86] 已放入 {Count} 個私有配置。");
		return Task.FromResult(NIL);
	}

	private static partial Task<nil> StripSymbols(PubOpts Opts){
		if(Opts.KeepSymbols){
			return Task.FromResult(NIL);
		}
		var removed = 0;
		foreach(var one in Directory.EnumerateFiles(Opts.OutDir, "*.pdb", SearchOption.TopDirectoryOnly)){
			File.Delete(one);
			removed++;
		}
		Console.WriteLine($"[PubWinX86] 已剝離 {removed} 個 .pdb（要保留請加 -KeepSymbols）。");
		return Task.FromResult(NIL);
	}

	private static partial Task<nil> Verify(PubOpts Opts){
		var required = new[] {
			ShellFileName,
			ServerFileName,
			RegExeFileName,
			"install.ps1",
			"install-user.cmd",
			"uninstall.ps1",
			"StartServer.vbs",
			"README.txt",
			RwCfgFileName,
		};
		foreach(var name in required){
			var path = Opts.OutDir/name;
			if(!File.Exists(path)){
				throw new InvalidOperationException($"發布包缺少必要檔案：{path}");
			}
		}
		// 位數與「是不是原生」是「裝上去能不能用」的前置條件，只檢查檔案在不在是不夠的。
		VerifyImage(Opts.OutDir/ShellFileName, PeImage.MachineAmd64, EPeKind.Native);
		VerifyImage(Opts.OutDir/ServerFileName, PeImage.MachineI386, EPeKind.Native);
		// 註冊工具是框架依賴發布：exe 是原生 apphost（沒有 CLR header），托管代碼在 .dll 裏。
		// 先前對著 exe 檢查 CLR header，必然失敗——這正是「驗證要驗對檔案」的例子。
		VerifyImage(Opts.OutDir/RegExeFileName, PeImage.MachineAmd64, EPeKind.Any);
		VerifyImage(Opts.OutDir/RegDllFileName, PeImage.MachineAmd64, EPeKind.Managed);
		// 安裝腳本第一件事就是讀這一份的 DllPath；空的話安裝一定失敗，
		// 那種包不該被當成「驗證通過的發布」。
		VerifyRimeDllConfigured(Opts.OutDir/RwCfgFileName);
		// PowerShell 5.1 讀沒有 BOM 的 .ps1 會當成 ANSI：含中文的腳本會變成亂碼，
		// 亂碼吃掉引號之後就是語法錯誤，安裝腳本根本跑不起來（2026-11-15 實際發生）。
		VerifyScriptBom(Opts.OutDir/"install.ps1");
		VerifyScriptBom(Opts.OutDir/"uninstall.ps1");

		var size = new DirectoryInfo(Opts.OutDir)
			.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
			.Sum(One => One.Length);
		var Count = Directory.EnumerateFiles(Opts.OutDir).Count();
		Console.WriteLine($"[PubWinX86] 完成：{Opts.OutDir}");
		Console.WriteLine($"[PubWinX86] 共 {Count} 個檔案、{size / (1024.0 * 1024.0):F1} MB。");
		Console.WriteLine("[PubWinX86] 安裝：管理員執行 install.ps1（讀同層 Ngan.Ime.Rw.jsonc 的 DllPath），");
		Console.WriteLine("            再以本人身分執行 install-user.cmd；每次安裝都是一個獨立輸入法。");
		return Task.FromResult(NIL);
	}

	[Doc($$"""
	檢查一個產物的位數與托管/原生屬性。
	#Prm[{{nameof(Path)}}][要被檢查的檔案。]
	#Prm[{{nameof(ExpectMachine)}}][期望的位數。]
	#Prm[{{nameof(Kind)}}][期望的映像類型；{{nameof(EPeKind.Any)}} 表示不檢查 CLR header。]
	""")]
	static void VerifyImage(str Path, u16 ExpectMachine, EPeKind Kind){
		PeImage.PeInfo Info;
		try{
			Info = PeImage.Read(Path);
		}catch(InvalidDataException Ex){
			throw new InvalidOperationException($"不是有效的 PE 檔：{Path}（{Ex.Message}）");
		}
		if(Info.Machine != ExpectMachine){
			throw new InvalidOperationException(
				$"位數不對：{Path} 是 {PeImage.Describe(Info.Machine)}，應該是 {PeImage.Describe(ExpectMachine)}");
		}
		if(Kind == EPeKind.Managed && !Info.HasClrHeader){
			throw new InvalidOperationException($"應為托管組件但沒有 CLR header：{Path}");
		}
		if(Kind == EPeKind.Native && Info.HasClrHeader){
			throw new InvalidOperationException(
				$"應為 NativeAOT 原生映像但帶了 CLR header：{Path}（會把 CLR 帶進別人的行程）");
		}
	}

	[Doc($$"""
	確認腳本含非 ASCII 時帶 UTF-8 BOM。
	Windows PowerShell 5.1 對沒有 BOM 的 .ps1 一律按 ANSI 解讀。
	中文註釋與訊息被解成亂碼之後，某些位元組序列會把後面的引號吃掉，
	於是整個腳本語法錯誤——症狀是「安裝腳本執行不了」，但錯因在編碼，不在腳本邏輯。
	#Prm[{{nameof(Path)}}][要檢查的腳本。]
	""")]
	static void VerifyScriptBom(str Path){
		var bytes = File.ReadAllBytes(Path);
		var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
		var hasNonAscii = false;
		var start = hasBom ? 3 : 0;
		for(var index = start; index < bytes.Length; index++){
			if(bytes[index] > 0x7F){
				hasNonAscii = true;
				break;
			}
		}
		if(hasNonAscii && !hasBom){
			throw new InvalidOperationException(
				$"腳本含非 ASCII 但沒有 UTF-8 BOM，PowerShell 5.1 會讀成亂碼而無法執行：{Path}");
		}
	}

	[Doc($$"""
	確認包裏的可寫配置真的填了 `Librime.DllPath`。
	先前發過一次「配置是空模板」的包：publish 成功、檔案齊全，但安裝腳本第一步就失敗。
	這裡只是把那個失敗提前到發布階段——發布的人與安裝的人常常是同一個，早失敗早發現。
	""")]
	static void VerifyRimeDllConfigured(str Path){
		var text = File.ReadAllText(Path);
		if(!text.Contains("DllPath", StringComparison.Ordinal)){
			throw new InvalidOperationException($"配置裏沒有 DllPath：{Path}");
		}
		// 不引入 JSON 解析：只要確認 DllPath 的值不是空的。發布腳本不該為此拉一個序列化器。
		var index = text.IndexOf("DllPath", StringComparison.Ordinal);
		var tail = text[(index + "DllPath".Length)..];
		var colon = tail.IndexOf(':');
		if(colon < 0){
			throw new InvalidOperationException($"配置裏的 DllPath 格式不對：{Path}");
		}
		var value = tail[(colon + 1)..].TrimStart();
		if(value.Length < 2 || value[0] != '"'){
			throw new InvalidOperationException($"配置裏的 DllPath 不是字串：{Path}");
		}
		var end = value.IndexOf('"', 1);
		if(end <= 1){
			throw new InvalidOperationException(
				$"配置裏的 Librime.DllPath 是空的——安裝腳本會拒絕安裝，請先填好 {Path}");
		}
	}

	[Doc($$"""
	產物的映像類型。
	""")]
	internal enum EPeKind{
		[Doc($$"""
		原生映像（NativeAOT）；不帶 CLR header。
		""")]
		Native,
		[Doc($$"""
		托管組件；必須帶 CLR header。
		""")]
		Managed,
		[Doc($$"""
		不檢查（例如框架依賴發布的原生 apphost）。
		""")]
		Any,
	}

	[Doc($$"""
	指向「最近一次成功發布目錄」的檔案名；放在發布根目錄下。
	""")]
	const str CurrentPointerName = "current.txt";

	[Doc($$"""
	寫下本次發布目錄的絕對路徑，供人與後續命令查「哪一份是最新的」。
	帶時間戳發布時，目錄名每次都不同；若不留下指針，使用端就得自己猜哪個是最新的。
	指針只在上一步驗證通過後才寫，故它一定指向一份完整的包。
	*目前沒有任何腳本讀它*，它是給人看的；要讓安裝腳本自動取最新版本時再讓它讀。
	內容是本機絕對路徑（用 `/` 分隔），換機器之後沒有意義——這是刻意的，因為它描述的就是
	「這臺機器上哪個目錄剛出爐」。
	""")]
	private static partial Task<nil> WriteCurrentPointer(PubOpts Opts){
		var dir = Path.GetDirectoryName((str)Opts.OutDir);
		if(str.IsNullOrEmpty(dir)){
			throw new InvalidOperationException($"輸出目錄沒有上層目錄：{Opts.OutDir}");
		}
		var pointer = Path.Combine(dir, CurrentPointerName);
		File.WriteAllText(pointer, ((str)Opts.OutDir).Replace('\\', '/'));
		Console.WriteLine($"[PubWinX86] 已更新指針：{pointer}");
		return Task.FromResult(NIL);
	}
}
