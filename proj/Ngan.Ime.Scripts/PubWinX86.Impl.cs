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

	/// 機器私有資源目錄（正式設定檔的來源；不進版控）。
	const str PrivRsrcRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Windows/ExternalRsrc.Private";

	/// 預設輸出目錄（相對倉庫根）；`artifacts/` 已在 .gitignore 裡。
	const str DfltOutRelPath = "artifacts/publish/win";

	/// 帶時間戳發布時的根目錄（相對倉庫根）；`current.txt` 也放在這裡。
	const str PublishRootRelPath = "artifacts/publish";

	/// 三個產物的暫存目錄（相對輸出目錄）；攤平後刪掉。
	const str StageRelPath = "_stage";

	/// 外殼產物檔名（x64）。
	const str ShellFileName = "Ngan.Ime.Tsf.dll";

	/// 服務端產物檔名（x86）。
	const str ServerFileName = "Ngan.Ime.Windows.exe";

	/// 註冊工具的 apphost 檔名（x64，框架依賴）。
	const str RegExeFileName = "Ngan.Ime.Tsf.Registration.exe";

	/// 註冊工具的托管組件檔名（真正的代碼在這裏）。
	const str RegDllFileName = "Ngan.Ime.Tsf.Registration.dll";

	/// 服務端啟動時讀的配置檔名；安裝腳本要靠它拿 rime.dll 路徑。
	const str RwCfgFileName = "Ngan.Ime.Rw.jsonc";

	/// 只讀配置檔名；與可寫配置一起放進包裡。
	const str RoCfgFileName = "Ngan.Ime.Ro.jsonc";

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
		// 私有配置最後放：它是機器特定的真值，不該被 pack 目錄裏的空模板蓋掉。
		await CopyPrivateRes(Ctx, Opts);
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
		// 同名檔案只在「內容完全相同」時留一份。用大小判斷是不夠的：
		// 兩個不同內容、剛好同大小的檔案會被靜默丟掉一份，而 Verify 只看檔案在不在。
		var Seen = new Dictionary<str, str>(StringComparer.OrdinalIgnoreCase);
		foreach(var One in Directory.EnumerateFiles(Stage, "*", SearchOption.AllDirectories)){
			var Name = Path.GetFileName(One);
			var Dest = Opts.OutDir/Name;
			if(Seen.TryGetValue(Name, out var FirstPath)){
				if(!HasSameContent(FirstPath, One)){
					throw new InvalidOperationException($"攤平時檔名衝突：{Name}（{FirstPath} 與 {One} 內容不同）");
				}
				continue;
			}
			File.Copy(One, Dest, overwrite: true);
			Seen[Name] = One;
		}
		Directory.Delete(Stage, recursive: true);
		return Task.FromResult(NIL);
	}

	/// 兩個檔案內容是否完全相同（SHA-256）；用於攤平時的同名去重。
	static bool HasSameContent(str Left, str Right){
		using var LeftStream = File.OpenRead(Left);
		using var RightStream = File.OpenRead(Right);
		var LeftHash = System.Security.Cryptography.SHA256.HashData(LeftStream);
		var RightHash = System.Security.Cryptography.SHA256.HashData(RightStream);
		return LeftHash.AsSpan().SequenceEqual(RightHash);
	}

	private static partial Task<nil> CopyPack(ISCtx Ctx, PubOpts Opts){
		var Pack = Ctx.RootDir/PackRelPath;
		if(!Directory.Exists(Pack)){
			throw new InvalidOperationException($"找不到安裝腳本來源：{Pack}");
		}
		foreach(var One in Directory.EnumerateFiles(Pack, "*", SearchOption.TopDirectoryOnly)){
			var Name = Path.GetFileName((str)One);
			var Dest = Opts.OutDir/Name;
			// 配置檔不覆蓋：pack 目錄裏那兩份是空模板（給第一次安裝用的骨架），
			// 真正要出貨的是機器私有配置（CopyPrivateRes）。覆蓋順序若反了，
			// 安裝腳本就會拿到空的 DllPath 而直接失敗。
			if(IsCfgFileName(Name) && File.Exists(Dest)){
				Console.WriteLine($"[PubWinX86] 保留已生成的配置：{Name}");
				continue;
			}
			File.Copy(One, Dest, overwrite: true);
		}
		return Task.FromResult(NIL);
	}

	/// 這個檔名是不是服務端的配置檔。
	static bool IsCfgFileName(str Name){
		return Name.Equals(RwCfgFileName, StringComparison.OrdinalIgnoreCase)
			|| Name.Equals(RoCfgFileName, StringComparison.OrdinalIgnoreCase);
	}

	/// 把機器私有的配置放進包裡；服務端以執行檔目錄為儲存區根，故必須與 exe 同層。
	private static partial Task<nil> CopyPrivateRes(ISCtx Ctx, PubOpts Opts){
		var SrcDir = Ctx.RootDir/PrivRsrcRelPath;
		if(!Directory.Exists(SrcDir)){
			// 不算錯誤：可能是第一次在新機器上發布，服務端會用鍵上默認值。
			// 但安裝腳本需要 DllPath，所以這裡要出聲，否則使用者只會在安裝時看到失敗。
			Console.Error.WriteLine($"[PubWinX86] 沒有私有資源目錄，跳過：{SrcDir}");
			Console.Error.WriteLine("[PubWinX86] 提醒：install.ps1 需要 Ngan.Ime.Rw.jsonc 裏的 Librime.DllPath。");
			return Task.FromResult(NIL);
		}
		var Count = 0;
		foreach(var One in Directory.EnumerateFiles(SrcDir, "*", SearchOption.TopDirectoryOnly)){
			File.Copy(One, Opts.OutDir/Path.GetFileName(One), overwrite: true);
			Count++;
		}
		Console.WriteLine($"[PubWinX86] 已放入 {Count} 個私有配置。");
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
		foreach(var Name in Required){
			var Path = Opts.OutDir/Name;
			if(!File.Exists(Path)){
				throw new InvalidOperationException($"發布包缺少必要檔案：{Path}");
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

		var Size = new DirectoryInfo(Opts.OutDir)
			.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
			.Sum(One => One.Length);
		var Count = Directory.EnumerateFiles(Opts.OutDir).Count();
		Console.WriteLine($"[PubWinX86] 完成：{Opts.OutDir}");
		Console.WriteLine($"[PubWinX86] 共 {Count} 個檔案、{Size / (1024.0 * 1024.0):F1} MB。");
		Console.WriteLine("[PubWinX86] 安裝：管理員執行 install.ps1（讀同層 Ngan.Ime.Rw.jsonc 的 DllPath），");
		Console.WriteLine("            再以本人身分執行 install-user.cmd；每次安裝都是一個獨立輸入法。");
		return Task.FromResult(NIL);
	}

	/// 檢查一個產物的位數與托管/原生屬性。
	/// <param name="Path">要被檢查的檔案。</param>
	/// <param name="ExpectMachine">期望的位數。</param>
	/// <param name="Kind">期望的映像類型；<see cref="EPeKind.Any"/> 表示不檢查 CLR header。</param>
	static void VerifyImage(str Path, ushort ExpectMachine, EPeKind Kind){
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

	/// 確認腳本含非 ASCII 時帶 UTF-8 BOM。
	/// <param name="Path">要檢查的腳本。</param>
	/// <remarks>
	/// Windows PowerShell 5.1 對沒有 BOM 的 .ps1 一律按 ANSI 解讀。
	/// 中文註釋與訊息被解成亂碼之後，某些位元組序列會把後面的引號吃掉，
	/// 於是整個腳本語法錯誤——症狀是「安裝腳本執行不了」，但錯因在編碼，不在腳本邏輯。
	/// </remarks>
	static void VerifyScriptBom(str Path){
		var Bytes = File.ReadAllBytes(Path);
		var HasBom = Bytes.Length >= 3 && Bytes[0] == 0xEF && Bytes[1] == 0xBB && Bytes[2] == 0xBF;
		var HasNonAscii = false;
		var Start = HasBom ? 3 : 0;
		for(var Index = Start; Index < Bytes.Length; Index++){
			if(Bytes[Index] > 0x7F){
				HasNonAscii = true;
				break;
			}
		}
		if(HasNonAscii && !HasBom){
			throw new InvalidOperationException(
				$"腳本含非 ASCII 但沒有 UTF-8 BOM，PowerShell 5.1 會讀成亂碼而無法執行：{Path}");
		}
	}

	/// 確認包裏的可寫配置真的填了 `Librime.DllPath`。
	/// <remarks>
	/// 先前發過一次「配置是空模板」的包：publish 成功、檔案齊全，但安裝腳本第一步就失敗。
	/// 這裡只是把那個失敗提前到發布階段——發布的人與安裝的人常常是同一個，早失敗早發現。
	/// </remarks>
	static void VerifyRimeDllConfigured(str Path){
		var Text = File.ReadAllText(Path);
		if(!Text.Contains("DllPath", StringComparison.Ordinal)){
			throw new InvalidOperationException($"配置裏沒有 DllPath：{Path}");
		}
		// 不引入 JSON 解析：只要確認 DllPath 的值不是空的。發布腳本不該為此拉一個序列化器。
		var Index = Text.IndexOf("DllPath", StringComparison.Ordinal);
		var Tail = Text[(Index + "DllPath".Length)..];
		var Colon = Tail.IndexOf(':');
		if(Colon < 0){
			throw new InvalidOperationException($"配置裏的 DllPath 格式不對：{Path}");
		}
		var Value = Tail[(Colon + 1)..].TrimStart();
		if(Value.Length < 2 || Value[0] != '"'){
			throw new InvalidOperationException($"配置裏的 DllPath 不是字串：{Path}");
		}
		var End = Value.IndexOf('"', 1);
		if(End <= 1){
			throw new InvalidOperationException(
				$"配置裏的 Librime.DllPath 是空的——安裝腳本會拒絕安裝，請先填好 {Path}");
		}
	}

	/// 產物的映像類型。
	internal enum EPeKind{
		/// 原生映像（NativeAOT）；不帶 CLR header。
		Native,
		/// 托管組件；必須帶 CLR header。
		Managed,
		/// 不檢查（例如框架依賴發布的原生 apphost）。
		Any,
	}

	/// <summary>指向「最近一次成功發布目錄」的檔案名；放在發布根目錄下。</summary>
	const str CurrentPointerName = "current.txt";

	/// <summary>
	/// 寫下本次發布目錄的絕對路徑，供人與後續命令查「哪一份是最新的」。
	/// </summary>
	/// <remarks>
	/// 帶時間戳發布時，目錄名每次都不同；若不留下指針，使用端就得自己猜哪個是最新的。
	/// 指針只在上一步驗證通過後才寫，故它一定指向一份完整的包。
	/// *目前沒有任何腳本讀它*，它是給人看的；要讓安裝腳本自動取最新版本時再讓它讀。
	/// 內容是本機絕對路徑（用 `/` 分隔），換機器之後沒有意義——這是刻意的，因為它描述的就是
	/// 「這臺機器上哪個目錄剛出爐」。
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
