using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

[StructLayout(LayoutKind.Sequential)]
internal struct SpikeRect {
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[GeneratedComInterface]
[Guid("2433BF8E-0F9B-435C-BA2C-180611978C30")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfContextViewSpike {
    [PreserveSig] int GetRangeFromPoint(uint Ec, IntPtr Pt, uint Flags, out IntPtr Range);
    [PreserveSig] int GetTextExt(uint Ec, IntPtr Range, out SpikeRect Rc, out int Clipped);
    [PreserveSig] int GetScreenExt(out SpikeRect Rc);
    [PreserveSig] int GetWnd(out IntPtr Hwnd);
}

[GeneratedComClass]
internal partial class FakeView : ITfContextViewSpike {
    public uint SeenEc;
    public IntPtr SeenRange;

    public int GetRangeFromPoint(uint Ec, IntPtr Pt, uint Flags, out IntPtr Range) {
        Range = IntPtr.Zero;
        return unchecked((int)0x80004005);
    }
    public int GetTextExt(uint Ec, IntPtr Range, out SpikeRect Rc, out int Clipped) {
        SeenEc = Ec;
        SeenRange = Range;
        Rc = new SpikeRect { Left = 10, Top = 20, Right = 30, Bottom = 40 };
        Clipped = 1;
        return 0;
    }
    public int GetScreenExt(out SpikeRect Rc) {
        Rc = new SpikeRect { Left = 1, Top = 2, Right = 3, Bottom = 4 };
        return 0;
    }
    public int GetWnd(out IntPtr Hwnd) {
        Hwnd = (IntPtr)0x1234;
        return 0;
    }
}

internal static class Program {
    private static unsafe int Count(void* Ptr) {
        var N = Marshal.AddRef((IntPtr)Ptr);
        Marshal.Release((IntPtr)Ptr);
        return N - 1;
    }

    private static unsafe int Main() {
        var Obj = new FakeView();
        var Wrappers = new StrategyBasedComWrappers();
        var Ptr = Wrappers.GetOrCreateComInterfaceForObject(Obj, CreateComInterfaceFlags.None);
        var Ok = true;

        {
            var View = ComInterfaceMarshaller<ITfContextViewSpike>.ConvertToManaged((void*)Ptr);
            var Hr = View.GetTextExt(7, (IntPtr)0xABC, out var Rc, out var Clipped);
            var Hr2 = View.GetScreenExt(out var Sr);
            var Hr3 = View.GetWnd(out var Hwnd);
            var Hr4 = View.GetRangeFromPoint(0, IntPtr.Zero, 0, out var Range);
            Ok &= Hr == 0 && Rc.Left == 10 && Rc.Top == 20 && Rc.Right == 30 && Rc.Bottom == 40 && Clipped == 1;
            Ok &= Hr2 == 0 && Sr.Top == 2 && Hr3 == 0 && Hwnd == (IntPtr)0x1234;
            Ok &= Hr4 == unchecked((int)0x80004005) && Range == IntPtr.Zero;
            Ok &= Obj.SeenEc == 7 && Obj.SeenRange == (IntPtr)0xABC;
            Console.WriteLine($"GetTextExt hr=0x{Hr:X8} rect={Rc.Left},{Rc.Top},{Rc.Right},{Rc.Bottom} clipped={Clipped}");
            Console.WriteLine($"參數封送: ec={Obj.SeenEc} range=0x{Obj.SeenRange:X}");
            Console.WriteLine($"GetScreenExt hr=0x{Hr2:X8} top={Sr.Top} GetWnd hr=0x{Hr3:X8} hwnd=0x{Hwnd:X} GetRangeFromPoint hr=0x{Hr4:X8}");
            GC.KeepAlive(View);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var Base = Count((void*)Ptr);
        int WithWrapper;
        {
            var View = ComInterfaceMarshaller<ITfContextViewSpike>.ConvertToManaged((void*)Ptr);
            WithWrapper = Count((void*)Ptr);
            GC.KeepAlive(View);
            View.ToString();
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var After = Count((void*)Ptr);
        Console.WriteLine($"參考計數 base={Base} withWrapper={WithWrapper} afterWrapperDropped={After}（期望 1、大於 1、回到 1）");
        Ok &= Base == 1 && WithWrapper > 1 && After == 1;

        // 模擬 msctf 的行為：別的模組拿到介面指標之後，直接對 vtable 槽位發間接呼叫。
        // 若 CFG 不允許，這裡就會 fail-fast；這決定外殼能不能把實作那半換成 CCW。
        {
            var Iid = new Guid("2433BF8E-0F9B-435C-BA2C-180611978C30");
            var Hr = Marshal.QueryInterface(Ptr, in Iid, out var Iface);
            if (Hr == 0 && Iface != IntPtr.Zero) {
                var Vtbl = *(nint**)Iface;
                var RawGetTextExt = (delegate* unmanaged<IntPtr, uint, IntPtr, SpikeRect*, int*, int>)Vtbl[4];
                var Rect = default(SpikeRect);
                var Clipped = 0;
                var RawHr = RawGetTextExt(Iface, 3, IntPtr.Zero, &Rect, &Clipped);
                Ok &= RawHr == 0 && Rect.Right == 30;
                Console.WriteLine($"原始 vtable 間接呼叫 hr=0x{RawHr:X8} right={Rect.Right}（CFG 放行）");
                Marshal.Release(Iface);
            } else {
                Console.WriteLine("QI 失敗，無法測試原始 vtable 呼叫 hr=0x" + Hr.ToString("X8"));
                Ok = false;
            }
        }

        Marshal.Release(Ptr);
        Console.WriteLine(Ok ? "SPIKE OK" : "SPIKE FAIL");
        return Ok ? 0 : 1;
    }
}
