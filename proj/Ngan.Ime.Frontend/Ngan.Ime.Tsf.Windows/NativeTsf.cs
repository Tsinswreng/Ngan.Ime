namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

/// <summary>
/// NativeAOT 版的 TSF 薄外殼。整個 DLL 不帶 CLR，只做三件事：
/// 1. 提供 DllGetClassObject 與 IClassFactory，讓 Windows 建立文字服務；
/// 2. 實作 ITfTextInputProcessor 與 ITfKeyEventSink 的 vtable（手寫，不用內建 COM）；
/// 3. 把 preedit 透過既有命名管道丟給 Avalonia 候選窗，文件內容留到後續階段。
/// </summary>
internal static unsafe class NativeTsf {
    /// <summary>本 POC 的文字服務 CLSID，必須與註冊工具寫入的 InprocServer32 對應。</summary>
    internal static readonly Guid Clsid = new("9B7E5C42-3A1D-4F6E-8B2A-5C1D0E9F7A31");

    /// <summary>標準 COM 介面識別碼。</summary>
    private static readonly Guid IidIUnknown = new("00000000-0000-0000-C000-000000000046");
    /// <summary>IClassFactory。</summary>
    private static readonly Guid IidClassFactory = new("00000001-0000-0000-C000-000000000046");
    /// <summary>ITfTextInputProcessor。</summary>
    private static readonly Guid IidTextInputProcessor = new("AA80E7F7-2021-11D2-93E0-0060B067B86E");
    /// <summary>ITfKeyEventSink。</summary>
    private static readonly Guid IidKeyEventSink = new("AA80E7F5-2021-11D2-93E0-0060B067B86E");
    /// <summary>ITfKeystrokeMgr。</summary>
    private static readonly Guid IidKeystrokeMgr = new("AA80E7F0-2021-11D2-93E0-0060B067B86E");
    private static readonly Guid IidEditSession = new("AA80E803-2021-11D2-93E0-0060B067B86E");
    private static readonly Guid IidContextComposition = new("D40C8AAE-AC92-4FC7-9A11-0EE0E23AA39B");
    private static readonly Guid IidCompositionSink = new("A781718C-579A-4B15-A280-32B8577ACC5E");

    private const int SOk = 0;
    private const int SFalse = 1;
    private const int ENoInterface = unchecked((int)0x80004002);
    private const int EFail = unchecked((int)0x80004005);
    private const int EClassNotAvailable = unchecked((int)0x80040111);
    private const int EInvalidArg = unchecked((int)0x80070057);
    private const int ENoAggregation = unchecked((int)0x80040110);

    private const int VkBackspace = 0x08;
    private const int VkEscape = 0x1B;
    private const int VkSpace = 0x20;

    private const uint TfEsSync = 0x1;
    private const uint TfEsRead = 0x2;
    private const uint TfEsReadWrite = 0x6;
    private const uint TfAnchorEnd = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TfSelection {
        public IntPtr Range;
        public uint ActiveEnd;
        public int Interim;
    }

    /// <summary>候選窗的命名管道名稱，與 Avalonia 端的 CandidatePipeServer 一致。</summary>
    private const string PipeName = "NganIme.Tsf.v1";

    private static readonly object LogGate = new();

    private static readonly IntPtr* TipVtbl;
    private static readonly IntPtr* SinkVtbl;
    private static readonly IntPtr* FactoryVtbl;
    private static readonly IntPtr* EditSessionVtbl;
    private static readonly IntPtr* CompSinkVtbl;

    private static readonly object FactoryGate = new();
    private static IntPtr FactoryView;
    private static int FactoryRefCount = 1;

    static NativeTsf() {
        TipVtbl = (IntPtr*)NativeMemory.Alloc(5, (nuint)sizeof(IntPtr));
        TipVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&QueryInterfaceEntry;
        TipVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&AddRefEntry;
        TipVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&ReleaseEntry;
        TipVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, uint, int>)&ActivateEntry;
        TipVtbl[4] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&DeactivateEntry;

