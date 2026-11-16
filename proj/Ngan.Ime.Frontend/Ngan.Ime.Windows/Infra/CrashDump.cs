namespace Ngan.Ime.Windows.Infra;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

[Doc($$"""
行程內的原生崩潰轉儲：註冊向量化例外處理器，遇到存取違規一類的例外就寫 minidump。
*為何不用 WER 的 LocalDumps*：那個要在 HKLM 設 `LocalDumps\<image>`，需要管理員；
沒有管理員權限時它就是空的，崩了也什麼都不會留下（先前一直踩這個坑）。
這裡改成行程內自己寫，不需要任何權限。

*限制*：`__fastfail`（例如 CRT 的 `abort()`，例外碼 0xC0000409）直接進核心，
使用者模式的處理器根本不會被呼叫，那一類崩潰抓不到；存取違規、堆損壞、堆疊溢位則抓得到。

*為何只寫有限次數*：處理器在崩潰路徑上執行，寫檔本身可能再失敗或再崩；
限制次數並全程吞例外，避免變成一崩再崩的迴圈。
""")]
internal static partial class CrashDump{

	[Doc($$"""
	最多寫幾份；夠定位就行，避免把磁碟塞滿。
	""")]
	private const i32 MaxDumps = 3;

	[Doc($$"""
	已寫出的份數。
	""")]
	private static i32 Written;

	[Doc($$"""
	是否正在寫（避免遞迴）。
	""")]
	private static i32 Busy;

