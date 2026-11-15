namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using Ngan.Ime.Tsf.Ipc;

/// <summary>
/// NativeAOT 版的 TSF 薄外殼。整個 DLL 不帶 CLR，只做三件事：
/// 1. 提供 DllGetClassObject 與 IClassFactory，讓 Windows 建立文字服務；
/// 2. 實作 ITfTextInputProcessor 與 ITfKeyEventSink 的 vtable（手寫；不以 `[ComImport]` 走內建 COM）；
/// 3. 把 preedit 透過既有命名管道丟給 Avalonia 候選窗，文件內容留到後續階段。
/// </summary>
/// <remarks>
/// 對外呼叫的介面分兩類，別搞混：
/// + *已被呼叫端*（實作 ITfTextInputProcessor、ITfKeyEventSink、ITfEditSession、IClassFactory）：
///   仍舊手寫 vtable，表格見下。
/// + *呼叫端*（ITfContext、ITfContextView、ITfRange、ITfKeystrokeMgr）：
///   ITfContextView 已改用生成式宣告（見 `ITfContextViewGen.cs`），其餘仍舊手寫。
///   遷移中的狀態，兩者在同一個檔案裏並存是刻意的，不是漏改。
///
/// vtable 對照表（依 mingw 的 `msctf.h` 逐一核對，2026-11-15）：
/// + `ITfContext`：3 RequestEditSession、4 InWriteSession、5 GetSelection、
///   6 SetSelection、7 GetStart、8 GetEnd、9 GetActiveView、10 EnumViews、11 GetStatus。
///   少算 6 的 SetSelection 一格，GetActiveView 就會指到 GetEnd——2026-11-15 的 notepad 崩潰就是這樣。
/// + `ITfContextView`：3 GetRangeFromPoint、4 GetTextExt、5 GetScreenExt、6 GetWnd。
/// + `ITfRange`：3 GetText、4 SetText、…、15 Collapse。
/// + `ITfEditSession`：3 DoEditSession。
/// + `ITfKeystrokeMgr`：3 AdviseKeyEventSink、4 UnadviseKeyEventSink。
/// + `ITfTextInputProcessor`：3 Activate、4 Deactivate。
/// + `ITfKeyEventSink`：3 OnSetFocus、4 OnTestKeyDown、5 OnTestKeyUp、6 OnKeyDown、
///   7 OnKeyUp、8 OnPreservedKey。
///
/// *目前沒有組字範圍（composition）*：本殼只做「送鍵 → 取回上屏文字 → SetText」。
/// 先前有一組 SetCompositionText／EndCompositionCore 的殘留代碼，但沒有任何 StartComposition
/// 的呼叫點，等於死代碼；已於 2026-11-15 的審查中移除，免得下一個人以為 inline preedit 只差一步。
/// 真要支援 inline preedit（或讓 Chromium 的 GetTextExt 可靠），必須照 Weasel 的做法
/// 建一個真正的組字範圍並塞零寬空格（WeaselTSF/Composition.cpp:45-56，issue #883），
/// 那是獨立一輪的工作，見本檔 RequestCaretMeasure 的註釋。
/// </remarks>
internal static unsafe partial class NativeTsf {
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

    private const int SOk = 0;
    private const int SFalse = 1;
    private const int ENoInterface = unchecked((int)0x80004002);
    private const int EFail = unchecked((int)0x80004005);
    private const int EInvalidArg = unchecked((int)0x80070057);
    private const int ENoAggregation = unchecked((int)0x80040110);


    private const uint TfEsSync = 0x1;
    private const uint TfEsRead = 0x2;
    private const uint TfEsReadWrite = 0x6;
    /// <summary>非同步、不急：TSF 會排進佇列，量插入點用這一個。</summary>
    private const uint TfEsAsyncDontCare = 0x8;
    private const uint TfAnchorEnd = 1;

