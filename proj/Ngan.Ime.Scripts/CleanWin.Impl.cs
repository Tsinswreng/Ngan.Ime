using Tsinswreng.CsCore;
using System.Diagnostics;
using Microsoft.Win32;
using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace Ngan.Ime.Scripts;

[Doc($$"""
CleanWin 的流程實現。
""")]
internal static partial class CleanWin{

	[Doc($$"""
	安裝根目錄名（在使用者本機資料下）。
	""")]
	const str InstallRootName = "NganIme";

	[Doc($$"""
	版本目錄名的前綴；安裝腳本用 `v<yyyyMMdd-HHmmss>`。
	""")]
	const str VersionDirPrefix = "v";

	[Doc($$"""
	排程工作名的前綴；`NganIme-Server-v<版本>`。
	""")]
	const str TaskNamePrefix = "NganIme-Server-";

	[Doc($$"""
	舊版安裝腳本用的固定排程工作名（沒有版本後綴）。
	""")]
	const str LegacyTaskName = "NganIme-Server";

	[Doc($$"""
	註冊工具檔名；反註冊要用它自己的 identity.txt。
	""")]
	const str RegistrarFileName = "Ngan.Ime.Tsf.Registration.exe";

	[Doc($$"""
	TSF 外殼的 CLSID 註冊樹（機器層）。
	""")]
	const str ClsidRootKey = @"SOFTWARE\Classes\CLSID";

	[Doc($$"""
	註冊表裏指向外殼 DLL 的值名（預設值）。
	""")]
	const str InprocServer32KeyName = "InprocServer32";

	internal static partial Task<nil> Main(ISCtx Ctx, CT Ct){
		var opts = ParseOpts();
		var root = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			InstallRootName);
		if(!Directory.Exists(root)){
			Console.WriteLine($"[CleanWin] 沒有安裝目錄：{root}");
			return Task.FromResult(NIL);
		}

		var registered = ListRegisteredShells(root);
		Console.WriteLine($"[CleanWin] 安裝根目錄：{root}");
		Console.WriteLine($"[CleanWin] 註冊表裏指向安裝根目錄的外殼：{registered.Count} 個");
		foreach(var one in registered){
			Console.WriteLine($"  已註冊：{one.Key}（{one.Value.Count} 個 CLSID）");
		}

		var dirs = ListVersionDirs(root);
		// 「最新版」＝目錄名排序最後一個（安裝腳本用 `v<時間戳>`，排序即時間）。
		var newest = dirs.Count > 0 ? dirs[^1] : "";
		var legacyTask = QueryLegacyTask();
		Console.WriteLine($"[CleanWin] 最新版本：{newest}");
		if(opts.UninstallOld){
			Console.WriteLine("[CleanWin] -UninstallOld：除了最新版以外的每一版都會先反註冊、再嘗試刪除。");
		}

		var removed = 0;
		var kept = 0;
		var unregistered = 0;
		foreach(var dir in dirs){
			var isNewest = str.Equals(dir, newest, StringComparison.OrdinalIgnoreCase);
			var isRegistered = registered.ContainsKey(dir);
			var hasTask = HasScheduledTask(dir, legacyTask);

			// step 1: 先處理「舊版還在註冊表與排程工作裏」這件事，否則下面的保留條件
			// 會讓每個裝過的版本永遠留着（每次安裝都多一個輸入法條目與一個自啟服務端）。
			if(opts.UninstallOld && !isNewest){
				if(opts.DryRun){
					Console.WriteLine($"  將反註冊（舊版）：{dir}");
				} else{
					if(isRegistered && UnregisterVersion(dir)){
						isRegistered = false;
						unregistered++;
					}
					RemoveScheduledTasks(dir, legacyTask);
					hasTask = false;
				}
			}

			// step 2: 三個判據都要看：註冊着、載入中、有排程工作。少任何一個都會誤刪。
			var holders = FindHolders(dir);
			if(isRegistered || holders.Count > 0 || hasTask){
				Console.WriteLine($"  保留：{dir}");
				if(isRegistered){
					Console.WriteLine(opts.DryRun && opts.UninstallOld && !isNewest
						? "      仍在註冊表中（-DryRun 沒有實際反註冊）"
						: "      仍在註冊表中（這個版本是「已安裝」狀態）");
				}
				if(hasTask){
					Console.WriteLine(isNewest
						? "      有排程工作（登入時自動啟動）"
						: "      仍有排程工作；-UninstallOld 會移除它");
				}
				foreach(var one in holders){
					Console.WriteLine($"      被載入：{one}");
				}
				kept++;
				continue;
			}
			if(opts.DryRun){
				Console.WriteLine($"  將刪除：{dir}");
				continue;
			}
			try{
				Directory.Delete(dir, recursive: true);
				Console.WriteLine($"  已刪除：{dir}");
				removed++;
			}catch(Exception Ex){
				Console.WriteLine($"  刪不掉：{dir}（{Ex.GetType().Name}）");
			}
		}

