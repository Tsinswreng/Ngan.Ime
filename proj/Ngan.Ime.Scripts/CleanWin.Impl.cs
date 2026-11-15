using System.Diagnostics;
using Microsoft.Win32;
using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace Ngan.Ime.Scripts;

/// CleanWin 的流程實現。
internal static partial class CleanWin{

	/// 安裝根目錄名（在使用者本機資料下）。
	const str InstallRootName = "NganIme";

	/// 版本目錄名的前綴；安裝腳本用 `v<yyyyMMdd-HHmmss>`。
	const str VersionDirPrefix = "v";

	/// 排程工作名的前綴；`NganIme-Server-v<版本>`。
	const str TaskNamePrefix = "NganIme-Server-";

	/// 舊版安裝腳本用的固定排程工作名（沒有版本後綴）。
	const str LegacyTaskName = "NganIme-Server";

	/// 註冊工具檔名；反註冊要用它自己的 identity.txt。
	const str RegistrarFileName = "Ngan.Ime.Tsf.Registration.exe";

	/// TSF 外殼的 CLSID 註冊樹（機器層）。
	const str ClsidRootKey = @"SOFTWARE\Classes\CLSID";

	/// 註冊表裏指向外殼 DLL 的值名（預設值）。
	const str InprocServer32KeyName = "InprocServer32";

	internal static partial Task<nil> Main(ISCtx Ctx, CT Ct){
		var Opts = ParseOpts();
		var Root = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			InstallRootName);
		if(!Directory.Exists(Root)){
			Console.WriteLine($"[CleanWin] 沒有安裝目錄：{Root}");
			return Task.FromResult(NIL);
		}

		var Registered = ListRegisteredShells(Root);
		Console.WriteLine($"[CleanWin] 安裝根目錄：{Root}");
		Console.WriteLine($"[CleanWin] 註冊表裏指向安裝根目錄的外殼：{Registered.Count} 個");
		foreach(var One in Registered){
			Console.WriteLine($"  已註冊：{One.Key}（{One.Value.Count} 個 CLSID）");
		}

		var Dirs = ListVersionDirs(Root);
		// 「最新版」＝目錄名排序最後一個（安裝腳本用 `v<時間戳>`，排序即時間）。
		var Newest = Dirs.Count > 0 ? Dirs[^1] : "";
		var LegacyTask = QueryLegacyTask();
		Console.WriteLine($"[CleanWin] 最新版本：{Newest}");
		if(Opts.UninstallOld){
			Console.WriteLine("[CleanWin] -UninstallOld：除了最新版以外的每一版都會先反註冊、再嘗試刪除。");
		}

		var Removed = 0;
		var Kept = 0;
		var Unregistered = 0;
		foreach(var Dir in Dirs){
			var IsNewest = string.Equals(Dir, Newest, StringComparison.OrdinalIgnoreCase);
			var IsRegistered = Registered.ContainsKey(Dir);
			var HasTask = HasScheduledTask(Dir, LegacyTask);

			// step 1: 先處理「舊版還在註冊表與排程工作裏」這件事，否則下面的保留條件
			// 會讓每個裝過的版本永遠留着（每次安裝都多一個輸入法條目與一個自啟服務端）。
			if(Opts.UninstallOld && !IsNewest){
				if(Opts.DryRun){
					Console.WriteLine($"  將反註冊（舊版）：{Dir}");
				}
				else{
					if(IsRegistered && UnregisterVersion(Dir)){
						IsRegistered = false;
						Unregistered++;
					}
					RemoveScheduledTasks(Dir, LegacyTask);
					HasTask = false;
				}
			}

			// step 2: 三個判據都要看：註冊着、載入中、有排程工作。少任何一個都會誤刪。
			var Holders = FindHolders(Dir);
			if(IsRegistered || Holders.Count > 0 || HasTask){
				Console.WriteLine($"  保留：{Dir}");
				if(IsRegistered){
					Console.WriteLine(Opts.DryRun && Opts.UninstallOld && !IsNewest
						? "      仍在註冊表中（-DryRun 沒有實際反註冊）"
						: "      仍在註冊表中（這個版本是「已安裝」狀態）");
				}
				if(HasTask){
					Console.WriteLine(IsNewest
						? "      有排程工作（登入時自動啟動）"
						: "      仍有排程工作；-UninstallOld 會移除它");
				}
				foreach(var One in Holders){
					Console.WriteLine($"      被載入：{One}");
				}
				Kept++;
				continue;
			}
			if(Opts.DryRun){
				Console.WriteLine($"  將刪除：{Dir}");
				continue;
			}
			try{
				Directory.Delete(Dir, recursive: true);
				Console.WriteLine($"  已刪除：{Dir}");
				Removed++;
			}catch(Exception Ex){
				Console.WriteLine($"  刪不掉：{Dir}（{Ex.GetType().Name}）");
			}
		}

