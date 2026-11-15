// 外殼 COM 介面的冒煙測試：不經過 TSF，直接對發布出來的 DLL 走完
//   LoadLibrary → DllGetClassObject(IClassFactory) → CreateInstance(ITfTextInputProcessor)
//   → QueryInterface(ITfKeyEventSink) → Release
// 用途：把「COM 這層有沒有壞」與「TSF 在那個應用裏的行為」分開。
// 用法：SmokeShellCom.exe <Ngan.Ime.Tsf.dll 路徑>
namespace SmokeShellCom;

using System;
using System.Runtime.InteropServices;

internal static unsafe class Program {
	private static readonly Guid IidClassFactory = new("00000001-0000-0000-C000-000000000046");
	private static readonly Guid IidTextInputProcessor = new("AA80E7F7-2021-11D2-93E0-0060B067B86E");
	private static readonly Guid IidKeyEventSink = new("AA80E7F5-2021-11D2-93E0-0060B067B86E");
	private static readonly Guid IidContext = new("AA80E7FD-2021-11D2-93E0-0060B067B86E");

	[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadLibrary(string Path);

	[DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
	private static extern IntPtr GetProcAddress(IntPtr Module, string Name);

	private static int Main(string[] Args) {
		if (Args.Length < 1) {
			Console.WriteLine("用法：SmokeShellCom <Ngan.Ime.Tsf.dll>");
			return 2;
		}
		var Ok = true;
		var Module = LoadLibrary(Args[0]);
		Console.WriteLine("LoadLibrary=" + (Module == IntPtr.Zero ? "失敗 " + Marshal.GetLastWin32Error() : "0x" + Module.ToString("X")));
		if (Module == IntPtr.Zero) {
			return 1;
		}
		var Export = GetProcAddress(Module, "DllGetClassObject");
		if (Export == IntPtr.Zero) {
			Console.WriteLine("找不到 DllGetClassObject");
			return 1;
		}
		var GetClassObject = (delegate* unmanaged<Guid*, Guid*, IntPtr*, int>)Export;
		var Clsid = Guid.Empty;   // 外殼不再比對 CLSID，填哪一個都回同一個 factory
		var Iid = IidClassFactory;
		IntPtr Factory = IntPtr.Zero;
		var Hr = GetClassObject(&Clsid, &Iid, &Factory);
		Ok &= Report("DllGetClassObject(IClassFactory)", Hr, Factory);
		if (Hr < 0 || Factory == IntPtr.Zero) {
			return 1;
		}

		var FactoryVtbl = *(IntPtr**)Factory;
		var CreateInstance = (delegate* unmanaged<IntPtr, IntPtr, Guid*, IntPtr*, int>)FactoryVtbl[3];
		var TipIid = IidTextInputProcessor;
		IntPtr Tip = IntPtr.Zero;
		Hr = CreateInstance(Factory, IntPtr.Zero, &TipIid, &Tip);
		Ok &= Report("CreateInstance(ITfTextInputProcessor)", Hr, Tip);

		if (Tip != IntPtr.Zero) {
			var KeyIid = IidKeyEventSink;
			IntPtr Sink = IntPtr.Zero;
			Hr = Marshal.QueryInterface(Tip, in KeyIid, out Sink);
			Ok &= Report("QI(ITfKeyEventSink)", Hr, Sink);
			if (Sink != IntPtr.Zero) {
				Marshal.Release(Sink);
			}
			// 外殼不該回 ITfContext（那是 TSF 給我們的，不是我們實作的）
			var CtxIid = IidContext;
			IntPtr Ctx = IntPtr.Zero;
			Hr = Marshal.QueryInterface(Tip, in CtxIid, out Ctx);
			var ExpectNoInterface = Hr < 0;
			Ok &= ExpectNoInterface;
			Console.WriteLine($"QI(ITfContext) hr=0x{Hr:X8}（期望失敗，因為外殼不實作它）{(ExpectNoInterface ? "" : "  <-- 不該成功")}");
			if (Ctx != IntPtr.Zero) {
				Marshal.Release(Ctx);
			}
			Marshal.Release(Tip);
		}
		Marshal.Release(Factory);
		Console.WriteLine(Ok ? "SMOKE OK" : "SMOKE FAIL");
		return Ok ? 0 : 1;
	}

	private static bool Report(string What, int Hr, IntPtr Ptr) {
		var Ok = Hr >= 0 && Ptr != IntPtr.Zero;
		Console.WriteLine($"{What} hr=0x{Hr:X8} ptr=0x{Ptr.ToString("X")}{(Ok ? "" : "  <-- 失敗")}");
		return Ok;
	}
}