		if(opts.UninstallOld){
			Console.WriteLine(opts.DryRun
				? "[CleanWin] -DryRun：以上「將反註冊」只是計劃，沒有真的改註冊表。"
				: $"[CleanWin] 已反註冊 {unregistered} 個舊版本。");
			if(unregistered == 0 && !opts.DryRun){
				Console.WriteLine("[CleanWin] 提示：反註冊要寫 HKLM，請以管理員身分執行；否則上面會顯示「仍在註冊表中」。");
			}
		}
		Console.WriteLine($"[CleanWin] 刪除 {removed} 個、保留 {kept} 個。");
		return Task.FromResult(NIL);
	}

	private static partial CleanOpts ParseOpts(){
		var opts = new CleanOpts();
		foreach(var arg in Environment.GetCommandLineArgs().Skip(1)){
			if(!arg.StartsWith("-", StringComparison.Ordinal)){
				continue;
			}
			switch(arg.TrimStart('-').ToLowerInvariant()){
				case "dryrun":
					opts.DryRun = true;
					break;
				case "uninstallold":
					opts.UninstallOld = true;
					break;
				case "help":
				case "h":
					Console.Error.WriteLine("用法: dotnet run --project proj/Ngan.Ime.Scripts -- CleanWin [-DryRun] [-UninstallOld]");
					Console.Error.WriteLine("  -DryRun        只印結果，不反註冊、不刪除。");
					Console.Error.WriteLine("  -UninstallOld  除了最新版以外的每一版都先反註冊（需管理員）、移除排程工作，再刪目錄。");
					Environment.Exit(0);
					break;
				default:
					throw new ArgumentException($"未知選項：{arg}。可用：-DryRun");
			}
		}
		return opts;
	}

	private static partial List<str> FindHolders(str Dir){
		var result = new List<str>();
		foreach(var proc in Process.GetProcesses()){
			try{
				if(proc.Id == Environment.ProcessId){
					continue;
				}
				// 先看主模組：服務端自己就是以該目錄的 exe 啟動的，它不一定出現在 Modules 裏
				// （64 位元行程看 32 位元行程時只有 64 位元的模組檢視）。
				var main = proc.MainModule;
				if(main?.FileName is {} MainPath && IsUnder(MainPath, Dir)){
					result.Add($"{proc.ProcessName}（pid={proc.Id}，主模組）");
					continue;
				}
				foreach(ProcessModule Module in proc.Modules){
					if(Module.FileName is {} File && IsUnder(File, Dir)){
						result.Add($"{proc.ProcessName}（pid={proc.Id}）");
						break;
					}
				}
			}catch{
				// 讀不到模組清單的行程（受保護或正在結束）不影響其他行程的判斷；
				// 但「讀不到」不等於「沒在用」，所以稍後還要用註冊表與排程工作交叉確認。
			}
		}
		return result;
	}

	[Doc($$"""
	路徑是否在指定目錄底下（不分大小寫、以目錄邊界判斷）。
	參數刻意不叫 `Path`：那會遮蔽 {{nameof(System.IO.Path)}}，編譯時很難看出來。
	""")]
	static bool IsUnder(str FilePath, str Dir){
		var normalizedDir = Dir.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
		return FilePath.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase)
			|| FilePath.Equals(Dir, StringComparison.OrdinalIgnoreCase);
	}

	private static partial List<str> ListVersionDirs(str Root){
		return Directory.EnumerateDirectories(Root)
			.Where(One => Path.GetFileName(One).StartsWith(VersionDirPrefix, StringComparison.OrdinalIgnoreCase))
			.OrderBy(One => One, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static partial Dictionary<str, List<str>> ListRegisteredShells(str Root){
		var result = new Dictionary<str, List<str>>(StringComparer.OrdinalIgnoreCase);
		try{
			using var clsidRoot = Registry.LocalMachine.OpenSubKey(ClsidRootKey);
			if(clsidRoot is null){
				return result;
			}
			foreach(var clsid in clsidRoot.GetSubKeyNames()){
				using var server = clsidRoot.OpenSubKey(clsid + "\\" + InprocServer32KeyName);
				if(server?.GetValue(null) is not str Path || Path.Length == 0){
					continue;
				}
				var dir = System.IO.Path.GetDirectoryName(Path);
				if(str.IsNullOrEmpty(dir) || !IsUnder(dir, Root)){
					continue;
				}
				if(!result.TryGetValue(dir, out var list)){
					list = [];
					result[dir] = list;
				}
				list.Add(clsid);
			}
		}catch(Exception Ex){
			// 讀不到 HKLM 不算致命：至少「被載入」與「有排程工作」兩條還在，
			// 但那兩條不足以判斷「已安裝」，所以要明確警告。
			Console.Error.WriteLine($"[CleanWin] 讀取註冊表失敗，這次判斷可能不完整：{Ex.Message}");
		}
		return result;
	}

	[Doc($$"""
	版本目錄對應的排程工作名。
	""")]
	static str TaskNameOf(str Dir){
		return TaskNamePrefix + Path.GetFileName(Dir);
	}

	private static partial bool HasScheduledTask(str Dir, str LegacyTaskText){
		// 用 schtasks 查詢而不是引用 TaskScheduler 套件：本命令是維護工具，
		// 不想為了一個查詢把 COM 任務排程器的互操作拉進來。
		if(RunSchtasks(["/Query", "/TN", TaskNameOf(Dir)]) is not null){
			return true;
		}
		// 舊版安裝腳本只有一個固定名稱的工作（`NganIme-Server`），指向哪一版要看它的內容。
		return LegacyTaskText.Length > 0 && LegacyTaskText.Contains(Dir, StringComparison.OrdinalIgnoreCase);
	}

	private static partial str QueryLegacyTask(){
		return RunSchtasks(["/Query", "/TN", LegacyTaskName, "/FO", "LIST", "/V"]) ?? "";
	}

	private static partial void RemoveScheduledTasks(str Dir, str LegacyTaskText){
		if(RunSchtasks(["/Query", "/TN", TaskNameOf(Dir)]) is not null){
			if(RunSchtasks(["/Delete", "/TN", TaskNameOf(Dir), "/F"]) is not null){
				Console.WriteLine($"      已移除排程工作：{TaskNameOf(Dir)}");
			} else{
				Console.WriteLine($"      排程工作刪不掉：{TaskNameOf(Dir)}（需要管理員？）");
			}
		}
		// 舊版的單一工作：只在它真的指向這個目錄時才刪，否則會拆掉別的版本的自啟。
		if(LegacyTaskText.Length > 0 && LegacyTaskText.Contains(Dir, StringComparison.OrdinalIgnoreCase)){
			if(RunSchtasks(["/Delete", "/TN", LegacyTaskName, "/F"]) is not null){
				Console.WriteLine($"      已移除舊版排程工作：{LegacyTaskName}");
			}
		}
	}

	private static partial bool UnregisterVersion(str Dir){
		var registrar = Path.Combine(Dir, RegistrarFileName);
		if(!File.Exists(registrar)){
			Console.WriteLine($"      找不到註冊工具，無法反註冊這個版本：{registrar}");
			return false;
		}
		// 先用它自己的 identity.txt 從使用者清單移除，再拆機器層註冊。
		// 兩步都失敗（例如沒提權）就回 false，讓呼叫端繼續把「仍註冊中」印出來。
		var userOk = RunTool(registrar, ["--uninstall-user"], out var userExit);
		var systemOk = RunTool(registrar, ["--unregister-system"], out var systemExit);
		if(!userOk || userExit != 0){
			Console.WriteLine($"      從使用者清單移除失敗（exit={userExit}）；可能需要管理員或該版本已不在清單中。");
		}
		if(!systemOk || systemExit != 0){
			Console.WriteLine($"      移除機器層註冊失敗（exit={systemExit}）；請以管理員身分重跑。");
			return false;
		}
		Console.WriteLine($"      已反註冊：{Dir}");
		return true;
	}

	[Doc($$"""
	執行一個外部工具並取得結束碼；起不來時回 false。
	""")]
	static bool RunTool(str Exe, str[] Args, out i32 ExitCode){
		ExitCode = -1;
		try{
			var info = new ProcessStartInfo(Exe){
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			foreach(var one in Args){
				info.ArgumentList.Add(one);
			}
			using var proc = Process.Start(info);
			if(proc is null){
				return false;
			}
			// 一邊讀輸出、一邊等逾時：`ReadToEnd` 是同步阻塞的，子行程若掛住不關 stdout，
			// 那一次呼叫永遠不返回，後面的 `WaitForExit(30s)` 根本走不到（先前就是這樣）。
			// 這裡改成先非同步把兩個串流讀完，再對「行程結束」本身計時。
			var stdoutTask = proc.StandardOutput.ReadToEndAsync();
			var stderrTask = proc.StandardError.ReadToEndAsync();
			if(!proc.WaitForExit(30_000)){
				// 逾時：把行程收掉，避免留下孤兒程序；輸出不再等待。
				try{
					proc.Kill(entireProcessTree: true);
				}catch{
					// 已經結束或無權限：忽略。
				}
				Console.WriteLine($"      外部工具逾時（30 秒）：{Exe}");
				return false;
			}
			var output = stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult();
			ExitCode = proc.ExitCode;
			if(output.Length > 0){
				foreach(var line in output.Split('\n')){
					var trimmed = line.Trim();
					if(trimmed.Length > 0){
						Console.WriteLine("        " + trimmed);
					}
				}
			}
			return true;
		}catch(Exception Ex){
			Console.WriteLine($"      執行失敗：{Ex.GetType().Name}");
			return false;
		}
	}

	[Doc($$"""
	執行 schtasks 並回傳輸出；非 0 結束碼回 null。
	""")]
	static str? RunSchtasks(str[] Args){
		try{
			var info = new ProcessStartInfo("schtasks"){
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			foreach(var one in Args){
				info.ArgumentList.Add(one);
			}
			using var proc = Process.Start(info);
			if(proc is null){
				return null;
			}
			var output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
			if(!proc.WaitForExit(10_000)){
				return null;
			}
			return proc.ExitCode == 0 ? output : null;
		}catch{
			return null;
		}
	}
}
