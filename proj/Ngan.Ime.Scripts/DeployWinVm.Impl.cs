//Impl: 聲明見 DeployWinVm.cs。
namespace Ngan.Ime.Scripts;

using System.IO.Compression;
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
				case "dryrun": O.DryRun = true; break;
				case "help": case "h":
					Console.Error.WriteLine("用法: dotnet run --project proj/Ngan.Ime.Frontend/Ngan.Ime.Scripts -- DeployWinVm [-Fast] [-NoBuild] [-StartVm] [-OpenNotepad] [-SkipSmoke] [-DryRun]");
					Environment.Exit(0);
					break;
				default:
					throw new ArgumentException($"未知選項：-{F}。可用：-Fast -NoBuild -StartVm -OpenNotepad -SkipSmoke -DryRun");
			}
		}
		return O;
	}

	private static async partial Task<nil> EnsureVmRunning(WinVmOpts O, CT Ct){
		var Running = await VmRunText(O, ["list"], Ct);
		var VmxName = Path.GetFileName(O.VmxPath);
		if(!Running.Contains(VmxName, StringComparison.OrdinalIgnoreCase)){
			if(!O.StartVm){
				throw new InvalidOperationException(
					$"VM 未開機：{O.VmxPath}。先手動開機，或加 -StartVm 自動開機（會用 nogui）。");
			}
			Console.WriteLine("[DeployWinVm] VM 未開機，正在啟動（nogui）…");
			await VmRun(O, ["start", O.VmxPath, "nogui"], Ct);
			// 等 VM 進入 running 狀態。
			for(var I = 0; I < 60; I++){
				await Task.Delay(1000, Ct);
				Running = await VmRunText(O, ["list"], Ct);
				if(Running.Contains(VmxName, StringComparison.OrdinalIgnoreCase)){
					break;
				}
			}
		}

		// 等 guest 操作通道（VMware Tools）可用。
		Console.Write("[DeployWinVm] 等待 guest 操作通道");
		for(var I = 0; I < 120; I++){
			var R = await TryCmd(O.VmRunExe, GuestArgs(O, "listProcessesInGuest", O.VmxPath)).Text(Ct);
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
					Cp(Src, P.HostPublish/Name);
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

			Write(P.GuestScript, GuestScriptText);
			Write(P.GuestArgs, MkGuestArgs(O));
		}

		Console.WriteLine("[DeployWinVm] staging 完成：" + P.OutRoot);
		return NIL;
	}

	private static async partial Task<nil> CopyToGuest(WinVmOpts O, WinVmPaths P, CT Ct){
		var InDir = O.WorkDir + @"\in";
		// 建立 guest 收件目錄；已存在時 cmd 會回非零，忽略即可。
		_ = await TryCmd(O.VmRunExe, GuestArgs(O, "runProgramInGuest", O.VmxPath,
			@"C:\Windows\System32\cmd.exe", @"/c mkdir " + InDir)).Text(Ct);

		foreach(var (Local, Remote) in new (Pth, str)[]{
			(P.TsfZip, InDir + @"\tsf.zip"),
			(P.RegZip, InDir + @"\reg.zip"),
			(P.HostZip, InDir + @"\host.zip"),
			(P.GuestScript, InDir + @"\deploy.ps1"),
			(P.GuestArgs, InDir + @"\deploy-args.txt"),
		}){
			Console.WriteLine($"[DeployWinVm] 複製 {BaseName(Local)} → {Remote}");
			await VmRun(O, ["copyFileFromHostToGuest", O.VmxPath, (str)Local, Remote], Ct);
		}
		return NIL;
	}

	private static async partial Task<nil> RunGuestDeploy(WinVmOpts O, WinVmPaths P, CT Ct){
		Console.WriteLine("[DeployWinVm] 在 guest 內部署（停舊宿主 → 解包 → 註冊 → 啟動 → 煙霧測試）…");
		// 經 cmd.exe 轉一手：vmrun 的 program 參數只吃「一個」argument 字串，
		// 直接傳 powershell.exe 會讓 -NoProfile 等開關全部黏成一個 argv 元素；cmd /c 會正確解析整行。
		var Exit = await TryCmd(O.VmRunExe, GuestArgs(O, "runProgramInGuest", O.VmxPath,
			@"C:\Windows\System32\cmd.exe",
			@"/c powershell.exe -NoProfile -ExecutionPolicy Bypass -File " + O.WorkDir + @"\in\deploy.ps1")).Text(Ct);
		if(!Exit.Exit.IsSuccess){
			Console.Error.WriteLine("[DeployWinVm] guest 部署腳本回非零：");
			Console.Error.WriteLine(Exit.Stdout);
			Console.Error.WriteLine(Exit.Stderr);
		}
		return NIL;
	}

	private static async partial Task<nil> CollectLogs(WinVmOpts O, WinVmPaths P, CT Ct){
		var RemoteLog = O.WorkDir + @"\last-run.log";
		// 複製回主機；失敗（例如腳本沒跑起來）就用空日誌，至少讓後續判斷有東西看。
		_ = await TryCmd(O.VmRunExe, GuestArgs(O, "copyFileFromGuestToHost", O.VmxPath, RemoteLog, (str)P.LastRunLog)).Text(Ct);

		if(!IsFile(P.LastRunLog)){
			Console.Error.WriteLine($"[DeployWinVm] 找不到 guest 日誌：{RemoteLog}");
			Environment.ExitCode = 1;
			return NIL;
		}

		var Text = await Read(P.LastRunLog).Text(Ct);
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

	/// 組 vmrun 的 guest 操作參數（帳號＋動作＋動作參數）。
	static IList<str> GuestArgs(WinVmOpts O, str Action, params str[] Rest){
		var Args = new List<str>{ "-T", "ws", "-gu", O.GuestUser, "-gp", O.GuestPass, Action };
		Args.AddRange(Rest);
		return Args;
	}

	/// 執行 vmrun，非零退出碼時拋（建置／部署步驟需要嚴格）。
	static async Task<nil> VmRun(WinVmOpts O, IList<str> Args, CT Ct){
		var Full = new List<str>{ "-T", "ws" };
		Full.AddRange(Args);
		var R = await TryCmd(O.VmRunExe, Full).Text(Ct);
		if(!R.Exit.IsSuccess){
			throw new InvalidOperationException($"vmrun 失敗（{R.Exit.ExitCode}）：{R.Stdout} {R.Stderr}");
		}
		return NIL;
	}

	/// 執行 vmrun 並取得 stdout（用於 `list` 這類查詢）。
	static async Task<str> VmRunText(WinVmOpts O, IList<str> Args, CT Ct){
		var Full = new List<str>{ "-T", "ws" };
		Full.AddRange(Args);
		var R = await TryCmd(O.VmRunExe, Full).Text(Ct);
		return R.Stdout;
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
		};
		return string.Join("\n", Lines) + "\n";
	}

	/// guest 端部署腳本；只有在 guest 內才做得了的事（排程工作、HKLM、以桌面使用者啟動）才用它。
	const str GuestScriptText = """
$ErrorActionPreference = 'Continue'
$Cfg = @{}
foreach($Line in Get-Content (Join-Path (Split-Path $PSCommandPath) 'deploy-args.txt')){
    if($Line -match '^\s*#' -or $Line -notmatch '='){ continue }
    $P = $Line.Split('=', 2)
    $Cfg[$P[0].Trim()] = $P[1].Trim()
}
$WorkDir = $Cfg.WorkDir
$Log = Join-Path $WorkDir 'last-run.log'
Set-Content -Path $Log -Value ('deploy ' + (Get-Date).ToString('o'))
function Log([string]$M){ Add-Content -Path $Log -Value ((Get-Date).ToString('HH:mm:ss') + ' ' + $M) }
function Fail([string]$M){ Log ('RESULT=FAIL ' + $M); exit 1 }

$In = Join-Path $WorkDir 'in'
$Apps = Join-Path $WorkDir 'apps'
$HostDir = Join-Path $Apps 'ngan-host-x86'
$TsfDir = Join-Path $Apps 'ngan-tsf'
$RegDir = Join-Path $Apps 'ngan-reg'
foreach($D in @($In, $Apps, $HostDir, $TsfDir, $RegDir)){ New-Item -ItemType Directory -Force -Path $D | Out-Null }

# 1) 停舊宿主：否則 TSF 可能連到舊行程（等於在測上一版）。
Log '停止舊宿主'
Get-Process -Name 'Ngan.Ime.Windows' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# 2) 解包三個產物；宿主目錄先清空（避免舊 DLL 殘留）。
Log '解包產物'
try {
    Get-ChildItem -Path $HostDir -Recurse -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Expand-Archive -Path (Join-Path $In 'host.zip') -DestinationPath $HostDir -Force
} catch { Fail ('解包宿主失敗：' + $_.Exception.Message) }
try { Expand-Archive -Path (Join-Path $In 'reg.zip') -DestinationPath $RegDir -Force } catch { Fail ('解包註冊工具失敗：' + $_.Exception.Message) }
try { Expand-Archive -Path (Join-Path $In 'tsf.zip') -DestinationPath $TsfDir -Force } catch { Fail ('解包 TSF 失敗：' + $_.Exception.Message) }

# 3) TSF DLL 用版本化檔名：被 explorer 載入的檔案不能覆寫。
$TsfVer = $Cfg.TsfVersion
$TsfSrc = Join-Path $TsfDir 'Ngan.Ime.Tsf.dll'
if(-not (Test-Path $TsfSrc)){ Fail ('TSF 產物缺少 Ngan.Ime.Tsf.dll：' + $TsfDir) }
$TsfDll = Join-Path $TsfDir ('Ngan.Ime.Tsf.' + $TsfVer + '.dll')
Copy-Item -Path $TsfSrc -Destination $TsfDll -Force
Log ('TSF DLL = ' + $TsfDll)

# 4) 系統註冊（SYSTEM 工作，需要寫 HKLM）。
$Registrar = Join-Path $RegDir 'Ngan.Ime.Tsf.Registration.exe'
if(-not (Test-Path $Registrar)){ Fail ('缺少註冊工具：' + $Registrar) }
$Prefix = $Cfg.TaskPrefix
$RegLog = Join-Path $WorkDir 'register.log'
$Act = New-ScheduledTaskAction -Execute 'C:\Windows\System32\cmd.exe' -Argument ('/c "' + $Registrar + '" --register "' + $TsfDll + '" > "' + $RegLog + '" 2>&1')
$Prn = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName ($Prefix + '-Register') -Action $Act -Principal $Prn -Force | Out-Null
Start-ScheduledTask -TaskName ($Prefix + '-Register')
Start-Sleep -Seconds 8
Log ('register.log：' + ((Get-Content $RegLog -ErrorAction SilentlyContinue) -join ' | '))

# 5) 加入桌面使用者的輸入法清單（必須以該使用者身分）。
$UserLog = Join-Path $WorkDir 'install-user.log'
$Act2 = New-ScheduledTaskAction -Execute 'C:\Windows\System32\cmd.exe' -Argument ('/c "' + $Registrar + '" --install-user > "' + $UserLog + '" 2>&1')
$Prn2 = New-ScheduledTaskPrincipal -UserId $Cfg.DesktopUser -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName ($Prefix + '-InstallUser') -Action $Act2 -Principal $Prn2 -Force | Out-Null
Start-ScheduledTask -TaskName ($Prefix + '-InstallUser')
Start-Sleep -Seconds 4
Log ('install-user.log：' + ((Get-Content $UserLog -ErrorAction SilentlyContinue) -join ' | '))

# 6) 寫 host.bat 並以桌面使用者身分常駐啟動。
$HostExe = Join-Path $HostDir 'Ngan.Ime.Windows.exe'
$HostBat = Join-Path $WorkDir 'host.bat'
@(
    '@echo off',
    ('set NGAN_RIME_DLL=' + $Cfg.RimeDll),
    ('set NGAN_RIME_USER_DATA=' + $Cfg.RimeUserData),
    ('start "" /b "' + $HostExe + '" --tsf')
) | Set-Content -Encoding ascii $HostBat
$Act3 = New-ScheduledTaskAction -Execute $HostBat
Register-ScheduledTask -TaskName ($Prefix + '-Host') -Action $Act3 -Principal $Prn2 -Force | Out-Null
Start-ScheduledTask -TaskName ($Prefix + '-Host')
Start-Sleep -Seconds 6

$Proc = Get-Process -Name 'Ngan.Ime.Windows' -ErrorAction SilentlyContinue
if($null -eq $Proc){ Fail '宿主沒有起來（行程不存在），請看 logs 目錄' }
Log ('宿主 pid=' + $Proc.Id)

# 7) 煙霧測試：以桌面使用者連管道送鍵（管道在該使用者的 session 裡）。
if($Cfg.SkipSmoke -ne '1'){
    $SmokePs1 = Join-Path $In 'smoke.ps1'
    @'
$ErrorActionPreference = "Continue"
try {
    $c = New-Object System.IO.Pipes.NamedPipeClientStream(".", "NganIme.Tsf.v1")
    $c.Connect(3000)
    $w = New-Object System.IO.StreamWriter($c); $w.AutoFlush = $true
    $r = New-Object System.IO.StreamReader($c)
    $w.WriteLine("K|67|0|100|100"); $a = $r.ReadLine()
    $w.WriteLine("K|32|0|100|100"); $b = $r.ReadLine()
    $c.Dispose()
    "SMOKE OK: $a / $b"
} catch { "SMOKE FAIL: " + $_.Exception.Message }
'@ | Set-Content -Encoding utf8 $SmokePs1
    $SmokeLog = Join-Path $WorkDir 'smoke.log'
    $Act4 = New-ScheduledTaskAction -Execute 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -Argument ('-NoProfile -ExecutionPolicy Bypass -File "' + $SmokePs1 + '" > "' + $SmokeLog + '" 2>&1')
    Register-ScheduledTask -TaskName ($Prefix + '-Smoke') -Action $Act4 -Principal $Prn2 -Force | Out-Null
    Start-ScheduledTask -TaskName ($Prefix + '-Smoke')
    Start-Sleep -Seconds 8
    $Smoke = (Get-Content $SmokeLog -ErrorAction SilentlyContinue) -join ' | '
    Log ('smoke：' + $Smoke)
    if($Smoke -notmatch 'SMOKE OK'){ Fail ('管道煙霧測試失敗：' + $Smoke) }
}

# 8) 可選：開記事本，方便立刻測。
if($Cfg.OpenNotepad -eq '1'){
    $Act5 = New-ScheduledTaskAction -Execute 'C:\Windows\System32\notepad.exe'
    Register-ScheduledTask -TaskName ($Prefix + '-Notepad') -Action $Act5 -Principal $Prn2 -Force | Out-Null
    Start-ScheduledTask -TaskName ($Prefix + '-Notepad')
    Log '已開記事本'
}

# 9) 清掉太舊的 TSF DLL（保留最近 3 個；刪不掉的通常被 explorer 載入，忽略）。
Get-ChildItem -Path $TsfDir -Filter 'Ngan.Ime.Tsf.*.dll' -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -Skip 3 |
    ForEach-Object { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue }

Log 'RESULT=OK'
exit 0
""";
}
