//Impl: 聲明見 DeployWinVm.cs。
namespace Ngan.Ime.Scripts;

using System.IO.Compression;
using System.Text;
using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

/// <summary>DeployWinVm 的流程實現。</summary>
internal static partial class DeployWinVm{

	/// 主機端 staging 目錄（相對倉庫根）；產物與日誌都在這裡，不入版控。
	const str OutRelPath = "scripts/win-vm/out";

	/// TSF 專案（x64，被 explorer 載入）。
	const str TsfCsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Tsf.Windows/Ngan.Ime.Tsf.Windows.csproj";

	/// TSF 專案目錄。
	const str TsfProjRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Tsf.Windows";

	/// 註冊工具專案（x64）。
	const str RegCsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Tsf.Registration/Ngan.Ime.Tsf.Registration.csproj";

	/// 註冊工具專案目錄。
	const str RegProjRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Tsf.Registration";

	/// Windows 宿主專案（x86，要載 x86 的 rime.dll）。
	const str HostCsprojRelPath = "proj/Ngan.Ime.Frontend/Ngan.Ime.Windows/Ngan.Ime.Windows.csproj";

	/// 宿主的配置檔；要一起放進發布輸出（儲存區根目錄＝執行檔目錄）。
	static readonly str[] HostCfgFiles = ["Ngan.Ime.Ro.jsonc", "Ngan.Ime.Rw.jsonc"];

	/// 宿主原始碼目錄（與其依賴），用來判斷是否需要重建。
	static readonly str[] HostSrcRelPaths = [
		"proj/Ngan.Ime.Frontend/Ngan.Ime.Windows",
		"proj/Ngan.Ime.Frontend/Ngan.Ime.UI",
		"proj/Ngan.Ime.Frontend/Ngan.Ime.Backend",
		"proj/Ngan.Ime.Core",
		"proj/Ngan.Ime.Rime",
	];

	internal static async partial Task Main(ISCtx Ctx, CT Ct){
		var O = ParseOpts();
		var P = MkPaths(Ctx);

		Console.WriteLine("[DeployWinVm] 本次設定：");
		Console.WriteLine($"  VM        : {O.VmxPath}");
		Console.WriteLine($"  guest 帳號: {O.GuestUser} / 桌面使用者 {O.DesktopUser}");
		Console.WriteLine($"  宿主模式  : {(O.Fast ? "Fast（framework-dependent x86）" : "AOT（self-contained x86）")}");
		Console.WriteLine($"  TSF 版本  : {O.TsfVersion}");
		Console.WriteLine($"  staging   : {P.OutRoot}");

		if(O.DryRun){
			await BuildArtifacts(O, P, Ct, DryRun: true);
			await Stage(O, P, Ct, DryRun: true);
			Console.WriteLine("[DeployWinVm] -DryRun：不連 VM、不建置、不複製。以上是將執行的步驟。");
			return;
		}

		// 1: VM 就緒（可選自動開機）。
		await EnsureVmRunning(O, Ct);

		// 2: 增量建置三個產物。
		await BuildArtifacts(O, P, Ct, DryRun: false);

		// 3: 打包並寫出 guest 腳本／參數檔。
		await Stage(O, P, Ct, DryRun: false);

		// 4: 複製進 guest。
		await CopyToGuest(O, P, Ct);

		// 5: guest 內部署（停舊宿主、解包、註冊、啟動、煙霧測試）。
		await RunGuestDeploy(O, P, Ct);

		// 6: 抓回日誌並回報。
		await CollectLogs(O, P, Ct);
	}

	private static partial WinVmOpts ParseOpts(){
		var O = new WinVmOpts();
		var Flags = Environment.GetCommandLineArgs()
			.Skip(1)
			.Where(A => A.StartsWith("-", StringComparison.Ordinal))
			.Select(A => A.TrimStart('-').ToLowerInvariant())
			.ToList();
		foreach(var F in Flags){
			switch(F){
				case "nobuild": O.NoBuild = true; break;
				case "fast": O.Fast = true; break;
				case "startvm": O.StartVm = true; break;
				case "opennotepad": O.OpenNotepad = true; break;
				case "skipsmoke": O.SkipSmoke = true; break;
				case "restartexplorer": O.RestartExplorer = true; break;
				case "dryrun": O.DryRun = true; break;
				case "help": case "h":
					Console.Error.WriteLine("用法: dotnet run --project proj/Ngan.Ime.Frontend/Ngan.Ime.Scripts -- DeployWinVm [-Fast] [-NoBuild] [-StartVm] [-OpenNotepad] [-SkipSmoke] [-RestartExplorer] [-DryRun]");
					Environment.Exit(0);
					break;
				default:
					throw new ArgumentException($"未知選項：-{F}。可用：-Fast -NoBuild -StartVm -OpenNotepad -SkipSmoke -DryRun");
			}
		}
		return O;
	}

