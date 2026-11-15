namespace Ngan.Ime.Windows.Infra;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// 行程內的原生崩潰轉儲：註冊向量化例外處理器，遇到存取違規一類的例外就寫 minidump。
/// </summary>
/// <remarks>
/// *為何不用 WER 的 LocalDumps*：那個要在 HKLM 設 `LocalDumps\&lt;image&gt;`，需要管理員；
/// 沒有管理員權限時它就是空的，崩了也什麼都不會留下（先前一直踩這個坑）。
/// 這裡改成行程內自己寫，不需要任何權限。
///
/// *限制*：`__fastfail`（例如 CRT 的 `abort()`，例外碼 0xC0000409）直接進核心，
/// 使用者模式的處理器根本不會被呼叫，那一類崩潰抓不到；存取違規、堆損壞、堆疊溢位則抓得到。
///
/// *為何只寫有限次數*：處理器在崩潰路徑上執行，寫檔本身可能再失敗或再崩；
/// 限制次數並全程吞例外，避免變成一崩再崩的迴圈。
/// </remarks>
internal static partial class CrashDump{

	/// <summary>最多寫幾份；夠定位就行，避免把磁碟塞滿。</summary>
	private const int MaxDumps = 3;

	/// <summary>已寫出的份數。</summary>
	private static int Written;

	/// <summary>是否正在寫（避免遞迴）。</summary>
	private static int Busy;

	/// <summary>轉儲目錄；放在使用者本機資料下，與 rime 日誌分開。</summary>
	private static str DumpDir => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"NganIme", "dumps");

	/// <summary>
	/// 本行程所屬的版本標記（安裝目錄的最後一層，例如 `v20261115-151236`）。
	/// </summary>
	/// <remarks>
	/// 多個版本可以同時安裝、同時運行；dump 與日誌若不帶版本，
	/// 事後根本分不清是哪一版崩的。開發時目錄可能是 `Debug`／`win-x86／`，
	/// 一律照原樣帶上，總比沒有好。
	/// </remarks>
	private static str VersionTag{
		get{
			var Dir = AppContext.BaseDirectory.TrimEnd('\\', '/');
			var Leaf = Path.GetFileName(Dir);
			if(string.IsNullOrWhiteSpace(Leaf)){
				return "unknown";
			}
			// 檔名只留安全字元：目錄名理論上可能含空白或中文。
			var Sb = new System.Text.StringBuilder(Leaf.Length);
			foreach(var One in Leaf){
				Sb.Append(char.IsLetterOrDigit(One) || One is '-' or '_' ? One : '_');
			}
			return Sb.ToString();
		}
	}

	/// <summary>本次要抓的例外碼：存取違規、堆損壞、堆疊溢位、整除零、非法指令。</summary>
	private static bool ShouldCapture(uint Code){
		return Code is 0xC0000005 or 0xC0000374 or 0xC00000FD or 0xC0000094 or 0xC000001D;
	}

	/// <summary>註冊處理器；重複呼叫無副作用。</summary>
	public static unsafe void Install(){
		try{
			Directory.CreateDirectory(DumpDir);
			// 1 = 插到處理器鏈最前面，先於其它處理器看到例外。
			AddVectoredExceptionHandler(1, (IntPtr)(delegate* unmanaged<ExceptionPointers*, int>)&OnException);
		}catch(Exception Ex){
			// 裝不上就算了：沒有轉儲也不能讓程式起不來。
			Console.Error.WriteLine("[CrashDump] 安裝失敗：" + Ex.Message);
		}
	}

	[UnmanagedCallersOnly]
	private static unsafe int OnException(ExceptionPointers* Info){
		try{
			if(Info is null || Info->ExceptionRecord is null){
				return ContinueSearch;
			}
			var Code = (uint)Info->ExceptionRecord->ExceptionCode;
			if(!ShouldCapture(Code)){
				return ContinueSearch;
			}
			if(Interlocked.CompareExchange(ref Busy, 1, 0) != 0){
				return ContinueSearch;
			}
			try{
				if(Interlocked.Increment(ref Written) <= MaxDumps){
					Write(Info, Code);
				}
			}finally{
				Interlocked.Exchange(ref Busy, 0);
			}
		}catch{
			// 崩潰路徑上不允許再拋。
		}
		return ContinueSearch;
	}

	/// <summary>寫一份 full-memory minidump；失敗只出聲，不再拋。</summary>
	private static unsafe void Write(ExceptionPointers* Info, uint Code){
		try{
			var Name = $"Ngan.Ime.Windows.{VersionTag}.{DateTime.Now:yyyyMMdd-HHmmss}.{Code:X8}.dmp";
			var Path = System.IO.Path.Combine(DumpDir, Name);
			using var Stream = File.Create(Path);
			var Mdmp = new MinidumpExceptionInformation{
				ThreadId = GetCurrentThreadId(),
				ExceptionPointers = (IntPtr)Info,
				ClientPointers = 0,
			};
			var Ok = MiniDumpWriteDump(
				GetCurrentProcess(),
				GetCurrentProcessId(),
				Stream.SafeFileHandle.DangerousGetHandle(),
				MiniDumpWithFullMemory | MiniDumpWithThreadInfo,
				(IntPtr)(&Mdmp),
				IntPtr.Zero,
				IntPtr.Zero);
			Console.Error.WriteLine($"[CrashDump] {(Ok ? "已寫出" : "寫出失敗")} {Path}");
		}catch(Exception Ex){
			Console.Error.WriteLine("[CrashDump] 寫入失敗：" + Ex.Message);
		}
	}

	private const int ContinueSearch = 0;
	private const uint MiniDumpWithThreadInfo = 0x00001000;
	private const uint MiniDumpWithFullMemory = 0x00000002;

	[StructLayout(LayoutKind.Sequential)]
	private struct MinidumpExceptionInformation{
		public uint ThreadId;
		public IntPtr ExceptionPointers;
		public int ClientPointers;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ExceptionRecord{
		public uint ExceptionCode;
		public uint ExceptionFlags;
		public IntPtr ExceptionRecordPtr;
		public IntPtr ExceptionAddress;
		public uint NumberParameters;
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
	private static extern IntPtr AddVectoredExceptionHandler(uint First, IntPtr Handler);

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetCurrentProcess();

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentProcessId();

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("dbghelp.dll", SetLastError = true)]
	private static extern bool MiniDumpWriteDump(
		IntPtr Process,
		uint ProcessId,
		IntPtr File,
		uint DumpType,
		IntPtr ExceptionParam,
		IntPtr UserStreamParam,
		IntPtr CallbackParam);
}