    /// <summary>螢幕矩形；手寫 vtable 與生成式宣告共用這一個結構。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>選取範圍；手寫 vtable 與生成式宣告共用。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct TfSelection {
        public IntPtr Range;
        public uint ActiveEnd;
        public int Interim;
    }

    private static readonly IntPtr* TipVtbl;
    private static readonly IntPtr* SinkVtbl;
    private static readonly IntPtr* FactoryVtbl;
    private static readonly IntPtr* EditSessionVtbl;
    /// <summary>量插入點專用的 ITfEditSession vtable（與上屏用的那一個完全分開）。</summary>
    private static readonly IntPtr* CaretSessionVtbl;

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

        // 量插入點用另一個 edit session 物件：兩個請求若共用同一個 view 與同一個
        // 「待辦操作」欄位，先到的 callback 會吃掉後到的操作（上屏憑空消失）。
        CaretSessionVtbl = (IntPtr*)NativeMemory.Alloc(4, (nuint)sizeof(IntPtr));
        CaretSessionVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&CaretSessionQueryEntry;
        CaretSessionVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&AddRefEntry;
        CaretSessionVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&ReleaseEntry;
        CaretSessionVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, uint, int>)&DoCaretSessionEntry;

        RegisterCfgTargets();
        AddVectoredExceptionHandler(1, (IntPtr)(delegate* unmanaged<IntPtr, int>)&VectoredExceptionHandler);
        // 一律落盤：行程一載入這支 DLL 就留一行，連「TSF 有沒有來要 class factory」都還沒發生。
        // 排查「某個應用切不過來」時，這一行有沒有出現就是第一個分界。
        LogWarn("shell loaded pid=" + Environment.ProcessId + " path=" + (GetOwnModulePath() is { Length: > 0 } P ? P : "?"));
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
        /// <summary>ITfEditSession 的介面指標（自己實作，交給 ITfContext::RequestEditSession）。</summary>
        public IntPtr EditSessionView;
        /// <summary>ITfEditSession 的 GCHandle。</summary>
        public IntPtr EditSessionHandle;
        /// <summary>量插入點專用的 ITfEditSession 介面指標；與上屏用的那一個分開。</summary>
        public IntPtr CaretSessionView;
        /// <summary>量插入點專用 edit session 的 GCHandle。</summary>
        public IntPtr CaretSessionHandle;
        /// <summary>目前按鍵所屬的 ITfContext（只在 edit session 內有效）。</summary>
        public IntPtr Context;
        /// <summary>待執行的編輯操作：0 無、2 上屏。</summary>
        public uint PendingOperation;
        /// <summary>待上屏的文字（由宿主回傳的 commit 合併）。</summary>
        public string PendingCommit = "";
        /// <summary>按下鍵的查詢結果；等 OnKeyDown 領取。</summary>
        public TsfReply? ReplyDown;
        /// <summary>擡起鍵的查詢結果；等 OnKeyUp 領取。</summary>
        public TsfReply? ReplyUp;
        /// <summary>
        /// 按鍵決策：吃不吃、徵詢與執行怎麼配對、上下成對的保證。
        /// </summary>
        /// <remarks>
        /// 這一項取代了原本的 DownPending／UpPending 兩個布林旗標。
        /// 那兩個旗標沒有定義「合法組合」，應用不照協議送事件時就會留下殘留狀態，
        /// 之後每一顆鍵都被誤判成「已問過且已吃掉」。
        /// </remarks>
        public readonly Ngan.Ime.Tsf.Ipc.TsfKeyPolicy Policy = new();
        /// <summary>插入點在螢幕上的左邊界（像素）；僅在 CaretValid 為 true 時有意義。</summary>
        public int CaretPointX;
        /// <summary>插入點在螢幕上的下邊界（像素）；僅在 CaretValid 為 true 時有意義。</summary>
        public int CaretPointY;
        /// <summary>是否量到過插入點。false 時 x/y 的 0 代表「未知」，不是螢幕左上角。</summary>
        public bool CaretValid;
        /// <summary>量測用的檢視（托管參考，自帶一份 COM 參考）；null 表示沒有進行中的量測。</summary>
        public ITfContextViewGen? CaretViewGen;
        /// <summary>量測用的 context（托管參考）；null 表示沒有進行中的量測。</summary>
        public ITfContextGen? CaretContextGen;
        /// <summary>是否已排入一次插入點量測（1 表示進行中）；用 int 以便 Interlocked。</summary>
        public int CaretPending;
        /// <summary>上一次排入量測的時間（TickCount64），用來節流：一顆鍵不必量四次。</summary>
        public long CaretMeasureTick;
    }

    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
    private static int DllGetClassObject(Guid* Rclsid, Guid* Riid, IntPtr* Ppv) {
        try {
            LogWarn("DllGetClassObject pid=" + Environment.ProcessId + " clsid=" + *Rclsid + " riid=" + *Riid);
            if (Rclsid is null || Riid is null || Ppv is null) {
                return EInvalidArg;
            }
            // 不再比對 CLSID：每次安裝用屬於自己的 CLSID 註冊（版本化安裝＝一個獨立輸入法），
            // 只要系統按註冊表找上這個 DLL，就回同一個 class factory。
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
            LogWarn("CreateInstance riid=" + *Riid);
            var State = new TsfState();
            State.TipView = MakeView(TipVtbl, State, out var TipHandle);
            State.TipHandle = TipHandle;
            State.SinkView = MakeView(SinkVtbl, State, out var SinkHandle);
            State.SinkHandle = SinkHandle;
            State.EditSessionView = MakeView(EditSessionVtbl, State, out var EditSessionHandle);
            State.EditSessionHandle = EditSessionHandle;
            State.CaretSessionView = MakeView(CaretSessionVtbl, State, out var CaretSessionHandle);
            State.CaretSessionHandle = CaretSessionHandle;

            if (*Riid == IidIUnknown || *Riid == IidTextInputProcessor) {
                *Ppv = State.TipView;
                LogWarn("CreateInstance ok tip=" + State.TipView.ToString("X"));
                return SOk;
            }
            if (*Riid == IidKeyEventSink) {
                *Ppv = State.SinkView;
                LogWarn("CreateInstance ok sink=" + State.SinkView.ToString("X"));
                return SOk;
            }
            *Ppv = IntPtr.Zero;
            LogWarn("CreateInstance E_NOINTERFACE riid=" + *Riid);
            return ENoInterface;
        }
        catch (Exception Exception) {
            Log("CreateInstance EX " + Exception);
            return EFail;
        }
    }

    /// <summary>
    /// 啟動文字服務：取得 ITfKeystrokeMgr 並把本物件的 key event sink 掛上去。
    /// </summary>
    /// <remarks>
    /// *為何要 AdviseKeyEventSink 的第三個參數為 1*：`fForeground` 表示「即使輸入焦點不在本服務
    /// 也要收到按鍵」。少了它，密碼框這類停用輸入法的控件會讓所有按鍵都到不了引擎。
    ///
    /// *已移除的探針*：先前這裏有 `NGAN_TSF_MODE=none/unadvise/nullsink` 三個分支與一段
    /// 「self-test sink QI」，那是 POC 期在查 CFG 與 advise 是否生效用的。
    /// 正式路徑只留一條，探針的代價是 60 行樣板把真正的邏輯埋掉。
    /// </remarks>
    private static int ActivateCore(IntPtr ThisPtr, IntPtr ThreadManager, uint ClientId) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                return EFail;
            }
            State.ClientId = ClientId;
            var iid = IidKeystrokeMgr;
            IntPtr KeystrokeMgr = IntPtr.Zero;
            var Hr = Marshal.QueryInterface(ThreadManager, in iid, out KeystrokeMgr);
            if (Hr < 0) {
                LogError("Activate QI(ITfKeystrokeMgr) hr=0x" + Hr.ToString("X8"));
                return Hr;
            }
            var KsGen = ComInterfaceMarshaller<ITfKeystrokeMgrGen>.ConvertToManaged((void*)KeystrokeMgr);
            if (KsGen is null) {
                LogError("Activate ConvertToManaged(ITfKeystrokeMgr) 回空");
                ReleaseCom(KeystrokeMgr);
                return EFail;
            }
            LogWarn("Activate QI(ITfKeystrokeMgr) ok mgr=" + KeystrokeMgr.ToString("X"));
            Hr = KsGen.AdviseKeyEventSink(ClientId, State.SinkView, 1);
            if (Hr < 0) {
                LogError("Activate AdviseKeyEventSink hr=0x" + Hr.ToString("X8"));
                ReleaseCom(KeystrokeMgr);
                return Hr;
            }
            State.KeystrokeMgr = KeystrokeMgr;
            LogWarn("Activate ok clientId=" + ClientId + " pid=" + Environment.ProcessId);
            return SOk;
        }
        catch (Exception Exception) {
            LogError("Activate EX " + Exception);
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
                var KsGen = ComInterfaceMarshaller<ITfKeystrokeMgrGen>.ConvertToManaged((void*)State.KeystrokeMgr);
                if (KsGen is not null) {
                    KsGen.UnadviseKeyEventSink(State.ClientId);
                }
                ReleaseCom(State.KeystrokeMgr);
                State.KeystrokeMgr = IntPtr.Zero;
            }
            State.Policy.Reset();
            CancelCaretMeasure(State);
            SendHide();
            LogWarn("Deactivate done clientId=" + State.ClientId);
            return SOk;
        }
        catch (Exception Exception) {
            LogError("Deactivate EX " + Exception);
            return EFail;
        }
    }

    private static int OnSetFocusCore(IntPtr ThisPtr, int IsForeground) {
        try {
            Log("OnSetFocus fg=" + IsForeground);
            var State = GetState(ThisPtr);
            if (State is null) {
                return SOk;
            }
            if (IsForeground == 0) {
                // 失去焦點：按鍵記錄與插入點都不再有意義，留著只會被下一個控件繼承。
                State.Policy.Reset();
                CancelCaretMeasure(State);
                State.CaretValid = false;
                SendHide();
            }
            return SOk;
        }
        catch (Exception Exception) {
            LogError("OnSetFocus EX " + Exception);
            return EFail;
        }
    }

    /// <summary>目前按著的修飾鍵位；位元值由協議層定義，殼端不再自己寫一份數字。</summary>
    private static int GetModifiers() {
        var Mods = 0;
        if ((GetKeyState(VkShift) & 0x8000) != 0) { Mods |= TsfIpc.ModShift; }
        if ((GetKeyState(VkControl) & 0x8000) != 0) { Mods |= TsfIpc.ModCtrl; }
        if ((GetKeyState(VkMenu) & 0x8000) != 0) { Mods |= TsfIpc.ModAlt; }
        return Mods;
    }

    /// <summary>VK_SHIFT；與 <see cref="TsfKeyPolicy.VkShift"/> 同值，這裡留常量是為了讀代碼時不必跳檔。</summary>
    private const int VkShift = TsfKeyPolicy.VkShift;
    /// <summary>VK_CONTROL。</summary>
    private const int VkControl = TsfKeyPolicy.VkCtrl;
    /// <summary>VK_MENU（Alt）。</summary>
    private const int VkMenu = TsfKeyPolicy.VkMenu;

    /// <summary>
    /// 一顆鍵的「按下／擡起」共同流程：結算反向事件、量插入點、決定吃不吃、必要時上屏。
    /// </summary>
    /// <remarks>
    /// 四個回調（TestKeyDown／TestKeyUp／KeyDown／KeyUp）只差三件事：
    /// 方向、要不要問引擎、問完之後要不要把結果留給對向回調領。
    /// 把流程收在一處，避免「某一條路徑忘了清旗標」這種不對稱缺陷。
    /// </remarks>
    /// <param name="State">文字服務狀態。</param>
    /// <param name="Context">這次按鍵所屬的 ITfContext。</param>
    /// <param name="VirtualKey">Win32 虛擬鍵碼。</param>
    /// <param name="IsUp">true 表示擡起。</param>
    /// <param name="IsTest">true 表示來自 OnTestKeyXxx（結果要留給 OnKeyXxx 領）。</param>
    /// <param name="IsEaten">回報給 TSF 的「是否吃掉」。</param>
    private static void HandleKeyEvent(TsfState State, IntPtr Context, int VirtualKey, bool IsUp, bool IsTest, int* IsEaten) {
        // step 1: 結算反方向尚未領走的徵詢結論（應用可能只送其中一半的事件）。
        State.Policy.SettleOpposite(IsUp);
        // step 2: 排一次插入點量測（非同步；量到的位置給下一次顯示用）。
        BeginCaretMeasure(State, Context);
        // step 3: 修飾鍵一律放行，不問引擎、不記錄。
        if (TsfKeyPolicy.IsPassThrough(VirtualKey)) {
            *IsEaten = 0;
            return;
        }
        var Cached = IsUp ? State.ReplyUp : State.ReplyDown;
        if (IsTest) {
            // 同一顆鍵的重複徵詢：沿用上一次的結論，不再送管道（否則引擎會處理兩次）。
            if (State.Policy.TryReuseTest(IsUp, VirtualKey)) {
                *IsEaten = 1;
                return;
            }
            Cached = null;
        }
        else if (State.Policy.TryTakeKey(IsUp, VirtualKey, out var PendingEaten)) {
            // 徵詢已經問過，這裡只領結果，不再問引擎。
            *IsEaten = PendingEaten ? 1 : 0;
            if (Cached is not null) {
                // 有 commit 就上屏：吃不吃是「按鍵歸誰」的問題，上屏是「字有沒有進去」的問題。
                CommitIfAny(State, Context, Cached);
                ClearReply(State, IsUp);
            }
            LogKey(IsUp, IsTest, VirtualKey, *IsEaten, Cached);
            return;
        }
        ResolveKeyAnchor(State, out var AnchorX, out var AnchorY);
        var Reply = QueryKey(VirtualKey, GetModifiers(), AnchorX, AnchorY, IsUp);
        var Eat = State.Policy.DecideEat(VirtualKey, IsUp, Reply is { Eaten: true });
        *IsEaten = Eat ? 1 : 0;
        if (Eat) {
            // 只有吃掉才需要把結果留給 OnKeyXxx；沒吃掉時應用的按鍵照常走，不必再問一次。
            SetReply(State, IsUp, Reply);
        } else {
            ClearReply(State, IsUp);
        }
        if (Reply is not null) {
            CommitIfAny(State, Context, Reply);
        }
        if (IsTest && Eat) {
            State.Policy.SetTest(IsUp, VirtualKey, true);
        }
        LogKey(IsUp, IsTest, VirtualKey, *IsEaten, Reply);
    }

    /// <summary>記下這一顆鍵的引擎回應，等對向回調領取。</summary>
    private static void SetReply(TsfState State, bool IsUp, TsfReply? Reply) {
        if (IsUp) {
            State.ReplyUp = Reply;
        } else {
            State.ReplyDown = Reply;
        }
    }

    /// <summary>丟掉上一顆鍵留下的引擎回應。</summary>
    private static void ClearReply(TsfState State, bool IsUp) {
        if (IsUp) {
            State.ReplyUp = null;
        } else {
            State.ReplyDown = null;
        }
    }

    /// <summary>逐鍵日誌；只在 NGAN_TSF_LOG=1 時寫（由 Log 自己判斷）。</summary>
    private static void LogKey(bool IsUp, bool IsTest, int VirtualKey, int Eaten, TsfReply? Reply) {
        if (!VerboseLog) {
            return;
        }
        Log((IsUp ? "Up" : "Down") + (IsTest ? "(test)" : "") + " vk=" + VirtualKey
            + " eat=" + Eaten + " cand=" + (Reply?.Candidates.Count ?? 0)
            + " preedit=" + (Reply?.Preedit?.Length ?? 0)
            + " err=" + (Reply?.Err.ToString() ?? "none"));
    }

    private static int OnTestKeyDownCore(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                *IsEaten = 0;
                return EFail;
            }
            HandleKeyEvent(State, Context, (int)WParam.ToInt64(), false, true, IsEaten);
            return SOk;
        }
        catch (Exception Exception) {
            LogError("OnTestKeyDown EX " + Exception);
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
            HandleKeyEvent(State, Context, (int)WParam.ToInt64(), false, false, IsEaten);
            return SOk;
        }
        catch (Exception Exception) {
            LogError("OnKeyDown EX " + Exception);
            *IsEaten = 0;
            return EFail;
        }
    }

    private static int OnTestKeyUpCore(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                *IsEaten = 0;
                return EFail;
            }
            HandleKeyEvent(State, Context, (int)WParam.ToInt64(), true, true, IsEaten);
            return SOk;
        }
        catch (Exception Exception) {
            LogError("OnTestKeyUp EX " + Exception);
            *IsEaten = 0;
            return EFail;
        }
    }

    private static int OnKeyUpCore(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                *IsEaten = 0;
                return EFail;
            }
            HandleKeyEvent(State, Context, (int)WParam.ToInt64(), true, false, IsEaten);
            return SOk;
        }
        catch (Exception Exception) {
            LogError("OnKeyUp EX " + Exception);
            *IsEaten = 0;
            return EFail;
        }
    }

    /// <summary>
    /// 回應帶了上屏文字時，開一次可寫的編輯會話把它寫進應用。
    /// </summary>
    /// <remarks>
    /// *為何要看 SessionResult*：`RequestEditSession` 回 `S_OK` 只代表請求排進去了，
    /// session 真正執行的結果在 `SessionResult`。上屏失敗是使用者直接看得到的錯
    /// （字沒進輸入框），必須升到 Error，不能只記 Trace。
    /// </remarks>
    private static void CommitIfAny(TsfState State, IntPtr Context, TsfReply Result) {
        if (Result.Commits.Count == 0) {
            return;
        }
        State.PendingCommit = string.Concat(Result.Commits);
        State.PendingOperation = 2;
        var Hr = RequestEditSession(State, Context);
        if (Hr < 0) {
            LogError("Commit RequestEditSession hr=0x" + Hr.ToString("X8") + " len=" + State.PendingCommit.Length);
        }
        else {
            Log("Commit len=" + State.PendingCommit.Length);
        }
    }

    private static int OnPreservedKeyCore(IntPtr ThisPtr, IntPtr Context, Guid* PreservedKey, int* IsEaten) {
        Log("OnPreservedKey");
        *IsEaten = 0;
        return SOk;
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr Hwnd, out uint ProcessId);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int VirtualKey);

    /// <summary>對一個 COM 介面指標呼叫 Release（vtable slot 2）。</summary>
    private static void ReleaseCom(IntPtr ComObject) {
        var Vtbl = *(IntPtr***)ComObject;
        var Release = (delegate* unmanaged<IntPtr, int>)Vtbl[2];
        Release(ComObject);
    }

    /// <summary>對一個 COM 介面指標呼叫 AddRef（vtable slot 1）。</summary>
    private static void AddRefCom(IntPtr ComObject) {
        var Vtbl = *(IntPtr***)ComObject;
        var AddRef = (delegate* unmanaged<IntPtr, int>)Vtbl[1];
        AddRef(ComObject);
    }

    /// <summary>兩次插入點量測的最小間隔（毫秒）；一顆鍵會經過四個回調，沒必要量四次。</summary>
    private const int CaretMeasureIntervalMs = 40;

    /// <summary>一次量測最多允許在途多久（毫秒）；超過視為卡住，收掉重排。</summary>
    private const int CaretStaleMs = 2000;

    /// <summary>兜底位置離前景視窗左上角的位移（像素）；只是「別黏在螢幕角落」的意思。</summary>
    private const int FallbackAnchorOffsetX = 24;

    /// <summary>兜底位置離前景視窗頂端的位移（像素）；避開標題列。</summary>
    private const int FallbackAnchorOffsetY = 96;

    /// <summary>
    /// 排一次「插入點量測」：非同步、唯讀的 edit session，量到的座標留給下一次顯示候選窗。
    /// </summary>
    /// <remarks>
    /// *為何非同步*：Weasel 用的是 `TF_ES_ASYNCDONTCARE | TF_ES_READ`（WeaselTSF/Composition.cpp:211）。
    /// 同步 session 在應用已經持有 session 時會回 `TF_E_SYNCHRONOUS`，而這段跑在應用行程的
    /// UI 執行緒上，每顆鍵多一次同步往返，連發按鍵（AltNav 一次四到十六顆）就明顯卡。
    /// 插入點在兩顆鍵之間幾乎不會移動，用上一次量到的值完全夠。
    ///
    /// *為何不用 Win32 的 caret 當主路徑*：Chromium 系（Edge、VS Code）沒有原生 caret，
    /// `GetGUIThreadInfo` 回傳的矩形是空的。TSF 的 `ITfContextView::GetTextExt` 才拿得到，
    /// 小狼毫也是這樣做的；Win32 那條只留作兜底。
    ///
    /// *為何狀態只有一個 `CaretPending` 旗標而且一定要還原*：先前版本把「已排入」與
    /// 「待辦操作」混在同一個欄位，`RequestEditSession` 回 `S_OK` 但 session 沒跑時
    /// 旗標永遠停在 true，之後每一顆鍵都在第一行返回，插入點永遠是 (0,0)，
    /// 候選窗就黏在螢幕左上角。現在任何失敗路徑都會把槽位還原。
    ///
    /// *為何還需要一個「卡住」的看門狗*：非同步 session 由 TSF 決定何時執行，
    /// 應用在忙、context 正在失效、或它根本不理會請求時，callback 可能永遠不來。
    /// 超過 <see cref="CaretStaleMs"/> 就把槽位收掉重排——寧可多量一次，也不要永遠不再量。
    /// </remarks>
    private static void BeginCaretMeasure(TsfState State, IntPtr Context) {
        if (Context == IntPtr.Zero) {
            return;
        }
        var Now = Environment.TickCount64;
        // 已有一次在途：正常會在幾毫秒內回來；超過門檻就是卡住了，收掉重排。
        if (Interlocked.CompareExchange(ref State.CaretPending, 1, 0) != 0) {
            if (Now - State.CaretMeasureTick <= CaretStaleMs) {
                return;
            }
            LogWarn("Caret measure stuck for " + (Now - State.CaretMeasureTick) + "ms; resetting");
            ReleaseCaretSlot(State);
            if (Interlocked.CompareExchange(ref State.CaretPending, 1, 0) != 0) {
                return;
            }
        }
        if (Now - State.CaretMeasureTick < CaretMeasureIntervalMs) {
            Interlocked.Exchange(ref State.CaretPending, 0);
            return;
        }
        State.CaretMeasureTick = Now;
        try {
            // 生成式宣告：GetActiveView 由生成器排在 9，不會再指到 8 的 GetEnd。
            // 2026-11-15 那次 notepad 崩潰就是手寫成 8（實際是 GetEnd）造成的。
            var ContextGen = ComInterfaceMarshaller<ITfContextGen>.ConvertToManaged((void*)Context);
            if (ContextGen is null) {
                LogWarn("Caret ConvertToManaged(context) 回空 ctx=" + Context.ToString("X"));
                ReleaseCaretSlot(State);
                return;
            }
            var ViewHr = ContextGen.GetActiveView(out var ViewGen);
            if (ViewHr < 0 || ViewGen is null) {
                LogWarn("Caret GetActiveView hr=0x" + ViewHr.ToString("X8"));
                ReleaseCaretSlot(State);
                return;
            }
            // 量測期間要留住這兩個物件：session 是非同步的，回到我們這裏時按鍵已經處理完了。
            // 托管介面自帶一份 COM 參考，舊的直接丟掉即可，釋放交給回收。
            State.CaretViewGen = ViewGen;
            State.CaretContextGen = ContextGen;
            var SessionResult = 0;
            var Hr = ContextGen.RequestEditSession(State.ClientId, State.CaretSessionView, TfEsAsyncDontCare | TfEsRead, out SessionResult);
            if (Hr < 0 || SessionResult < 0) {
                LogWarn("Caret RequestEditSession hr=0x" + Hr.ToString("X8") + " result=0x" + SessionResult.ToString("X8"));
                ReleaseCaretSlot(State);
            }
        }
        catch (Exception Exception) {
            LogError("BeginCaretMeasure EX " + Exception);
            ReleaseCaretSlot(State);
        }
    }

    /// <summary>取消／結束一次量測：放掉持有的介面並把旗標歸零。任何失敗路徑都必須走到這裏。</summary>
    private static void CancelCaretMeasure(TsfState State) {
        ReleaseCaretSlot(State);
    }

    /// <summary>釋放量測槽位（丟掉托管參考）並清掉 pending 旗標。</summary>
    private static void ReleaseCaretSlot(TsfState State) {
        State.CaretViewGen = null;
        State.CaretContextGen = null;
        Interlocked.Exchange(ref State.CaretPending, 0);
    }

    /// <summary>
    /// 在 edit session 內量插入點；成敗都會把槽位還原。
    /// </summary>
    /// <remarks>
    /// *為何「量不到就退回 Win32」只在從未量到過時做*：一旦有過有效座標，
    /// 寧可沿用舊值（差一顆鍵的距離）也不要換成 Win32 的粗略值，否則浮窗會在
    /// 「量到」與「退回」之間跳來跳去。從未量到過（例如 Chromium）時才需要兜底。
    /// </remarks>
    private static int DoCaretSessionCore(IntPtr ThisPtr, uint EditCookie) {
        var State = GetState(ThisPtr);
        if (State is null) {
            return EFail;
        }
        var Context = State.CaretContextGen;
        var View = State.CaretViewGen;
        State.CaretContextGen = null;
        State.CaretViewGen = null;
        // 先歸零：session 裏任何提早返回都不會把「量測中」帶到下一個事件。
        Interlocked.Exchange(ref State.CaretPending, 0);
        try {
            if (Context is null || View is null) {
                return EFail;
            }
            var Adopted = MeasureCaret(State, Context, View, EditCookie, out var Rect, out var Hr, out var Clipped);
            // 成敗都記：hr 為負是呼叫失敗，四邊全零是這個控件不回報插入點，兩者處置不同。
            LogWarn("Caret GetTextExt hr=0x" + Hr.ToString("X8")
                + " rect=" + Rect.Left + "," + Rect.Top + "," + Rect.Right + "," + Rect.Bottom
                + " clipped=" + Clipped + " adopted=" + (Adopted ? 1 : 0));
            if (!Adopted && !State.CaretValid) {
                // 從未量到過：退回 Win32 的 caret，至少不要讓浮窗落在螢幕原點。
                TryTakeCaretFromGui(State);
            }
            return SOk;
        }
        catch (Exception Exception) {
            LogError("DoCaretSession EX " + Exception);
            return EFail;
        }
        finally {
            // 托管介面參考由回收處理，這裡沒有要放掉的東西。
        }
    }

    /// <summary>
    /// 用 `ITfContextView::GetTextExt` 量出插入點矩形，並判斷這個結果能不能用。
    /// </summary>
    /// <param name="Context">按鍵所屬的 context（已 AddRef）。</param>
    /// <param name="View">該 context 的 active view（已 AddRef）。</param>
    /// <param name="EditCookie">session 的 cookie。</param>
    /// <param name="Rect">量到的矩形。</param>
    /// <param name="Hr">GetTextExt 的 HRESULT。</param>
    /// <param name="Clipped">控件是否回報「文字被裁切」。</param>
    /// <returns>true 表示這個矩形已被採用。</returns>
    private static bool MeasureCaret(TsfState State, ITfContextGen Context, ITfContextViewGen View, uint EditCookie, out NativeRect Rect, out int Hr, out int Clipped) {
        Rect = default;
        Clipped = 0;
        var Selection = default(TfSelection);
        Hr = Context.GetSelection(EditCookie, 0, 1, out Selection, out var Fetched);
        if (Hr < 0 || Fetched != 1 || Selection.Range == IntPtr.Zero) {
            // 拿不到選取範圍，後面就不可能量到座標。
            LogWarn("Caret GetSelection hr=0x" + Hr.ToString("X8") + " fetched=" + Fetched + " range=" + Selection.Range.ToString("X"));
            return false;
        }
        try {
            // 源生成式 COM：vtable 由生成器排，這裡不再手寫槽位索引（見 ITfContextViewGen）。
            // ConvertToManaged 會替這個介面指標建立托管包裝，包裝自帶一份參考；
            // 這一份由 GC 在包裝不可達時釋放，我們自己那份仍照舊在 finally 裏放。
            var ClippedFlag = 0;
            Hr = View.GetTextExt(EditCookie, Selection.Range, out var Measured, out ClippedFlag);
            Rect = Measured;
            Clipped = ClippedFlag;
            if (Hr < 0) {
                return false;
            }
            return TryAdoptCaretRect(State, Measured);
        }
        finally {
            ReleaseCom(Selection.Range);
        }
    }

    /// <summary>
    /// 判斷量到的矩形能不能當插入點用，能用就寫進狀態。
    /// </summary>
    /// <remarks>
    /// 三個條件缺一不可：
    /// + 矩形不是全零（有些控件在沒有版面時回 0,0,0,0）；
    /// + 錨點落在前景視窗附近（有些控件回的是相對於視窗的座標，直接用會跑到螢幕外）；
    /// + 錨點在虛擬螢幕範圍內。
    /// 錨點取「左下角」＝插入點下方，候選窗從那裏往下排。
    /// </remarks>
    private static bool TryAdoptCaretRect(TsfState State, NativeRect Rect) {
        if (Rect.Left == 0 && Rect.Top == 0 && Rect.Right == 0 && Rect.Bottom == 0) {
            return false;
        }
        var X = Rect.Left;
        var Y = Rect.Bottom;
        if (!IsNearForegroundWindow(X, Y)) {
            return false;
        }
        State.CaretPointX = X;
        State.CaretPointY = Y;
        State.CaretValid = true;
        return true;
    }

    /// <summary>點是否落在前景視窗的範圍（往外放宽一點，容許插入點貼在邊界上）。</summary>
    private static bool IsNearForegroundWindow(int X, int Y) {
        var Hwnd = GetForegroundWindow();
        if (Hwnd == IntPtr.Zero) {
            return true;
        }
        if (!GetWindowRect(Hwnd, out var Rect)) {
            return true;
        }
        const int Slack = 64;
        return X >= Rect.Left - Slack && X <= Rect.Right + Slack
            && Y >= Rect.Top - Slack && Y <= Rect.Bottom + Slack;
    }

    /// <summary>
    /// 用 Win32 的 GUI thread info 取得插入點，作為 GetTextExt 量不到時的兜底。
    /// </summary>
    /// <remarks>
    /// *為何要看前景視窗的執行緒*：`GUITHREADINFO` 是「某個 GUI 執行緒」的狀態，
    /// 而 TSF 的 OnTestKeyDown 不一定跑在應用建立視窗的那條執行緒上（Chromium 系就是另一條）。
    ///
    /// *為何要試兩種座標解釋*：`rcCaret` 依文檔是螢幕座標，但實務上有控件回報客戶區座標。
    /// 這裡把兩種都算出來，取「落在前景視窗內」的那一個；兩個都合理時優先螢幕座標。
    /// 先前版本一律再呼叫一次 `ClientToScreen`，對螢幕座標的來源就會偏掉一個客戶區原點。
    /// </remarks>
    private static void TryTakeCaretFromGui(TsfState State) {
        try {
            var Foreground = GetForegroundWindow();
            var ThreadId = Foreground != IntPtr.Zero
                ? GetWindowThreadProcessId(Foreground, out _)
                : GetCurrentThreadId();
            var Info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
            if (!GetGUIThreadInfo(ThreadId, ref Info)) {
                return;
            }
            if (Info.CaretRect.Right == 0 && Info.CaretRect.Bottom == 0) {
                // 這個控件不回報 caret（Chromium 系就是這樣），維持原狀。
                return;
            }
            var Hwnd = Info.Caret != IntPtr.Zero
                ? Info.Caret
                : (Info.Focus != IntPtr.Zero ? Info.Focus : Foreground);
            var AsScreen = new NativePoint { X = Info.CaretRect.Left, Y = Info.CaretRect.Bottom };
            var AsClient = AsScreen;
            if (Hwnd != IntPtr.Zero && ClientToScreen(Hwnd, ref AsClient)) {
                // 兩個候選裏挑一個落在前景視窗內的。
                var UseClient = IsNearForegroundWindow(AsClient.X, AsClient.Y)
                    && !IsNearForegroundWindow(AsScreen.X, AsScreen.Y);
                if (UseClient) {
                    AsScreen = AsClient;
                }
            }
            State.CaretPointX = AsScreen.X;
            State.CaretPointY = AsScreen.Y;
            State.CaretValid = true;
            LogWarn("Caret GUITHREADINFO adopted x=" + State.CaretPointX + " y=" + State.CaretPointY);
        }
        catch (Exception Exception) {
            LogError("TryTakeCaretFromGui EX " + Exception);
        }
    }

    /// <summary>
    /// 送出按鍵請求時要附的插入點座標。
    /// </summary>
    /// <remarks>
    /// 座標有三級：量到的插入點 → 前景視窗左上角附近（從沒量到過時的兜底）→ (0,0)（未知）。
    /// (0,0) 由接收端解釋成「不知道位置」，候選窗因此不會被搬到螢幕原點。
    /// </remarks>
    private static void ResolveKeyAnchor(TsfState State, out int X, out int Y) {
        if (State.CaretValid) {
            X = State.CaretPointX;
            Y = State.CaretPointY;
            return;
        }
        var Hwnd = GetForegroundWindow();
        if (Hwnd != IntPtr.Zero && GetWindowRect(Hwnd, out var Rect)) {
            X = Rect.Left + FallbackAnchorOffsetX;
            Y = Rect.Top + FallbackAnchorOffsetY;
            return;
        }
        X = 0;
        Y = 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr Hwnd, out NativeRect Rect);

    private static int RequestEditSession(TsfState State, IntPtr Context) {
        State.Context = Context;
        var ContextGen = ComInterfaceMarshaller<ITfContextGen>.ConvertToManaged((void*)Context);
        if (ContextGen is null) {
            LogError("RequestEditSession: context 轉換失敗 ctx=" + Context.ToString("X"));
            return EFail;
        }
        var SessionResult = 0;
        // 上屏一定要同步讀寫：呼叫端要立刻知道字有沒有進去。
        var Hr = ContextGen.RequestEditSession(State.ClientId, State.EditSessionView, TfEsSync | TfEsReadWrite, out SessionResult);
        if (Hr < 0) {
            LogError("RequestEditSession hr=0x" + Hr.ToString("X8"));
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
            uint Operation;
            lock (State) {
                Operation = State.PendingOperation;
                State.PendingOperation = 0;
            }
            Log("DoEditSession op=" + Operation + " ec=0x" + EditCookie.ToString("X"));
            return Operation switch {
                2 => InsertTextCore(State, EditCookie),
                _ => SOk,
            };
        }
        catch (Exception Exception) {
            LogError("DoEditSession EX " + Exception);
            return EFail;
        }
    }

    /// <summary>
    /// 把目前 preedit 緩衝區的文字插入游標位置。
    /// 打字過程中完全不碰文件，只有空白鍵上屏時才呼叫 ITfRange::SetText。
    /// </summary>
    private static int InsertTextCore(TsfState State, uint EditCookie) {
        var ContextGen = ComInterfaceMarshaller<ITfContextGen>.ConvertToManaged((void*)State.Context);
        if (ContextGen is null) {
            LogError("InsertText: context 轉換失敗");
            return EFail;
        }
        var Selection = default(TfSelection);
        var Hr = ContextGen.GetSelection(EditCookie, 0, 1, out Selection, out var Fetched);
        Log("InsertText GetSelection hr=0x" + Hr.ToString("X8") + " fetched=" + Fetched);
        if (Hr < 0) {
            return Hr;
        }
        if (Fetched != 1 || Selection.Range == IntPtr.Zero) {
            return EFail;
        }
        var RangeGen = ComInterfaceMarshaller<ITfRangeGen>.ConvertToManaged((void*)Selection.Range);
        if (RangeGen is null) {
            LogError("InsertText: range 轉換失敗");
            ReleaseCom(Selection.Range);
            return EFail;
        }
        var Text = State.PendingCommit;
        fixed (char* TextPtr = Text) {
            Hr = RangeGen.SetText(EditCookie, 0, (IntPtr)TextPtr, Text.Length);
        }
        Log("InsertText SetText hr=0x" + Hr.ToString("X8") + " len=" + Text.Length);
        if (Hr >= 0) {
            var CollapseHr = RangeGen.Collapse(EditCookie, TfAnchorEnd);
            Log("InsertText Collapse hr=0x" + CollapseHr.ToString("X8"));
        }
        ReleaseCom(Selection.Range);
        State.PendingCommit = "";
        return Hr;
    }

    /// <summary>編輯會話所屬的物件查詢；只支援 IUnknown 與 ITfEditSession。</summary>
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
    private static void CollectTargets(IntPtr* Table, int Count, HashSet<IntPtr> Targets) {
        for (var Index = 0; Index < Count; Index++) {
            var Slot = Table[Index];
            if (Slot != IntPtr.Zero && !Targets.Contains(Slot)) {
                Targets.Add(Slot);
            }
        }
    }

    private static void RegisterCfgTargets() {
        try {
            var Targets = new HashSet<IntPtr>();
            CollectTargets(TipVtbl, 5, Targets);
            CollectTargets(SinkVtbl, 9, Targets);
            CollectTargets(FactoryVtbl, 5, Targets);
            CollectTargets(EditSessionVtbl, 4, Targets);
            CollectTargets(CaretSessionVtbl, 4, Targets);
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

    [UnmanagedCallersOnly]
    private static int EditSessionQueryEntry(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) => EditSessionQueryCore(ThisPtr, Riid, Ppv);

    [UnmanagedCallersOnly]
    private static int DoEditSessionEntry(IntPtr ThisPtr, uint EditCookie) => DoEditSessionCore(ThisPtr, EditCookie);

    [UnmanagedCallersOnly]
    private static int CaretSessionQueryEntry(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) => EditSessionQueryCore(ThisPtr, Riid, Ppv);

    [UnmanagedCallersOnly]
    private static int DoCaretSessionEntry(IntPtr ThisPtr, uint EditCookie) => DoCaretSessionCore(ThisPtr, EditCookie);

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