		if(Opts.UninstallOld){
			Console.WriteLine(Opts.DryRun
				? "[CleanWin] -DryRun：以上「將反註冊」只是計劃，沒有真的改註冊表。"
				: $"[CleanWin] 已反註冊 {Unregistered} 個舊版本。");
			if(Unregistered == 0 && !Opts.DryRun){
				Console.WriteLine("[CleanWin] 提示：反註冊要寫 HKLM，請以管理員身分執行；否則上面會顯示「仍在註冊表中」。");
			}
		}
		Console.WriteLine($"[CleanWin] 刪除 {Removed} 個、保留 {Kept} 個。");
		return Task.FromResult(NIL);
	}

	private static partial CleanOpts ParseOpts(){
		var Opts = new CleanOpts();
		foreach(var Arg in Environment.GetCommandLineArgs().Skip(1)){
			if(!Arg.StartsWith("-", StringComparison.Ordinal)){
				continue;
			}
			switch(Arg.TrimStart('-').ToLowerInvariant()){
				case "dryrun":
					Opts.DryRun = true;
					break;
				case "uninstallold":
					Opts.UninstallOld = true;
					break;
				case "help":
				case "h":
					Console.Error.WriteLine("用法: dotnet run --project proj/Ngan.Ime.Scripts -- CleanWin [-DryRun] [-UninstallOld]");
					Console.Error.WriteLine("  -DryRun        只印結果，不反註冊、不刪除。");
					Console.Error.WriteLine("  -UninstallOld  除了最新版以外的每一版都先反註冊（需管理員）、移除排程工作，再刪目錄。");
					Environment.Exit(0);
					break;
				default:
					throw new ArgumentException($"未知選項：{Arg}。可用：-DryRun");
			}
		}
		return Opts;
	}

	private static partial List<str> FindHolders(str Dir){
		var Result = new List<str>();
		foreach(var Proc in Process.GetProcesses()){
			try{
				if(Proc.Id == Environment.ProcessId){
					continue;
				}
				// 先看主模組：服務端自己就是以該目錄的 exe 啟動的，它不一定出現在 Modules 裏
				// （64 位元行程看 32 位元行程時只有 64 位元的模組檢視）。
				var Main = Proc.MainModule;
				if(Main?.FileName is {} MainPath && IsUnder(MainPath, Dir)){
					Result.Add($"{Proc.ProcessName}（pid={Proc.Id}，主模組）");
					continue;
				}
				foreach(ProcessModule Module in Proc.Modules){
					if(Module.FileName is {} File && IsUnder(File, Dir)){
						Result.Add($"{Proc.ProcessName}（pid={Proc.Id}）");
						break;
					}
				}
			}catch{
				// 讀不到模組清單的行程（受保護或正在結束）不影響其他行程的判斷；
				// 但「讀不到」不等於「沒在用」，所以稍後還要用註冊表與排程工作交叉確認。
			}
		}
		return Result;
	}

	/// 路徑是否在指定目錄底下（不分大小寫、以目錄邊界判斷）。
	/// <remarks>參數刻意不叫 `Path`：那會遮蔽 <see cref="System.IO.Path"/>，編譯時很難看出來。</remarks>
	static bool IsUnder(str FilePath, str Dir){
		var NormalizedDir = Dir.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
		return FilePath.StartsWith(NormalizedDir, StringComparison.OrdinalIgnoreCase)
			|| FilePath.Equals(Dir, StringComparison.OrdinalIgnoreCase);
	}

	private static partial List<str> ListVersionDirs(str Root){
		return Directory.EnumerateDirectories(Root)
			.Where(One => Path.GetFileName(One).StartsWith(VersionDirPrefix, StringComparison.OrdinalIgnoreCase))
			.OrderBy(One => One, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static partial Dictionary<str, List<str>> ListRegisteredShells(str Root){
		var Result = new Dictionary<str, List<str>>(StringComparer.OrdinalIgnoreCase);
		try{
			using var ClsidRoot = Registry.LocalMachine.OpenSubKey(ClsidRootKey);
			if(ClsidRoot is null){
				return Result;
			}
			foreach(var Clsid in ClsidRoot.GetSubKeyNames()){
				using var Server = ClsidRoot.OpenSubKey(Clsid + "\\" + InprocServer32KeyName);
				if(Server?.GetValue(null) is not str Path || Path.Length == 0){
					continue;
				}
				var Dir = System.IO.Path.GetDirectoryName(Path);
				if(string.IsNullOrEmpty(Dir) || !IsUnder(Dir, Root)){
					continue;
				}
				if(!Result.TryGetValue(Dir, out var List)){
					List = [];
					Result[Dir] = List;
				}
				List.Add(Clsid);
			}
		}catch(Exception Ex){
			// 讀不到 HKLM 不算致命：至少「被載入」與「有排程工作」兩條還在，
			// 但那兩條不足以判斷「已安裝」，所以要明確警告。
			Console.Error.WriteLine($"[CleanWin] 讀取註冊表失敗，這次判斷可能不完整：{Ex.Message}");
		}
		return Result;
	}

	/// 版本目錄對應的排程工作名。
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
			}
			else{
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
		var Registrar = Path.Combine(Dir, RegistrarFileName);
		if(!File.Exists(Registrar)){
			Console.WriteLine($"      找不到註冊工具，無法反註冊這個版本：{Registrar}");
			return false;
		}
		// 先用它自己的 identity.txt 從使用者清單移除，再拆機器層註冊。
		// 兩步都失敗（例如沒提權）就回 false，讓呼叫端繼續把「仍註冊中」印出來。
		var UserOk = RunTool(Registrar, ["--uninstall-user"], out var UserExit);
		var SystemOk = RunTool(Registrar, ["--unregister-system"], out var SystemExit);
		if(!UserOk || UserExit != 0){
			Console.WriteLine($"      從使用者清單移除失敗（exit={UserExit}）；可能需要管理員或該版本已不在清單中。");
		}
		if(!SystemOk || SystemExit != 0){
			Console.WriteLine($"      移除機器層註冊失敗（exit={SystemExit}）；請以管理員身分重跑。");
			return false;
		}
		Console.WriteLine($"      已反註冊：{Dir}");
		return true;
	}

	/// <summary>執行一個外部工具並取得結束碼；起不來時回 false。</summary>
	static bool RunTool(str Exe, str[] Args, out i32 ExitCode){
		ExitCode = -1;
		try{
			var Info = new ProcessStartInfo(Exe){
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			foreach(var One in Args){
				Info.ArgumentList.Add(One);
			}
			using var Proc = Process.Start(Info);
			if(Proc is null){
				return false;
			}
			// 一定要讀掉輸出，否則子行程可能卡在寫滿的管線上。
			var Output = Proc.StandardOutput.ReadToEnd() + Proc.StandardError.ReadToEnd();
			if(!Proc.WaitForExit(30_000)){
				return false;
			}
			ExitCode = Proc.ExitCode;
			if(Output.Length > 0){
				foreach(var Line in Output.Split('\n')){
					var Trimmed = Line.Trim();
					if(Trimmed.Length > 0){
						Console.WriteLine("        " + Trimmed);
					}
				}
			}
			return true;
		}catch(Exception Ex){
			Console.WriteLine($"      執行失敗：{Ex.GetType().Name}");
			return false;
		}
	}

	/// <summary>執行 schtasks 並回傳輸出；非 0 結束碼回 null。</summary>
	static str? RunSchtasks(str[] Args){
		try{
			var Info = new ProcessStartInfo("schtasks"){
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			foreach(var One in Args){
				Info.ArgumentList.Add(One);
			}
			using var Proc = Process.Start(Info);
			if(Proc is null){
				return null;
			}
			var Output = Proc.StandardOutput.ReadToEnd() + Proc.StandardError.ReadToEnd();
			if(!Proc.WaitForExit(10_000)){
				return null;
			}
			return Proc.ExitCode == 0 ? Output : null;
		}catch{
			return null;
		}
	}
}