        SinkVtbl = (IntPtr*)NativeMemory.Alloc(9, (nuint)sizeof(IntPtr));
        SinkVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&QueryInterfaceEntry;
        SinkVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&AddRefEntry;
        SinkVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&ReleaseEntry;
        SinkVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, int, int>)&OnSetFocusEntry;
        SinkVtbl[4] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, int*, int>)&OnTestKeyDownEntry;
        SinkVtbl[5] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, int*, int>)&OnTestKeyUpEntry;
        SinkVtbl[6] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, int*, int>)&OnKeyDownEntry;
        SinkVtbl[7] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, int*, int>)&OnKeyUpEntry;
        SinkVtbl[8] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, Guid*, int*, int>)&OnPreservedKeyEntry;

        FactoryVtbl = (IntPtr*)NativeMemory.Alloc(5, (nuint)sizeof(IntPtr));
        FactoryVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&FactoryQueryInterfaceEntry;
        FactoryVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&FactoryAddRefEntry;
        FactoryVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&FactoryReleaseEntry;
        FactoryVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, Guid*, IntPtr*, int>)&CreateInstanceEntry;
        FactoryVtbl[4] = (IntPtr)(delegate* unmanaged<IntPtr, int, int>)&LockServerEntry;

        EditSessionVtbl = (IntPtr*)NativeMemory.Alloc(4, (nuint)sizeof(IntPtr));
        EditSessionVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&EditSessionQueryEntry;
        EditSessionVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&AddRefEntry;
        EditSessionVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&ReleaseEntry;
        EditSessionVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, uint, int>)&DoEditSessionEntry;

        CompSinkVtbl = (IntPtr*)NativeMemory.Alloc(4, (nuint)sizeof(IntPtr));
        CompSinkVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&CompSinkQueryEntry;
        CompSinkVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&AddRefEntry;
        CompSinkVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&ReleaseEntry;
        CompSinkVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, uint, IntPtr, int>)&OnCompositionTerminatedEntry;

        RegisterCfgTargets();
        AddVectoredExceptionHandler(1, (IntPtr)(delegate* unmanaged<IntPtr, int>)&VectoredExceptionHandler);
        Log("NativeTsf static init pid=" + Environment.ProcessId);
    }

    /// <summary>單一文字服務執行個體的狀態；兩個介面指標共用同一份參考計數。</summary>
    private sealed class TsfState {
        /// <summary>COM 參考計數，兩個 view 共用。</summary>
        public int RefCount = 1;
        /// <summary>ITfTextInputProcessor 的介面指標。</summary>
        public IntPtr TipView;
        /// <summary>ITfKeyEventSink 的介面指標。</summary>
        public IntPtr SinkView;
        /// <summary>對應的 GCHandle，釋放時要一起 Free。</summary>
        public IntPtr TipHandle;
        /// <summary>對應的 GCHandle，釋放時要一起 Free。</summary>
        public IntPtr SinkHandle;
        /// <summary>TSF 指派的 client id。</summary>
        public uint ClientId;
        /// <summary>ITfKeystrokeMgr 的原始指標，Deactivate 時釋放。</summary>
        public IntPtr KeystrokeMgr;
        /// <summary>目前未上屏的 preedit 文字。</summary>
        public readonly StringBuilder Preedit = new();
        /// <summary>ITfEditSession 的介面指標（自己實作，交給 ITfContext::RequestEditSession）。</summary>
        public IntPtr EditSessionView;
        /// <summary>ITfEditSession 的 GCHandle。</summary>
        public IntPtr EditSessionHandle;
        /// <summary>ITfCompositionSink 的介面指標。</summary>
        public IntPtr CompSinkView;
        /// <summary>ITfCompositionSink 的 GCHandle。</summary>
        public IntPtr CompSinkHandle;
        /// <summary>目前按鍵所屬的 ITfContext（只在 edit session 內有效）。</summary>
        public IntPtr Context;
        /// <summary>目前的 TSF composition；0 表示尚未建立。</summary>
        public IntPtr Composition;
        /// <summary>待執行的編輯操作：0 無、2 上屏。</summary>
        public uint PendingOperation;
        /// <summary>待上屏的文字（由宿主回傳的 commit 合併）。</summary>
        public string PendingCommit = "";
        /// <summary>最近一次按鍵查詢宿主的結果。</summary>
        public HostResult? Host;
        /// <summary>游標在螢幕上的左邊界（像素）。</summary>
        public int CaretX;
        /// <summary>游標在螢幕上的下邊界（像素）。</summary>
        public int CaretY;
    }

    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
    private static int DllGetClassObject(Guid* Rclsid, Guid* Riid, IntPtr* Ppv) {
        try {
            Log("DllGetClassObject");
            if (Rclsid is null || Riid is null || Ppv is null) {
                return EInvalidArg;
            }
            if (*Rclsid != Clsid) {
                return EClassNotAvailable;
            }
            EnsureFactory();
            return FactoryQueryCore(FactoryView, Riid, Ppv);
        }
        catch (Exception Exception) {
            Log("DllGetClassObject EX " + Exception);
            return EFail;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
    private static int DllCanUnloadNow() {
        return SFalse;
    }

    private static void EnsureFactory() {
        lock (FactoryGate) {
            if (FactoryView == IntPtr.Zero) {
                var Block = (IntPtr*)NativeMemory.Alloc(2, (nuint)sizeof(IntPtr));
                Block[0] = (IntPtr)FactoryVtbl;
                Block[1] = IntPtr.Zero;
                FactoryView = (IntPtr)Block;
            }
        }
    }

    private static IntPtr MakeView(IntPtr* Vtbl, TsfState State, out IntPtr Handle) {
        var Block = (IntPtr*)NativeMemory.Alloc(2, (nuint)sizeof(IntPtr));
        Block[0] = (IntPtr)Vtbl;
        var GcHandle = GCHandle.Alloc(State);
        Handle = GCHandle.ToIntPtr(GcHandle);
        Block[1] = Handle;
        return (IntPtr)Block;
    }

    private static TsfState? GetState(IntPtr ThisPtr) {
        if (ThisPtr == IntPtr.Zero) {
            return null;
        }
        var Block = (IntPtr*)ThisPtr;
        var Handle = Block[1];
        if (Handle == IntPtr.Zero) {
            return null;
        }
        return GCHandle.FromIntPtr(Handle).Target as TsfState;
    }

    private static int QueryCore(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
        Log("QI core this=" + ThisPtr.ToString("X"));
        var State = GetState(ThisPtr);
        if (State is null) {
            return ENoInterface;
        }
        Log("QI " + *Riid + " this=" + ThisPtr.ToString("X"));
        if (*Riid == IidIUnknown) {
            *Ppv = ThisPtr;
            Interlocked.Increment(ref State.RefCount);
            return SOk;
        }
        if (*Riid == IidTextInputProcessor) {
            *Ppv = State.TipView;
            Interlocked.Increment(ref State.RefCount);
            return SOk;
        }
        if (*Riid == IidKeyEventSink) {
            *Ppv = State.SinkView;
            Interlocked.Increment(ref State.RefCount);
            return SOk;
        }
        if (*Riid == IidCompositionSink) {
            *Ppv = State.CompSinkView;
            Interlocked.Increment(ref State.RefCount);
            return SOk;
        }
        *Ppv = IntPtr.Zero;
        return ENoInterface;
    }

    private static int AddRefCore(IntPtr ThisPtr) {
        var State = GetState(ThisPtr);
        var Count = State is null ? 0 : Interlocked.Increment(ref State.RefCount);
        Log("AddRef this=" + ThisPtr.ToString("X") + " -> " + Count);
        return Count;
    }

    private static int ReleaseCore(IntPtr ThisPtr) {
        var State = GetState(ThisPtr);
        if (State is null) {
            return 0;
        }
        var Count = Interlocked.Decrement(ref State.RefCount);
        Log("Release this=" + ThisPtr.ToString("X") + " -> " + Count);
        // POC：物件在行程生命期內不釋放。
        // 之前一旦參考計數歸零就釋放 view，結果 TSF 之後仍拿著 sink 指標，拿到失效記憶體。
        return Count;
    }

    private static int FactoryQueryCore(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
        if (*Riid == IidIUnknown || *Riid == IidClassFactory) {
            *Ppv = ThisPtr;
            Interlocked.Increment(ref FactoryRefCount);
            return SOk;
        }
        *Ppv = IntPtr.Zero;
        return ENoInterface;
    }

    private static int CreateInstanceCore(IntPtr ThisPtr, IntPtr Outer, Guid* Riid, IntPtr* Ppv) {
        try {
            if (Outer != IntPtr.Zero) {
                return ENoAggregation;
            }
            Log("CreateInstance riid=" + *Riid);
            var State = new TsfState();
            State.TipView = MakeView(TipVtbl, State, out var TipHandle);
            State.TipHandle = TipHandle;
            State.SinkView = MakeView(SinkVtbl, State, out var SinkHandle);
            State.SinkHandle = SinkHandle;
            State.EditSessionView = MakeView(EditSessionVtbl, State, out var EditSessionHandle);
            State.EditSessionHandle = EditSessionHandle;
            State.CompSinkView = MakeView(CompSinkVtbl, State, out var CompSinkHandle);
            State.CompSinkHandle = CompSinkHandle;

            if (*Riid == IidIUnknown || *Riid == IidTextInputProcessor) {
                *Ppv = State.TipView;
                return SOk;
            }
            if (*Riid == IidKeyEventSink) {
                *Ppv = State.SinkView;
                return SOk;
            }
            *Ppv = IntPtr.Zero;
            return ENoInterface;
        }
        catch (Exception Exception) {
            Log("CreateInstance EX " + Exception);
            return EFail;
        }
    }

    private static int ActivateCore(IntPtr ThisPtr, IntPtr ThreadManager, uint ClientId) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                return EFail;
            }
            State.ClientId = ClientId;
            var iid = IidKeystrokeMgr;
            var ThreadVtbl = *(IntPtr***)ThreadManager;
            var QueryInterface = (delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)ThreadVtbl[0];
            IntPtr KeystrokeMgr = IntPtr.Zero;
            var Hr = QueryInterface(ThreadManager, &iid, &KeystrokeMgr);
            Log("Activate QI keystrokeMgr hr=0x" + Hr.ToString("X8"));
            if (Hr < 0) {
                return Hr;
            }
            var KsVtbl = *(IntPtr***)KeystrokeMgr;
            Log("Activate ksm=" + KeystrokeMgr.ToString("X") + " vtbl=" + ((IntPtr)KsVtbl).ToString("X") + " slot0=" + ((IntPtr)KsVtbl[0]).ToString("X") + " slot1=" + ((IntPtr)KsVtbl[1]).ToString("X"));
            var Mode = Environment.GetEnvironmentVariable("NGAN_TSF_MODE") ?? "advise";
            Log("Activate mode=" + Mode);
            if (Mode == "none") {
                State.KeystrokeMgr = KeystrokeMgr;
                return SOk;
            }
            if (Mode == "unadvise") {
                var UnadviseProbe = (delegate* unmanaged<IntPtr, uint, int>)KsVtbl[4];
                Hr = UnadviseProbe(KeystrokeMgr, ClientId);
                Log("Activate Unadvise(probe) hr=0x" + Hr.ToString("X8"));
                State.KeystrokeMgr = KeystrokeMgr;
                return SOk;
            }
            // 自測：用間接呼叫驗證 sink 的 vtable 真的可被呼叫。
            try {
                var SelfVtbl = *(IntPtr**)State.SinkView;
                var SelfQuery = (delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)SelfVtbl[0];
                var SelfIid = IidKeyEventSink;
                IntPtr SelfOut = IntPtr.Zero;
                var SelfHr = SelfQuery(State.SinkView, &SelfIid, &SelfOut);
                Log("self-test sink QI hr=0x" + SelfHr.ToString("X8") + " out=" + SelfOut.ToString("X"));
            }
            catch (Exception Exception) {
                Log("self-test EX " + Exception);
            }
            if (Mode == "nullsink") {
                var NullAdvise = (delegate* unmanaged<IntPtr, uint, IntPtr, int, int>)KsVtbl[3];
                Hr = NullAdvise(KeystrokeMgr, ClientId, IntPtr.Zero, 0);
                Log("Activate null-sink Advise hr=0x" + Hr.ToString("X8"));
                State.KeystrokeMgr = KeystrokeMgr;
                return SOk;
            }
            {
                var Vt = *(IntPtr**)State.SinkView;
                var Slots = new StringBuilder();
                for (var I = 0; I < 9; I++) { Slots.Append(Vt[I].ToString("X")).Append(' '); }
                Log("sink vtbl=" + ((IntPtr)Vt).ToString("X") + " slots=" + Slots);
            }
            var Advise = (delegate* unmanaged<IntPtr, uint, IntPtr, int, int>)KsVtbl[3];
            Log("Activate advising clientId=" + ClientId + " sink=" + State.SinkView.ToString("X"));
            Hr = Advise(KeystrokeMgr, ClientId, State.SinkView, 1);
            Log("Activate AdviseKeyEventSink hr=0x" + Hr.ToString("X8"));
            if (Hr < 0) {
                ReleaseCom(KeystrokeMgr);
                return Hr;
            }
            State.KeystrokeMgr = KeystrokeMgr;
            return SOk;
        }
        catch (Exception Exception) {
            Log("Activate EX " + Exception);
            return EFail;
        }
    }

    private static int DeactivateCore(IntPtr ThisPtr) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                return EFail;
            }
            if (State.KeystrokeMgr != IntPtr.Zero) {
                var KsVtbl = *(IntPtr***)State.KeystrokeMgr;
                var Unadvise = (delegate* unmanaged<IntPtr, uint, int>)KsVtbl[4];
                Unadvise(State.KeystrokeMgr, State.ClientId);
                ReleaseCom(State.KeystrokeMgr);
                State.KeystrokeMgr = IntPtr.Zero;
            }
            ReleaseComposition(State);
            lock (State) {
                State.Preedit.Clear();
            }
            Send("HIDE");
            Log("Deactivate done");
            return SOk;
        }
        catch (Exception Exception) {
            Log("Deactivate EX " + Exception);
            return EFail;
        }
    }

    private static int OnSetFocusCore(IntPtr ThisPtr, int IsForeground) {
        try {
            Log("OnSetFocus fg=" + IsForeground);
            var State = GetState(ThisPtr);
            if (State is not null && IsForeground == 0) {
                lock (State) {
                    State.Preedit.Clear();
                    ReleaseComposition(State);
                }
                Send("HIDE");
            }
            return SOk;
        }
        catch (Exception Exception) {
            Log("OnSetFocus EX " + Exception);
            return EFail;
        }
    }

    /// <summary>可以交給宿主（Rime）判斷的按鍵範圍；其餘按鍵一律放行。</summary>
    // 交給引擎的鍵：除了「送進去會破壞系統操作」的少數組合，其餘一律先問引擎。
    // 舊版只放行字母／數字／標點，方向鍵與 - = 根本到不了引擎，
    // 症狀就是「有候選時方向鍵沒反應」「按 - 或 = 直接把字上屏」。
    private static bool IsHostKey(int VirtualKey, int Mods) {
        // Win 鍵、PrintScreen、Alt+Tab、Ctrl+Esc、Alt+F4 由系統處理。
        if (VirtualKey is 0x5B or 0x5C or 0x2C or 0x73) { return false; }
        if (VirtualKey == 0x09 && (Mods & 4) != 0) { return false; }
        if (VirtualKey == 0x1B && (Mods & 2) != 0) { return false; }
        if (VirtualKey == 0x73 && (Mods & 4) != 0) { return false; }
        if (VirtualKey is >= 0x70 and <= 0x7B) { return false; } // F1-F12
        return true;
    }

    private static int GetModifiers() {
        var Mods = 0;
        if ((GetKeyState(0x10) & 0x8000) != 0) { Mods |= 1; }
        if ((GetKeyState(0x11) & 0x8000) != 0) { Mods |= 2; }
        if ((GetKeyState(0x12) & 0x8000) != 0) { Mods |= 4; }
        return Mods;
    }

    private static int OnTestKeyDownCore(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                *IsEaten = 0;
                return EFail;
            }
            var VirtualKey = (int)WParam.ToInt64();
            // 每次評估都先清掉上一次的結果，避免 OnKeyDown 讀到過期的回應。
            State.Host = null;
            var Mods = GetModifiers();
            if (!IsHostKey(VirtualKey, Mods)) {
                *IsEaten = 0;
                return SOk;
            }
            UpdateCaretFromGui(State);
            State.Host = QueryHost(VirtualKey, Mods, State.CaretX, State.CaretY);
            *IsEaten = State.Host is not null && State.Host.Eaten ? 1 : 0;
            Log("OnTestKeyDown vk=" + VirtualKey + " eat=" + *IsEaten + " cand=" + (State.Host?.Candidates.Count ?? 0));
            return SOk;
        }
        catch (Exception Exception) {
            Log("OnTestKeyDown EX " + Exception);
            *IsEaten = 0;
            return EFail;
        }
    }

    private static int OnKeyDownCore(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                *IsEaten = 0;
                return EFail;
            }
            var Result = State.Host;
            State.Host = null;
            if (Result is null || !Result.Eaten) {
                *IsEaten = 0;
                return SOk;
            }
            *IsEaten = 1;
            if (Result.Commits.Count > 0) {
                State.PendingCommit = string.Concat(Result.Commits);
                State.PendingOperation = 2;
                var Hr = RequestEditSession(State, Context);
                Log("Commit len=" + State.PendingCommit.Length + " hr=0x" + Hr.ToString("X8"));
            }
            return SOk;
        }
        catch (Exception Exception) {
            Log("OnKeyDown EX " + Exception);
            *IsEaten = 0;
            return EFail;
        }
    }

    private static int OnTestKeyUpCore(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) {
        Log("OnTestKeyUp");
        *IsEaten = 0;
        return SOk;
    }

    private static int OnKeyUpCore(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) {
        Log("OnKeyUp");
        *IsEaten = 0;
        return SOk;
    }

    private static int OnPreservedKeyCore(IntPtr ThisPtr, IntPtr Context, Guid* PreservedKey, int* IsEaten) {
        Log("OnPreservedKey");
        *IsEaten = 0;
        return SOk;
    }

    private static bool IsSupported(int VirtualKey) {
        return VirtualKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A or VkSpace or VkBackspace or VkEscape;
    }

    private static void ApplyKey(TsfState State, int VirtualKey) {
        lock (State) {
            if (VirtualKey == VkEscape) {
                State.Preedit.Clear();
                State.PendingOperation = 0;
                return;
            }
            if (VirtualKey == VkSpace) {
                State.PendingOperation = 2;
                return;
            }
            State.Preedit.Append(char.ToLowerInvariant((char)VirtualKey));
            State.PendingOperation = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo {
        public uint Size;
        public uint Flags;
        public IntPtr Active;
        public IntPtr Focus;
        public IntPtr Capture;
        public IntPtr MenuOwner;
        public IntPtr MoveSize;
        public IntPtr Caret;
        public NativeRect CaretRect;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetGUIThreadInfo(uint ThreadId, ref GuiThreadInfo Info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClientToScreen(IntPtr Window, ref NativePoint Point);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    /// <summary>
    /// 用 Win32 的 GUI thread info 取得插入點位置。
    /// TSF 的 ITfContextView::GetTextExt 在這版 Windows 的 ABI 與文件不一致（會把 edit cookie
    /// 當成指標），先用 caret 矩形代替；記事本這類原生控件都拿得到。
    /// </summary>
    private static void UpdateCaretFromGui(TsfState State) {
        try {
            var Info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
            if (GetGUIThreadInfo(GetCurrentThreadId(), ref Info) && Info.Caret != IntPtr.Zero) {
                var Point = new NativePoint { X = Info.CaretRect.Left, Y = Info.CaretRect.Bottom };
                if (ClientToScreen(Info.Caret, ref Point)) {
                    State.CaretX = Point.X;
                    State.CaretY = Point.Y;
                }
            }
        }
        catch (Exception Exception) {
            Log("UpdateCaretFromGui EX " + Exception);
        }
    }

    private static void PublishPreedit(TsfState State) {
        lock (State) {
            if (State.Preedit.Length == 0) {
                Send("HIDE");
            }
            else {
                SendShow(State.Preedit.ToString(), State.CaretX, State.CaretY);
            }
        }
    }

    private static void SendShow(string Preedit, int X, int Y) {
        Send("SHOW|" + X + "|" + Y + "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Preedit)));
    }

    private sealed class CandidateItem {
        public string Text = "";
        public string Comment = "";
    }

    private sealed class HostResult {
        public bool Eaten;
        public string Preedit = "";
        public int Highlight;
        public List<CandidateItem> Candidates = new();
        public List<string> Commits = new();
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int VirtualKey);

    /// <summary>
    /// 把按鍵送給 Ngan.Ime 宿主（Rime）處理，並取回是否吃掉、preedit、候選與 commit。
    /// 宿主沒開或超時就回 null，呼叫端必須放行按鍵，避免整個系統打不了字。
    /// </summary>
    private static readonly object PipeLock = new();
    private static NamedPipeClientStream? Pipe;
    private static StreamReader? PipeReader;
    private static StreamWriter? PipeWriter;

    private static void ResetPipe() {
        try { PipeWriter?.Dispose(); } catch { }
        try { PipeReader?.Dispose(); } catch { }
        try { Pipe?.Dispose(); } catch { }
        PipeWriter = null;
        PipeReader = null;
        Pipe = null;
    }

    /// <summary>
    /// 與宿主保持長連線；連線斷了才重連。避免每個按鍵都重新 Connect 撞上宿主換連線的空窗。
    /// </summary>
    private static HostResult? QueryHost(int Vk, int Mods, int X, int Y) {
        lock (PipeLock) {
            for (var Attempt = 0; Attempt < 2; Attempt++) {
                try {
                    if (Pipe is null || !Pipe.IsConnected) {
                        ResetPipe();
                        Pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                        Pipe.Connect(1000);
                        PipeWriter = new StreamWriter(Pipe, new UTF8Encoding(false)) { AutoFlush = true };
                        PipeReader = new StreamReader(Pipe, Encoding.UTF8);
                    }
                    PipeWriter!.WriteLine("K|" + Vk + "|" + Mods + "|" + X + "|" + Y);
                    var Line = PipeReader!.ReadLine();
                    if (Line is null) {
                        ResetPipe();
                        continue;
                    }
                    return ParseHostReply(Line);
                }
                catch (Exception Exception) {
                    Log("QueryHost attempt " + Attempt + " failed " + Exception.GetType().Name + ": " + Exception.Message);
                    ResetPipe();
                }
            }
            return null;
        }
    }

    private static HostResult? ParseHostReply(string Line) {
        if (!Line.StartsWith("R|", StringComparison.Ordinal)) {
            return null;
        }
        var Parts = Line.Split('|');
        var Result = new HostResult {
            Eaten = Parts[1] == "1",
            Highlight = int.Parse(Parts[2]),
            Preedit = Encoding.UTF8.GetString(Convert.FromBase64String(Parts[3])),
        };
        var CandidateCount = int.Parse(Parts[4]);
        var Index = 5;
        for (var I = 0; I < CandidateCount; I++) {
            var Pair = Parts[Index++].Split('~');
            Result.Candidates.Add(new CandidateItem {
                Text = Encoding.UTF8.GetString(Convert.FromBase64String(Pair[0])),
                Comment = Pair.Length > 1 ? Encoding.UTF8.GetString(Convert.FromBase64String(Pair[1])) : "",
            });
        }
        var CommitCount = int.Parse(Parts[Index++]);
        for (var I = 0; I < CommitCount; I++) {
            Result.Commits.Add(Encoding.UTF8.GetString(Convert.FromBase64String(Parts[Index++])));
        }
        return Result;
    }

    private static void Send(string Message) {
        try {
            using var Client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            Client.Connect(10);
            using var Writer = new StreamWriter(Client, new UTF8Encoding(false)) { AutoFlush = true };
            Writer.WriteLine(Message);
        }
        catch (Exception Exception) {
            Log("Pipe failed " + Exception.GetType().Name);
        }
    }

    private static void ReleaseCom(IntPtr ComObject) {
        var Vtbl = *(IntPtr***)ComObject;
        var Release = (delegate* unmanaged<IntPtr, int>)Vtbl[2];
        Release(ComObject);
    }

    private static int RequestEditSession(TsfState State, IntPtr Context) {
        State.Context = Context;
        var Vtbl = *(IntPtr**)Context;
        var Request = (delegate* unmanaged<IntPtr, uint, IntPtr, uint, int*, int>)Vtbl[3];
        var SessionResult = 0;
        var Flags = State.PendingOperation == 2 ? (TfEsSync | TfEsReadWrite) : (TfEsSync | TfEsRead);
        var Hr = Request(Context, State.ClientId, State.EditSessionView, Flags, &SessionResult);
        if (Hr < 0) {
            Log("RequestEditSession hr=0x" + Hr.ToString("X8"));
            return Hr;
        }
        return SessionResult;
    }

    private static int DoEditSessionCore(IntPtr ThisPtr, uint EditCookie) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                return EFail;
            }
            lock (State) {
                var Operation = State.PendingOperation;
                State.PendingOperation = 0;
                Log("DoEditSession op=" + Operation + " ec=0x" + EditCookie.ToString("X") + " text=[" + State.Preedit + "]");
                return Operation switch {
                    2 => InsertTextCore(State, EditCookie),
                    _ => SOk,
                };
            }
        }
        catch (Exception Exception) {
            Log("DoEditSession EX " + Exception);
            return EFail;
        }
    }

    /// <summary>
    /// 把目前 preedit 緩衝區的文字插入游標位置。
    /// 打字過程中完全不碰文件，只有空白鍵上屏時才呼叫 ITfRange::SetText。
    /// </summary>
    private static int InsertTextCore(TsfState State, uint EditCookie) {
        var ContextVtbl = *(IntPtr**)State.Context;
        var GetSelection = (delegate* unmanaged<IntPtr, uint, uint, uint, TfSelection*, uint*, int>)ContextVtbl[5];
        var Selection = default(TfSelection);
        uint Fetched = 0;
        var Hr = GetSelection(State.Context, EditCookie, 0, 1, &Selection, &Fetched);
        Log("InsertText GetSelection hr=0x" + Hr.ToString("X8") + " fetched=" + Fetched);
        if (Hr < 0) {
            return Hr;
        }
        if (Fetched != 1 || Selection.Range == IntPtr.Zero) {
            return EFail;
        }
        var RangeVtbl = *(IntPtr**)Selection.Range;
        var SetText = (delegate* unmanaged<IntPtr, uint, uint, char*, int, int>)RangeVtbl[4];
        var Text = State.PendingCommit;
        fixed (char* TextPtr = Text) {
            Hr = SetText(Selection.Range, EditCookie, 0, TextPtr, Text.Length);
        }
        Log("InsertText SetText hr=0x" + Hr.ToString("X8") + " len=" + Text.Length);
        if (Hr >= 0) {
            var Collapse = (delegate* unmanaged<IntPtr, uint, uint, int>)RangeVtbl[15];
            var CollapseHr = Collapse(Selection.Range, EditCookie, TfAnchorEnd);
            Log("InsertText Collapse hr=0x" + CollapseHr.ToString("X8"));
        }
        ReleaseCom(Selection.Range);
        State.PendingCommit = "";
        return Hr;
    }

    private static int UpdateCompositionCore(TsfState State, uint EditCookie) {
        if (State.Composition == IntPtr.Zero) {
            var StartHr = StartCompositionCore(State, EditCookie);
            if (StartHr < 0) {
                return StartHr;
            }
        }
        return SetCompositionText(State, EditCookie, State.Preedit.ToString());
    }

    private static int StartCompositionCore(TsfState State, uint EditCookie) {
        var ContextVtbl = *(IntPtr**)State.Context;
        var GetSelection = (delegate* unmanaged<IntPtr, uint, uint, uint, TfSelection*, uint*, int>)ContextVtbl[5];
        var Selection = default(TfSelection);
        uint Fetched = 0;
        var Hr = GetSelection(State.Context, EditCookie, 0, 1, &Selection, &Fetched);
        Log("GetSelection hr=0x" + Hr.ToString("X8") + " fetched=" + Fetched);
        if (Hr < 0) {
            return Hr;
        }
        if (Fetched != 1 || Selection.Range == IntPtr.Zero) {
            return EFail;
        }
        var QueryInterface = (delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)ContextVtbl[0];
        var Iid = IidContextComposition;
        IntPtr Composer = IntPtr.Zero;
        Hr = QueryInterface(State.Context, &Iid, &Composer);
        Log("QI ITfContextComposition hr=0x" + Hr.ToString("X8"));
        if (Hr < 0) {
            ReleaseCom(Selection.Range);
            return Hr;
        }
        var ComposerVtbl = *(IntPtr**)Composer;
        var Start = (delegate* unmanaged<IntPtr, uint, IntPtr, IntPtr, IntPtr*, int>)ComposerVtbl[3];
        IntPtr Composition = IntPtr.Zero;
        Hr = Start(Composer, EditCookie, Selection.Range, State.CompSinkView, &Composition);
        Log("StartComposition hr=0x" + Hr.ToString("X8") + " comp=" + Composition.ToString("X"));
        Log("before release composer");
        ReleaseCom(Composer);
        Log("before release selection");
        ReleaseCom(Selection.Range);
        Log("after releases");
        if (Hr < 0) {
            return Hr;
        }
        State.Composition = Composition;
        return SOk;
    }

    private static int SetCompositionText(TsfState State, uint EditCookie, string Text) {
        if (State.Composition == IntPtr.Zero) {
            return EFail;
        }
        Log("SetCompositionText comp=" + State.Composition.ToString("X"));
        var CompositionVtbl = *(IntPtr**)State.Composition;
        Log("SetCompositionText vtbl=" + ((IntPtr)CompositionVtbl).ToString("X"));
        // 這裡的 slot 3 其實吃 (ITfRange**)，不吃 ec（用 dump 反組譯 textinputframework 確認過）。
        var GetRange = (delegate* unmanaged<IntPtr, IntPtr*, int>)CompositionVtbl[3];
        IntPtr Range = IntPtr.Zero;
        var Hr = GetRange(State.Composition, &Range);
        Log("GetRange hr=0x" + Hr.ToString("X8") + " range=" + Range.ToString("X"));
        if (Hr < 0 || Range == IntPtr.Zero) {
            Log("Composition.GetRange hr=0x" + Hr.ToString("X8"));
            return Hr < 0 ? Hr : EFail;
        }
        var RangeVtbl = *(IntPtr**)Range;
        Log("Range vtbl=" + ((IntPtr)RangeVtbl).ToString("X"));
        var SetText = (delegate* unmanaged<IntPtr, uint, uint, char*, int, int>)RangeVtbl[4];
        fixed (char* TextPtr = Text) {
            Log("SetText calling len=" + Text.Length);
            Hr = SetText(Range, EditCookie, 0, TextPtr, Text.Length);
        }
        Log("Range.SetText hr=0x" + Hr.ToString("X8") + " len=" + Text.Length);
        if (Hr >= 0) {
            var Collapse = (delegate* unmanaged<IntPtr, uint, uint, int>)RangeVtbl[15];
            var CollapseHr = Collapse(Range, EditCookie, TfAnchorEnd);
            Log("Range.Collapse hr=0x" + CollapseHr.ToString("X8"));
            Hr = CollapseHr;
        }
        ReleaseCom(Range);
        return Hr;
    }

    private static int EndCompositionCore(TsfState State, uint EditCookie, bool ClearText) {
        if (State.Composition == IntPtr.Zero) {
            State.Preedit.Clear();
            return SOk;
        }
        if (ClearText) {
            SetCompositionText(State, EditCookie, string.Empty);
        }
        var CompositionVtbl = *(IntPtr**)State.Composition;
        var End = (delegate* unmanaged<IntPtr, uint, int>)CompositionVtbl[6];
        var Hr = End(State.Composition, EditCookie);
        Log("EndComposition hr=0x" + Hr.ToString("X8"));
        ReleaseCom(State.Composition);
        State.Composition = IntPtr.Zero;
        State.Preedit.Clear();
        return Hr;
    }

    private static void ReleaseComposition(TsfState State) {
        if (State.Composition != IntPtr.Zero) {
            try {
                ReleaseCom(State.Composition);
            }
            catch (Exception Exception) {
                Log("ReleaseComposition EX " + Exception);
            }
            State.Composition = IntPtr.Zero;
        }
    }

    private static int OnCompositionTerminatedCore(IntPtr ThisPtr, uint EditCookie, IntPtr Composition) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                return EFail;
            }
            lock (State) {
                Log("OnCompositionTerminated cookie=" + EditCookie);
                State.Composition = IntPtr.Zero;
                State.Preedit.Clear();
            }
            return SOk;
        }
        catch (Exception Exception) {
            Log("OnCompositionTerminated EX " + Exception);
            return EFail;
        }
    }

    private static int EditSessionQueryCore(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
        var State = GetState(ThisPtr);
        if (State is null) {
            return ENoInterface;
        }
        if (*Riid == IidIUnknown || *Riid == IidEditSession) {
            *Ppv = ThisPtr;
            Interlocked.Increment(ref State.RefCount);
            return SOk;
        }
        *Ppv = IntPtr.Zero;
        return ENoInterface;
    }

    private static int CompSinkQueryCore(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
        var State = GetState(ThisPtr);
        if (State is null) {
            return ENoInterface;
        }
        if (*Riid == IidIUnknown || *Riid == IidCompositionSink) {
            *Ppv = ThisPtr;
            Interlocked.Increment(ref State.RefCount);
            return SOk;
        }
        *Ppv = IntPtr.Zero;
        return ENoInterface;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CfgCallTargetInfo {
        public IntPtr Offset;
        public IntPtr Flags;
    }

    [DllImport("kernelbase.dll", SetLastError = true)]
    private static extern bool SetProcessValidCallTargets(IntPtr Process, IntPtr VirtualAddress, UIntPtr RegionSize, uint NumberOfOffsets, [In] CfgCallTargetInfo[] OffsetInformation);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    /// <summary>
    /// 把我們 vtable 裡的函式位址註冊成 CFG 合法間接呼叫目標。
    /// TSF（msctf.dll）是 CFG 保護的，它間接呼叫我們的 sink 時若目標不在 bitmap 內，
    /// 會直接 fail-fast，連我們的函式都不會進去；NativeAOT 產生的 DLL 沒有 guard CF function table，
    /// 所以必須自己註冊。
    /// </summary>
    private static void CollectTargets(IntPtr* Table, int Count, List<IntPtr> Targets) {
        for (var Index = 0; Index < Count; Index++) {
            var Slot = Table[Index];
            if (Slot != IntPtr.Zero && !Targets.Contains(Slot)) {
                Targets.Add(Slot);
            }
        }
    }

    private static void RegisterCfgTargets() {
        try {
            var Targets = new List<IntPtr>();
            CollectTargets(TipVtbl, 5, Targets);
            CollectTargets(SinkVtbl, 9, Targets);
            CollectTargets(FactoryVtbl, 5, Targets);
            CollectTargets(EditSessionVtbl, 4, Targets);
            CollectTargets(CompSinkVtbl, 4, Targets);
            foreach (var Target in Targets) {
                var Page = (IntPtr)(Target.ToInt64() & ~0xFFFL);
                var Info = new[] { new CfgCallTargetInfo { Offset = (IntPtr)(Target.ToInt64() - Page.ToInt64()), Flags = (IntPtr)1 } };
                var Ok = SetProcessValidCallTargets(GetCurrentProcess(), Page, (UIntPtr)0x1000, 1, Info);
                Log("CFG register " + Target.ToString("X") + " ok=" + Ok);
            }
        }
        catch (Exception Exception) {
            Log("CFG register EX " + Exception);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr AddVectoredExceptionHandler(uint First, IntPtr Handler);

    [UnmanagedCallersOnly]
    private static int VectoredExceptionHandler(IntPtr ExceptionInfo) {
        try {
            // EXCEPTION_RECORD: Code(+0) Flags(+4) Next(+8) Address(+16) NumberParameters(+24) Information(+32)
            var Info = (IntPtr*)ExceptionInfo;
            var Record = (IntPtr*)Info[0];
            var Code = (uint)Record[0].ToInt64();
            var Address = Record[2];
            var ParamCount = (uint)(Record[3].ToInt64() & 0xFFFFFFFF);
            var Info0 = Record[4];
            var Info1 = Record[5];
            Log("VEH code=0x" + Code.ToString("X8") + " addr=" + Address.ToString("X") + " n=" + ParamCount + " info0=" + Info0.ToString("X") + " info1=" + Info1.ToString("X"));
        }
        catch {
        }
        return 0;
    }

    private static void Log(string Message) {
        try {
            var DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NganImePoc");
            Directory.CreateDirectory(DirectoryPath);
            var Line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + Environment.ProcessId + "] " + Message + Environment.NewLine;
            lock (LogGate) {
                File.AppendAllText(Path.Combine(DirectoryPath, "tsf-native.log"), Line, Encoding.UTF8);
            }
        }
        catch {
        }
    }

    [UnmanagedCallersOnly]
    private static int EditSessionQueryEntry(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) => EditSessionQueryCore(ThisPtr, Riid, Ppv);

    [UnmanagedCallersOnly]
    private static int DoEditSessionEntry(IntPtr ThisPtr, uint EditCookie) => DoEditSessionCore(ThisPtr, EditCookie);

    [UnmanagedCallersOnly]
    private static int CompSinkQueryEntry(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) => CompSinkQueryCore(ThisPtr, Riid, Ppv);

    [UnmanagedCallersOnly]
    private static int OnCompositionTerminatedEntry(IntPtr ThisPtr, uint EditCookie, IntPtr Composition) => OnCompositionTerminatedCore(ThisPtr, EditCookie, Composition);

    [UnmanagedCallersOnly]
    private static int QueryInterfaceEntry(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) { Log("QI-entry this=" + ThisPtr.ToString("X")); return QueryCore(ThisPtr, Riid, Ppv); }

    [UnmanagedCallersOnly]
    private static int AddRefEntry(IntPtr ThisPtr) { Log("AddRef-entry this=" + ThisPtr.ToString("X")); return AddRefCore(ThisPtr); }

    [UnmanagedCallersOnly]
    private static int ReleaseEntry(IntPtr ThisPtr) { Log("Release-entry this=" + ThisPtr.ToString("X")); return ReleaseCore(ThisPtr); }

    [UnmanagedCallersOnly]
    private static int ActivateEntry(IntPtr ThisPtr, IntPtr ThreadManager, uint ClientId) => ActivateCore(ThisPtr, ThreadManager, ClientId);

    [UnmanagedCallersOnly]
    private static int DeactivateEntry(IntPtr ThisPtr) => DeactivateCore(ThisPtr);

    [UnmanagedCallersOnly]
    private static int OnSetFocusEntry(IntPtr ThisPtr, int IsForeground) { Log("OnSetFocus-entry this=" + ThisPtr.ToString("X")); return OnSetFocusCore(ThisPtr, IsForeground); }

    [UnmanagedCallersOnly]
    private static int OnTestKeyDownEntry(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) => OnTestKeyDownCore(ThisPtr, Context, WParam, LParam, IsEaten);

    [UnmanagedCallersOnly]
    private static int OnTestKeyUpEntry(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) => OnTestKeyUpCore(ThisPtr, Context, WParam, LParam, IsEaten);

    [UnmanagedCallersOnly]
    private static int OnKeyDownEntry(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) => OnKeyDownCore(ThisPtr, Context, WParam, LParam, IsEaten);

    [UnmanagedCallersOnly]
    private static int OnKeyUpEntry(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) => OnKeyUpCore(ThisPtr, Context, WParam, LParam, IsEaten);

    [UnmanagedCallersOnly]
    private static int OnPreservedKeyEntry(IntPtr ThisPtr, IntPtr Context, Guid* PreservedKey, int* IsEaten) => OnPreservedKeyCore(ThisPtr, Context, PreservedKey, IsEaten);

    [UnmanagedCallersOnly]
    private static int FactoryQueryInterfaceEntry(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) => FactoryQueryCore(ThisPtr, Riid, Ppv);

    [UnmanagedCallersOnly]
    private static int FactoryAddRefEntry(IntPtr ThisPtr) => Interlocked.Increment(ref FactoryRefCount);

    [UnmanagedCallersOnly]
    private static int FactoryReleaseEntry(IntPtr ThisPtr) => Interlocked.Decrement(ref FactoryRefCount);

    [UnmanagedCallersOnly]
    private static int CreateInstanceEntry(IntPtr ThisPtr, IntPtr Outer, Guid* Riid, IntPtr* Ppv) => CreateInstanceCore(ThisPtr, Outer, Riid, Ppv);

    [UnmanagedCallersOnly]
    private static int LockServerEntry(IntPtr ThisPtr, int Lock) => SOk;
}