	[Doc($$"""
	轉儲目錄；放在使用者本機資料下，與 rime 日誌分開。
	""")]
	private static str DumpDir => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"NganIme", "dumps");

	[Doc($$"""
	本行程所屬的版本標記（安裝目錄的最後一層，例如 `v20261115-151236`）。
	多個版本可以同時安裝、同時運行；dump 與日誌若不帶版本，
	事後根本分不清是哪一版崩的。開發時目錄可能是 `Debug`／`win-x86／`，
	一律照原樣帶上，總比沒有好。
	""")]
	private static str VersionTag{
		get{
			var dir = AppContext.BaseDirectory.TrimEnd('\\', '/');
			var leaf = Path.GetFileName(dir);
			if(str.IsNullOrWhiteSpace(leaf)){
				return "unknown";
			}
			// 檔名只留安全字元：目錄名理論上可能含空白或中文。
			var sb = new System.Text.StringBuilder(leaf.Length);
			foreach(var one in leaf){
				sb.Append(char.IsLetterOrDigit(one) || one is '-' or '_' ? one : '_');
			}
			return sb.ToString();
		}
	}

	[Doc($$"""
	本次要抓的例外碼：存取違規、堆損壞、堆疊溢位、整除零、非法指令。
	""")]
	private static bool ShouldCapture(u32 Code){
		return Code is 0xC0000005 or 0xC0000374 or 0xC00000FD or 0xC0000094 or 0xC000001D;
	}

	[Doc($$"""
	註冊處理器；重複呼叫無副作用。
	""")]
	public static unsafe void Install(){
		try{
			Directory.CreateDirectory(DumpDir);
			// 1 = 插到處理器鏈最前面，先於其它處理器看到例外。
			AddVectoredExceptionHandler(1, (IntPtr)(delegate* unmanaged<ExceptionPointers*, i32>)&OnException);
		}catch(Exception Ex){
			// 裝不上就算了：沒有轉儲也不能讓程式起不來。
			Console.Error.WriteLine("[CrashDump] 安裝失敗：" + Ex.Message);
		}
	}

	[UnmanagedCallersOnly]
	private static unsafe i32 OnException(ExceptionPointers* Info){
		try{
			if(Info is null || Info->ExceptionRecord is null){
				return ContinueSearch;
			}
			var code = (u32)Info->ExceptionRecord->ExceptionCode;
			if(!ShouldCapture(code)){
				return ContinueSearch;
			}
			if(Interlocked.CompareExchange(ref Busy, 1, 0) != 0){
				return ContinueSearch;
			}
			try{
				if(Interlocked.Increment(ref Written) <= MaxDumps){
					Write(Info, code);
				}
			}finally{
				Interlocked.Exchange(ref Busy, 0);
			}
		}catch{
			// 崩潰路徑上不允許再拋。
		}
		return ContinueSearch;
	}

	[Doc($$"""
	寫一份 minidump，並在 dump 目錄留一個 `last-crash.txt` 指路；失敗只出聲，不再拋。

	*預設不含整份記憶體*：輸入法會在密碼框與私人文件旁邊執行，整份記憶體 dump 會把
	使用者資料寫進 `%LOCALAPPDATA%`；而且 `MiniDumpWriteDump` 從被傾印的行程內呼叫本來就有死鎖風險
	（MSDN 對這點的說明是「不建議」，不是「禁止」）。故預設只帶「執行緒資訊 ＋ 間接引用到的記憶體
	＋ 已卸載模組」，足以還原呼叫堆疊與當時的區域變數；要完整記憶體時設 `NGAN_DUMP_FULL=1`。

	*為何要留 `last-crash.txt`*：常駐模式的 stderr 沒有人看（而且主控台可能已被 `FreeConsole` 脫離），
	只靠 `Console.Error` 等於沒有痕跡；這個小檔在任何情況下都寫得出來。
	""")]
	private static unsafe void Write(ExceptionPointers* Info, u32 Code){
		try{
			var name = $"Ngan.Ime.Windows.{VersionTag}.{DateTime.Now:yyyyMMdd-HHmmss}.{Code:X8}.dmp";
			var Path = System.IO.Path.Combine(DumpDir, name);
			using var stream = File.Create(Path);
			var mdmp = new MinidumpExceptionInformation{
				ThreadId = GetCurrentThreadId(),
				ExceptionPointers = (IntPtr)Info,
				ClientPointers = 0,
			};
			var flags = MiniDumpWithThreadInfo | MiniDumpWithIndirectlyReferencedMemory | MiniDumpWithUnloadedModules;
			if (Environment.GetEnvironmentVariable("NGAN_DUMP_FULL") == "1") {
				flags |= MiniDumpWithFullMemory;
			}
			var ok = MiniDumpWriteDump(
				GetCurrentProcess(),
				GetCurrentProcessId(),
				stream.SafeFileHandle.DangerousGetHandle(),
				flags,
				(IntPtr)(&mdmp),
				IntPtr.Zero,
				IntPtr.Zero);
			WriteCrashMarker(Path, Code, ok);
			Console.Error.WriteLine($"[CrashDump] {(ok ? "已寫出" : "寫出失敗")} {Path}");
		}catch(Exception Ex){
			Console.Error.WriteLine("[CrashDump] 寫入失敗：" + Ex.Message);
		}
	}

	[Doc($$"""
	在 dump 目錄留一個 `last-crash.txt` 指路；常駐模式沒有主控台，這是唯一一定看得到的痕跡。
	""")]
	private static void WriteCrashMarker(str DumpPath, u32 Code, bool Ok){
		try{
			var marker = System.IO.Path.Combine(DumpDir, "last-crash.txt");
			var text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
				+ "\ncode=0x" + Code.ToString("X8")
				+ "\ndump=" + DumpPath
				+ "\nfull_memory=" + (Environment.GetEnvironmentVariable("NGAN_DUMP_FULL") == "1" ? "1" : "0")
				+ "\nwritten=" + (Ok ? "1" : "0")
				+ "\n";
			File.WriteAllText(marker, text, new System.Text.UTF8Encoding(false));
		}catch{
			// 崩潰路徑上不再拋。
		}
	}

	private const i32 ContinueSearch = 0;
	private const u32 MiniDumpWithThreadInfo = 0x00001000;
	[Doc("間接引用到的記憶體（堆疊上的指標所指的內容）；不含整份記憶體。")]
	private const u32 MiniDumpWithIndirectlyReferencedMemory = 0x00000040;
	[Doc("已卸載模組清單；還原堆疊時用得到。")]
	private const u32 MiniDumpWithUnloadedModules = 0x00000020;
	private const u32 MiniDumpWithFullMemory = 0x00000002;

	[StructLayout(LayoutKind.Sequential)]
	private struct MinidumpExceptionInformation{
		public u32 ThreadId;
		public IntPtr ExceptionPointers;
		public i32 ClientPointers;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ExceptionRecord{
		public u32 ExceptionCode;
		public u32 ExceptionFlags;
		public IntPtr ExceptionRecordPtr;
		public IntPtr ExceptionAddress;
		public u32 NumberParameters;
		public IntPtr ExceptionInformation0;
		public IntPtr ExceptionInformation1;
		public IntPtr ExceptionInformation2;
		public IntPtr ExceptionInformation3;
		public IntPtr ExceptionInformation4;
		public IntPtr ExceptionInformation5;
		public IntPtr ExceptionInformation6;
		public IntPtr ExceptionInformation7;
		public IntPtr ExceptionInformation8;
		public IntPtr ExceptionInformation9;
		public IntPtr ExceptionInformation10;
		public IntPtr ExceptionInformation11;
		public IntPtr ExceptionInformation12;
		public IntPtr ExceptionInformation13;
		public IntPtr ExceptionInformation14;
	}

	[StructLayout(LayoutKind.Sequential)]
	private unsafe struct ExceptionPointers{
		public ExceptionRecord* ExceptionRecord;
		public IntPtr ContextRecord;
	}

	[DllImport("kernel32.dll")]
	private static extern IntPtr AddVectoredExceptionHandler(u32 First, IntPtr Handler);

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetCurrentProcess();

	[DllImport("kernel32.dll")]
	private static extern u32 GetCurrentProcessId();

	[DllImport("kernel32.dll")]
	private static extern u32 GetCurrentThreadId();

	[DllImport("dbghelp.dll", SetLastError = true)]
	private static extern bool MiniDumpWriteDump(
		IntPtr Process,
		u32 ProcessId,
		IntPtr File,
		u32 DumpType,
		IntPtr ExceptionParam,
		IntPtr UserStreamParam,
		IntPtr CallbackParam);
}