	private static async partial Task<nil> EnsureVmRunning(WinVmOpts O, CT Ct){
		var Running = await VmRunText(O, HostArgs("list"), Ct);
		var VmxName = Path.GetFileName(O.VmxPath);
		if(!Running.Contains(VmxName, StringComparison.OrdinalIgnoreCase)){
			if(!O.StartVm){
				throw new InvalidOperationException(
					$"VM 未開機：{O.VmxPath}。先手動開機，或加 -StartVm 自動開機（會用 nogui）。");
			}
			Console.WriteLine("[DeployWinVm] VM 未開機，正在啟動（nogui）…");
			await VmRun(O, HostArgs("start", O.VmxPath, "nogui"), Ct, TimeoutSec: 180);
			// 等 VM 進入 running 狀態。
			for(var I = 0; I < 60; I++){
				await Task.Delay(1000, Ct);
				Running = await VmRunText(O, HostArgs("list"), Ct);
				if(Running.Contains(VmxName, StringComparison.OrdinalIgnoreCase)){
					break;
				}
			}
		}

		// 等 guest 操作通道（VMware Tools）可用。
		Console.Write("[DeployWinVm] 等待 guest 操作通道");
		for(var I = 0; I < 120; I++){
			var R = await VmRunRaw(O, GuestArgs(O, "listProcessesInGuest", O.VmxPath), 60, Ct);
			if(R.Exit.IsSuccess){
				Console.WriteLine(" 就緒");
				return NIL;
			}
			Console.Write(".");
			await Task.Delay(1000, Ct);
		}
		throw new InvalidOperationException("等候 guest 操作通道逾時（VMware Tools 未就緒？）");
	}

	private static async partial Task<nil> BuildArtifacts(WinVmOpts O, WinVmPaths P, CT Ct, bool DryRun){
		Mkdir(P.OutRoot);
		Mkdir(P.TsfPublish);
		Mkdir(P.RegPublish);
		Mkdir(P.HostPublish);

		var Root = (Pth)P.OutRoot/""; // 佔位，避免未使用警告
		_ = Root;

		if(O.NoBuild){
			Console.WriteLine("[DeployWinVm] -NoBuild：跳過建置。");
			return NIL;
		}

		// TSF：只有它自己的源碼變了才重發（AOT 最慢的一步）。
		if(DryRun || NeedBuild(P, P.TsfPublish/"Ngan.Ime.Tsf.dll", TsfProjRelPath)){
			Console.WriteLine("[DeployWinVm] 發布 TSF（win-x64, NativeAOT）…");
			if(!DryRun){
				await Exe("dotnet", ["publish", (str)(P.RepoRoot/TsfCsprojRelPath), "-c", "Release", "-r", "win-x64", "-o", (str)P.TsfPublish], Ct);
			}
		}else{
			Console.WriteLine("[DeployWinVm] TSF 無變更，沿用既有產物。");
		}

		// 註冊工具：framework-dependent x64（guest 已有 x64 runtime）。
		if(DryRun || NeedBuild(P, P.RegPublish/"Ngan.Ime.Tsf.Registration.exe", RegProjRelPath)){
			Console.WriteLine("[DeployWinVm] 發布註冊工具（win-x64）…");
			if(!DryRun){
				await Exe("dotnet", [
					"publish", (str)(P.RepoRoot/RegCsprojRelPath), "-c", "Release", "-r", "win-x64",
					"--self-contained", "false", "-p:PublishAot=false", "-o", (str)P.RegPublish,
				], Ct);
			}
		}else{
			Console.WriteLine("[DeployWinVm] 註冊工具無變更，沿用既有產物。");
		}

		// 宿主：預設 AOT x86；-Fast 時 framework-dependent（快，但 guest 需 x86 runtime）。
		if(DryRun || NeedBuild(P, P.HostPublish/"Ngan.Ime.Windows.exe", HostSrcRelPaths)){
			var Extra = O.Fast
				? new List<str>{ "--self-contained", "false", "-p:PublishAot=false", "-p:PublishReadyToRun=false" }
				: new List<str>();
			Console.WriteLine($"[DeployWinVm] 發布宿主（win-x86, {(O.Fast ? "framework-dependent" : "NativeAOT")}）…");
			if(!DryRun){
				var Args = new List<str>{ "publish", (str)(P.RepoRoot/HostCsprojRelPath), "-c", "Release", "-r", "win-x86", "-o", (str)P.HostPublish };
				Args.AddRange(Extra);
				await Exe("dotnet", Args, Ct);
			}
		}else{
			Console.WriteLine("[DeployWinVm] 宿主無變更，沿用既有產物。");
		}
		return NIL;
	}

	private static async partial Task<nil> Stage(WinVmOpts O, WinVmPaths P, CT Ct, bool DryRun){
		// 把配置檔一起放進宿主目錄：宿主以執行檔目錄為儲存區根，找不到配置就會用默認值。
		var HostProj = P.RepoRoot/"proj/Ngan.Ime.Frontend/Ngan.Ime.Windows";
		foreach(var Name in HostCfgFiles){
			var Src = HostProj/Name;
			if(IsFile(Src)){
				if(!DryRun){
					Cp(Src, P.HostPublish/Name, new CpOptions(Overwrite: true));
				}
			}else{
				Console.Error.WriteLine($"[DeployWinVm] 警告：缺少配置檔 {Src}（宿主會用鍵上默認值）。");
			}
		}

		if(!DryRun){
			// 打包：一次複製一個 zip，比逐檔複製快很多。
			if(File.Exists(P.TsfZip)){ File.Delete(P.TsfZip); }
			if(File.Exists(P.RegZip)){ File.Delete(P.RegZip); }
			if(File.Exists(P.HostZip)){ File.Delete(P.HostZip); }
			ZipFile.CreateFromDirectory((str)P.TsfPublish, (str)P.TsfZip, CompressionLevel.Fastest, includeBaseDirectory: false);
			ZipFile.CreateFromDirectory((str)P.RegPublish, (str)P.RegZip, CompressionLevel.Fastest, includeBaseDirectory: false);
			ZipFile.CreateFromDirectory((str)P.HostPublish, (str)P.HostZip, CompressionLevel.Fastest, includeBaseDirectory: false);

			// 必須帶 BOM：guest 是 Windows PowerShell 5.1，它對 .ps1 的預設解碼是 ANSI，
			// 無 BOM 的 UTF-8 會把中文註釋讀成亂碼，進而吞掉引號、整份腳本語法錯誤。
			File.WriteAllText((str)P.GuestScript, GuestScriptText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			File.WriteAllText((str)P.GuestArgs, MkGuestArgs(O), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}

		Console.WriteLine("[DeployWinVm] staging 完成：" + P.OutRoot);
		return NIL;
	}

	private static async partial Task<nil> CopyToGuest(WinVmOpts O, WinVmPaths P, CT Ct){
		var InDir = O.WorkDir + @"\in";

		// 收件目錄必須先存在，否則 vmrun 的複製會「回 0 但什麼都沒寫」。
		// 這裡刻意不用 cmd.exe：vmrun 在 session 0 起的 cmd 一律回 1（無輸出），
		// 而 powershell.exe 在同一個 session 正常，故一律走 PowerShell。
		await GuestPowerShell(O,
			$"New-Item -ItemType Directory -Force -Path '{InDir}' | Out-Null",
			60, Ct);

		foreach(var (Local, Remote) in new (Pth, str)[]{
			(P.TsfZip, InDir + @"\tsf.zip"),
			(P.RegZip, InDir + @"\reg.zip"),
			(P.HostZip, InDir + @"\host.zip"),
			(P.GuestScript, InDir + @"\deploy.ps1"),
			(P.GuestArgs, InDir + @"\deploy-args.txt"),
		}){
			var Size = new FileInfo((str)Local).Length;
			Console.WriteLine($"[DeployWinVm] 複製 {BaseName(Local)}（{Size / 1024} KB）→ {Remote}");
			await CopyFileToGuest(O, (str)Local, Remote, Size, Ct);
		}
		// 剛寫完的檔案偶爾會被 guest 端的防毒／索引短暫鎖住，直接執行會得到莫名其妙的 exit 1。
		await Task.Delay(2000, Ct);
		return NIL;
	}

	private static async partial Task<nil> RunGuestDeploy(WinVmOpts O, WinVmPaths P, CT Ct){
		Console.WriteLine("[DeployWinVm] 在 guest 內部署（停舊宿主 → 解包 → 註冊 → 啟動 → 煙霧測試）…");
		// 直接叫 powershell.exe，不經 cmd：實測 vmrun 在 session 0 起的 cmd.exe 一律 exit 1。
		// 同一段參數偶爾會在「剛複製完檔案」時瞬間失敗（日志檔都沒更新），故最多重試兩次。
		for(var Attempt = 1; Attempt <= 2; Attempt++){
			// 用 -Command 而不是 -File：實測在同一條 vmrun 呼叫路徑上，-File 會以 1 立刻結束
			// （連腳本第一行都沒跑到），而 -Command 正常。語義相同，故這裡選能用的那個。
			var Script = O.WorkDir + @"\in\deploy.ps1";
			var Exit = await VmRunRaw(O, GuestArgs(O, "runProgramInGuest", O.VmxPath,
				@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
				"-NoProfile", "-ExecutionPolicy", "Bypass",
				"-Command", "& '" + Script + "'; exit $LASTEXITCODE"), 1800, Ct);
			if(Exit.Exit.IsSuccess){
				return NIL;
			}
			Console.Error.WriteLine($"[DeployWinVm] guest 部署腳本回非零（第 {Attempt} 次）：{Exit.Stdout} {Exit.Stderr}");
			if(Attempt == 1){
				await Task.Delay(3000, Ct);
			}
		}
		return NIL;
	}

	private static async partial Task<nil> CollectLogs(WinVmOpts O, WinVmPaths P, CT Ct){
		var RemoteLog = O.WorkDir + @"\last-run.log";
		// 複製回主機；失敗（例如腳本沒跑起來）就用空日誌，至少讓後續判斷有東西看。
		// 先刪掉舊檔：vmrun 抓取有時「回 0 但沒寫檔」，留著舊檔會讓人以為這次跑過了。
		if(File.Exists((str)P.LastRunLog)){
			File.Delete((str)P.LastRunLog);
		}
		try{
			await VmRun(O, GuestArgs(O, "copyFileFromGuestToHost", O.VmxPath, RemoteLog, (str)P.LastRunLog), Ct, TimeoutSec: 180);
		}catch(Exception Ex){
			Console.Error.WriteLine($"[DeployWinVm] 抓回 guest 日誌失敗：{Ex.Message}");
		}

		if(!IsFile(P.LastRunLog)){
			Console.Error.WriteLine($"[DeployWinVm] 找不到 guest 日誌：{RemoteLog}");
			Environment.ExitCode = 1;
			return NIL;
		}

		var Text = File.ReadAllText((str)P.LastRunLog, Encoding.UTF8);
		Console.WriteLine();
		Console.WriteLine("===== guest 部署日誌（尾段） =====");
		foreach(var Line in Text.Split('\n').TakeLast(40)){
			Console.WriteLine(Line.TrimEnd());
		}
		Console.WriteLine("==================================");

		if(Text.Contains("RESULT=OK", StringComparison.Ordinal)){
			Console.WriteLine("[DeployWinVm] 完成：VM 已就緒，可到桌面開記事本測試。");
			Console.WriteLine($"  日誌：{P.LastRunLog}");
			Environment.ExitCode = 0;
		}else{
			Console.Error.WriteLine("[DeployWinVm] 失敗：guest 腳本沒有回報 RESULT=OK，請看上面的日誌。");
			Environment.ExitCode = 1;
		}
		return NIL;
	}

	/// 建立本次部署的路徑集合。
	private static partial WinVmPaths MkPaths(ISCtx Ctx){
		var Out = Ctx.RootDir/OutRelPath;
		return new WinVmPaths{
			RepoRoot = Ctx.RootDir,
			OutRoot = Out,
			TsfPublish = Out/"tsf",
			RegPublish = Out/"reg",
			HostPublish = Out/"host",
			TsfZip = Out/"tsf.zip",
			RegZip = Out/"reg.zip",
			HostZip = Out/"host.zip",
			GuestScript = Out/"deploy.ps1",
			GuestArgs = Out/"deploy-args.txt",
			LastRunLog = Out/"last-run.log",
		};
	}

	/// 組 vmrun 的主機級參數（不涉及 guest 認證）。
	static IList<str> HostArgs(params str[] Rest){
		var Args = new List<str>{ "-T", "ws" };
		Args.AddRange(Rest);
		return Args;
	}

	/// 組 vmrun 的 guest 操作參數（帳號＋動作＋動作參數）。
	static IList<str> GuestArgs(WinVmOpts O, str Action, params str[] Rest){
		var Args = new List<str>{ "-T", "ws", "-gu", O.GuestUser, "-gp", O.GuestPass, Action };
		Args.AddRange(Rest);
		return Args;
	}

	/// 執行 vmrun 並等到結束；逾時會殺掉子行程並拋，避免整個部署卡死。
	static async Task<CommandTextResult> VmRunRaw(WinVmOpts O, IList<str> Args, i32 TimeoutSec, CT Ct){
		var Cmd = TryCmd(O.VmRunExe, Args);
		using var Cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		try{
			return await Cmd.Text(Cts.Token).WaitAsync(TimeSpan.FromSeconds(TimeoutSec), Ct);
		}catch(TimeoutException){
			Cts.Cancel();
			await Cmd.DisposeAsync();
			throw new TimeoutException(
				$"vmrun 逾時（>{TimeoutSec}s）：{string.Join(' ', Args)}\n"
				+ "常見原因：VMware Tools 的 guest 操作通道卡住；重開 guest 或重啟 VMware Tools 後可恢復。");
		}
	}

	/// 執行 vmrun，非零退出碼時拋（建置／部署步驟需要嚴格）。
	static async Task<nil> VmRun(WinVmOpts O, IList<str> Args, CT Ct, i32 TimeoutSec = 120){
		var R = await VmRunRaw(O, Args, TimeoutSec, Ct);
		if(!R.Exit.IsSuccess){
			throw new InvalidOperationException($"vmrun 失敗（{R.Exit.ExitCode}）：{R.Stdout} {R.Stderr}");
		}
		return NIL;
	}

	/// 執行 vmrun 並取得 stdout（用於 `list` 這類查詢）。
	static async Task<str> VmRunText(WinVmOpts O, IList<str> Args, CT Ct){
		var R = await VmRunRaw(O, Args, 60, Ct);
		return R.Stdout;
	}

	/// 在 guest 內以 PowerShell 跑一段命令；回傳退出碼是否為 0。
	static async Task<bool> GuestPowerShell(WinVmOpts O, str Command, i32 TimeoutSec, CT Ct){
		// 每個開關都必須是獨立參數：若全部塞進同一個字串，PowerShell 會把它當成
		// 一個未知的參數名，直接以非零碼結束（大小驗證因此永遠失敗）。
		var R = await VmRunRaw(O, GuestArgs(O, "runProgramInGuest", O.VmxPath,
			@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
			"-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", Command), TimeoutSec, Ct);
		return R.Exit.IsSuccess;
	}

	/// 複製單一檔案進 guest，並用「guest 端檔案大小」驗證；不符就重試一次。
	static async Task<nil> CopyFileToGuest(WinVmOpts O, str Local, str Remote, i64 Size, CT Ct){
		for(var Attempt = 1; Attempt <= 2; Attempt++){
			try{
				await VmRun(O, GuestArgs(O, "copyFileFromHostToGuest", O.VmxPath, Local, Remote), Ct, TimeoutSec: 180);
			}catch(TimeoutException) when(Attempt == 1){
				// 實測 vmrun 有「檔案已落地但行程不返回」的情況，故逾時後仍要驗證一次。
				Console.Error.WriteLine($"[DeployWinVm] 複製逾時，驗證 guest 端是否已收到：{Remote}");
			}
			if(await GuestFileSizeEquals(O, Remote, Size, Ct)){
				return NIL;
			}
			if(Attempt == 1){
				Console.Error.WriteLine($"[DeployWinVm] 大小不符，重試一次：{Remote}");
			}
		}
		throw new InvalidOperationException($"複製到 guest 失敗或大小不符：{Remote}（期望 {Size} bytes）");
	}

	/// 用 guest 端的 PowerShell 檢查檔案大小；相符回 true。
	static async Task<bool> GuestFileSizeEquals(WinVmOpts O, str Remote, i64 Size, CT Ct){
		var Cmd = $"try{{ if((Get-Item -LiteralPath '{Remote}').Length -eq {Size}){{exit 0}} else {{exit 2}} }}catch{{exit 3}}";
		return await GuestPowerShell(O, Cmd, 60, Ct);
	}

	/// 判斷是否需要重建：產物不存在，或任一源碼檔比它新。
	static bool NeedBuild(WinVmPaths P, Pth Marker, params str[] SrcRelPaths){
		if(!IsFile(Marker)){
			return true;
		}
		var MarkerTime = File.GetLastWriteTimeUtc((str)Marker);
		foreach(var Rel in SrcRelPaths){
			var Dir = (str)(P.RepoRoot/Rel);
			if(!Directory.Exists(Dir)){ continue; }
			foreach(var FilePath in Directory.EnumerateFiles(Dir, "*.*", SearchOption.AllDirectories)){
				var Lower = FilePath.Replace('\\', '/').ToLowerInvariant();
				if(Lower.Contains("/bin/") || Lower.Contains("/obj/")){ continue; }
				if(!(Lower.EndsWith(".cs") || Lower.EndsWith(".csproj") || Lower.EndsWith(".props")
					|| Lower.EndsWith(".targets") || Lower.EndsWith(".jsonc") || Lower.EndsWith(".axaml"))){
					continue;
				}
				if(File.GetLastWriteTimeUtc(FilePath) > MarkerTime){
					return true;
				}
			}
		}
		return false;
	}

	/// 產生 guest 端參數檔（key=value；避免在 vmrun 參數裡處理引號）。
	private static partial str MkGuestArgs(WinVmOpts O){
		var Lines = new List<str>{
			"WorkDir=" + O.WorkDir,
			"TsfVersion=" + O.TsfVersion,
			"DesktopUser=" + O.DesktopUser,
			"RimeDll=" + O.RimeDll,
			"RimeUserData=" + O.RimeUserData,
			"TaskPrefix=" + O.TaskPrefix,
			"OpenNotepad=" + (O.OpenNotepad ? "1" : "0"),
			"SkipSmoke=" + (O.SkipSmoke ? "1" : "0"),
			"RestartExplorer=" + (O.RestartExplorer ? "1" : "0"),
		};
		return string.Join("\n", Lines) + "\n";
	}

	/// guest 端部署腳本；只有在 guest 內才做得了的事（排程工作、HKLM、以桌面使用者啟動）才用它。
	const str GuestScriptText = """
$ErrorActionPreference = 'Continue'

# ---- 參數 ----
$Cfg = @{}
foreach($Line in Get-Content (Join-Path (Split-Path $PSCommandPath) 'deploy-args.txt')){
    if($Line -match '^\s*#' -or $Line -notmatch '='){ continue }
    $P = $Line.Split('=', 2)
    $Cfg[$P[0].Trim()] = $P[1].Trim()
}
if([string]::IsNullOrWhiteSpace($Cfg.WorkDir)){ Write-Error 'deploy-args.txt 解析失敗（WorkDir 為空）'; exit 2 }
$WorkDir = $Cfg.WorkDir
$In      = Join-Path $WorkDir 'in'
$Apps    = Join-Path $WorkDir 'apps'
$HostDir = Join-Path $Apps 'ngan-host-x86'
$TsfDir  = Join-Path $Apps 'ngan-tsf'
$RegDir  = Join-Path $Apps 'ngan-reg'
$Log     = Join-Path $WorkDir 'last-run.log'

Set-Content -Path $Log -Value ('deploy ' + (Get-Date).ToString('o')) -Encoding utf8
function Log([string]$M){ Add-Content -Path $Log -Value ((Get-Date).ToString('HH:mm:ss') + ' ' + $M) -Encoding utf8 }
function Fail([string]$M){ Log ('RESULT=FAIL ' + $M); exit 1 }

# 排程工作的動作一律是「跑一支會自我記錄的 .ps1」。
# 不用 cmd /c 加 `>` 重定向：PowerShell 的 -File 不吃 `>`，而 vmrun 在 session 0 起的 cmd 會直接失敗。
function New-PsTask([string]$Name, [string]$ScriptPath, [string]$UserId, [string]$LogonType, [string]$RunLevel){
    $Act = New-ScheduledTaskAction -Execute 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' `
        -Argument ('-NoProfile -ExecutionPolicy Bypass -File "' + $ScriptPath + '"')
    $Prn = New-ScheduledTaskPrincipal -UserId $UserId -LogonType $LogonType -RunLevel $RunLevel
    Register-ScheduledTask -TaskName $Name -Action $Act -Principal $Prn -Force | Out-Null
}

foreach($D in @($In, $Apps, $HostDir, $TsfDir, $RegDir)){ New-Item -ItemType Directory -Force -Path $D | Out-Null }

# ---- 1 停舊宿主 ----
Log '停止舊宿主'
Get-Process -Name 'Ngan.Ime.Windows' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# ---- 2 解包 ----
Log '解包產物'
try {
    Get-ChildItem -Path $HostDir -Recurse -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Expand-Archive -Path (Join-Path $In 'host.zip') -DestinationPath $HostDir -Force
} catch { Fail ('解包宿主失敗：' + $_.Exception.Message) }
try { Expand-Archive -Path (Join-Path $In 'reg.zip') -DestinationPath $RegDir -Force } catch { Fail ('解包註冊工具失敗：' + $_.Exception.Message) }
try { Expand-Archive -Path (Join-Path $In 'tsf.zip') -DestinationPath $TsfDir -Force } catch { Fail ('解包 TSF 失敗：' + $_.Exception.Message) }

# ---- 2.5 修正宿主配置裡的本機路徑 ----
# 部署包裡的 jsonc 是在主機上跑的配置，DllPath 之類會指向主機路徑；
# 直接啟動會得到 DllNotFoundException 並讓宿主當場退出，故在 guest 內改寫成本機路徑。
$CfgFile = Join-Path $HostDir 'Ngan.Ime.Rw.jsonc'
if(Test-Path $CfgFile){
    try {
        $Json = Get-Content $CfgFile -Raw -Encoding utf8 | ConvertFrom-Json
        $Json.Librime.DllPath = $Cfg.RimeDll
        $Json.Librime.RimeTraits.user_data_dir = $Cfg.RimeUserData
        $RimeLogDir = Join-Path $WorkDir 'rime-log'
        New-Item -ItemType Directory -Force -Path $RimeLogDir | Out-Null
        $Json.Librime.RimeTraits.log_dir = $RimeLogDir
        ($Json | ConvertTo-Json -Depth 12) | Set-Content -Path $CfgFile -Encoding utf8
        Log ('已改寫配置：DllPath=' + $Cfg.RimeDll)
    } catch { Fail ('改寫配置失敗：' + $_.Exception.Message) }
} else {
    Log '警告：部署包沒有 Ngan.Ime.Rw.jsonc，將改用環境變數與默認值'
}
if(-not (Test-Path $Cfg.RimeDll)){ Fail ('guest 缺少 rime.dll：' + $Cfg.RimeDll) }

# ---- 3 TSF DLL 版本化：被 explorer 載入的檔案不能覆寫 ----
$TsfSrc = Join-Path $TsfDir 'Ngan.Ime.Tsf.dll'
if(-not (Test-Path $TsfSrc)){ Fail ('TSF 產物缺少 Ngan.Ime.Tsf.dll：' + $TsfDir) }
$TsfDll = Join-Path $TsfDir ('Ngan.Ime.Tsf.' + $Cfg.TsfVersion + '.dll')
Copy-Item -Path $TsfSrc -Destination $TsfDll -Force
Log ('TSF DLL = ' + $TsfDll)

# ---- 4 系統註冊（SYSTEM） ----
$Registrar = Join-Path $RegDir 'Ngan.Ime.Tsf.Registration.exe'
if(-not (Test-Path $Registrar)){ Fail ('缺少註冊工具：' + $Registrar) }
$RegLog  = Join-Path $WorkDir 'register.log'
$RegPs1  = Join-Path $In 'do-register.ps1'
@"
`$out = '$RegLog'
& '$Registrar' --register '$TsfDll' *> `$out
'exit=' + `$LASTEXITCODE | Add-Content -Path `$out -Encoding utf8
"@ | Set-Content -Path $RegPs1 -Encoding utf8
New-PsTask -Name ($Cfg.TaskPrefix + '-Register') -ScriptPath $RegPs1 -UserId 'SYSTEM' -LogonType 'ServiceAccount' -RunLevel 'Highest'
Start-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-Register')
Start-Sleep -Seconds 8
Log ('register.log：' + ((Get-Content $RegLog -Encoding utf8 -ErrorAction SilentlyContinue) -join ' | '))

# ---- 5 加入桌面使用者的輸入法清單 ----
$UserLog = Join-Path $WorkDir 'install-user.log'
$UserPs1 = Join-Path $In 'do-install-user.ps1'
@"
`$out = '$UserLog'
& '$Registrar' --install-user *> `$out
'exit=' + `$LASTEXITCODE | Add-Content -Path `$out -Encoding utf8
"@ | Set-Content -Path $UserPs1 -Encoding utf8
New-PsTask -Name ($Cfg.TaskPrefix + '-InstallUser') -ScriptPath $UserPs1 -UserId $Cfg.DesktopUser -LogonType 'Interactive' -RunLevel 'Limited'
Start-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-InstallUser')
Start-Sleep -Seconds 4
Log ('install-user.log：' + ((Get-Content $UserLog -Encoding utf8 -ErrorAction SilentlyContinue) -join ' | '))

# ---- 5.5 讓新註冊的 TSF 生效 ----
# TSF 是「行程載入時」決定的：已開着的記事本／explorer 會繼續用舊 DLL 的邏輯
# （症狀就是方向鍵、Escape 這些新加的路由看起來沒生效）。故這裡結束測試用的記事本，
# 並把仍持有舊版 DLL 的行程列出來，讓使用者知道要重開哪些。
$CurTsf = (Get-Item $TsfDll).FullName
Get-Process -Name 'notepad' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if($Cfg.RestartExplorer -eq '1'){
    Log '重啟 explorer（-RestartExplorer）'
    Get-Process -Name 'explorer' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-Process -FilePath 'C:\Windows\explorer.exe'
    Start-Sleep -Seconds 3
}
$Stale = @()
foreach($P in (Get-Process -ErrorAction SilentlyContinue)){
    try {
        foreach($M in $P.Modules){
            if($M.ModuleName -like 'Ngan.Ime.Tsf*' -and $M.FileName -ne $CurTsf){
                $Stale += ($P.ProcessName + '(' + $P.Id + ')->' + (Split-Path $M.FileName -Leaf))
                break
            }
        }
    } catch { }
}
if($Stale.Count -gt 0){
    Log ('仍載入舊版 TSF（需重開才會用新邏輯）：' + (($Stale | Select-Object -Unique) -join ', '))
} else {
    Log '沒有行程載入舊版 TSF'
}

# ---- 6 以桌面使用者身分常駐啟動宿主 ----
$HostExe = Join-Path $HostDir 'Ngan.Ime.Windows.exe'
if(-not (Test-Path $HostExe)){ Fail ('缺少宿主：' + $HostExe) }
$HostPs1 = Join-Path $In 'do-start-host.ps1'
@"
`$env:NGAN_RIME_DLL = '$($Cfg.RimeDll)'
`$env:NGAN_RIME_USER_DATA = '$($Cfg.RimeUserData)'
Start-Process -FilePath '$HostExe' -ArgumentList '--tsf' -WindowStyle Hidden
"@ | Set-Content -Path $HostPs1 -Encoding utf8
New-PsTask -Name ($Cfg.TaskPrefix + '-Host') -ScriptPath $HostPs1 -UserId $Cfg.DesktopUser -LogonType 'Interactive' -RunLevel 'Limited'
Start-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-Host')
Start-Sleep -Seconds 8

$Proc = Get-Process -Name 'Ngan.Ime.Windows' -ErrorAction SilentlyContinue
if($null -eq $Proc){
    $HostLog = Join-Path $HostDir 'logs/Ngan.Ime.Windows.log'
    if(Test-Path $HostLog){
        Log '宿主日誌尾段：'
        Get-Content $HostLog -Tail 12 -Encoding utf8 | ForEach-Object { Log ('  ' + $_) }
    }
    Fail '宿主沒有起來（行程不存在）'
}
Log ('宿主 pid=' + ($Proc | Select-Object -First 1).Id)

# ---- 7 管道煙霧測試（以桌面使用者身分） ----
if($Cfg.SkipSmoke -ne '1'){
    $SmokeLog = Join-Path $WorkDir 'smoke.log'
    $SmokePs1 = Join-Path $In 'do-smoke.ps1'
    @"
`$out = '$SmokeLog'
try {
    # 宿主是一請求一連線：每次連線只送一個請求，重複使用連線會得到「管道已中斷」。
    function Send-Key([string]`$payload){
        `$c = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'NganIme.Tsf.v1')
        `$c.Connect(5000)
        `$w = New-Object System.IO.StreamWriter(`$c); `$w.AutoFlush = `$true
        `$r = New-Object System.IO.StreamReader(`$c)
        `$w.WriteLine(`$payload)
        `$line = `$r.ReadLine()
        `$c.Dispose()
        return `$line
    }
    `$a = Send-Key 'K|67|0|100|100'
    `$b = Send-Key 'K|32|0|100|100'
    'SMOKE OK: ' + `$a + ' / ' + `$b | Set-Content -Path `$out -Encoding utf8
} catch {
    'SMOKE FAIL: ' + `$_.Exception.Message | Set-Content -Path `$out -Encoding utf8
}
"@ | Set-Content -Path $SmokePs1 -Encoding utf8
    New-PsTask -Name ($Cfg.TaskPrefix + '-Smoke') -ScriptPath $SmokePs1 -UserId $Cfg.DesktopUser -LogonType 'Interactive' -RunLevel 'Limited'
    Start-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-Smoke')
    Start-Sleep -Seconds 8
    $Smoke = (Get-Content $SmokeLog -Encoding utf8 -ErrorAction SilentlyContinue) -join ' | '
    Log ('smoke：' + $Smoke)
    if($Smoke -notmatch 'SMOKE OK'){ Fail ('管道煙霧測試失敗：' + $Smoke) }
}

# ---- 8 可選：開記事本 ----
if($Cfg.OpenNotepad -eq '1'){
    $NpPs1 = Join-Path $In 'do-notepad.ps1'
    @"
Start-Process -FilePath 'C:\Windows\System32\notepad.exe'
"@ | Set-Content -Path $NpPs1 -Encoding utf8
    New-PsTask -Name ($Cfg.TaskPrefix + '-Notepad') -ScriptPath $NpPs1 -UserId $Cfg.DesktopUser -LogonType 'Interactive' -RunLevel 'Limited'
    Start-ScheduledTask -TaskName ($Cfg.TaskPrefix + '-Notepad')
    Log '已開記事本'
}

# ---- 9 清理太舊的 TSF DLL（保留最近 3 個；被載入的刪不掉就忽略） ----
Get-ChildItem -Path $TsfDir -Filter 'Ngan.Ime.Tsf.*.dll' -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -Skip 3 |
    ForEach-Object { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue }

Log 'RESULT=OK'
exit 0
""";
}
