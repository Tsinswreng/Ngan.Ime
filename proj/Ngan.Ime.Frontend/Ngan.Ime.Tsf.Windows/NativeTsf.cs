namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using Ngan.Ime.Tsf.Ipc;

/// <summary>
/// NativeAOT 版的 TSF 薄外殼。整個 DLL 不帶 CLR，只做四件事：
/// 1. 提供 DllGetClassObject 與 IClassFactory，讓 Windows 建立文字服務；
/// 2. 實作 ITfTextInputProcessor、ITfKeyEventSink、ITfEditSession、ITfCompositionSink 的 vtable
///    （手寫；不以 `[ComImport]` 走內建 COM）；
/// 3. 把每一顆按鍵送給服務端，吃不吃照引擎的回答；
/// 4. 上屏照 Weasel：在真正的組字範圍裡寫字。
/// </summary>
/// <remarks>
/// 對外呼叫的介面分兩類，別搞混：
/// + *已被呼叫端*（實作 ITfTextInputProcessor、ITfKeyEventSink、ITfEditSession、
///   ITfCompositionSink、IClassFactory）：仍舊手寫 vtable，表格見下。
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
/// + `ITfCompositionSink`：3 OnCompositionTerminated（`msctf.h:7071-7093`）。
/// + `ITfKeystrokeMgr`：3 AdviseKeyEventSink、4 UnadviseKeyEventSink。
/// + `ITfTextInputProcessor`：3 Activate、4 Deactivate。
/// + `ITfKeyEventSink`：3 OnSetFocus、4 OnTestKeyDown、5 OnTestKeyUp、6 OnKeyDown、
///   7 OnKeyUp、8 OnPreservedKey。
///
/// *上屏照 Weasel 的組字生命週期*（2026-11-15 重寫）：一顆會上屏的鍵，依序排三個
/// **非同步可寫**編輯會話——開始組字、把最終文字寫進組字範圍、結束組字
/// （Weasel `EditSession.cpp:26-30` 的提交段，各步的會話在 `Composition.cpp:73`、`:365`、`:121`）。
/// 三個會話都走 `TF_ES_ASYNCDONTCARE | TF_ES_READWRITE`，**沒有任何同步會話**：
/// Weasel 整份原始碼一次都沒用 `TF_ES_SYNC`，而我們的會話是從按鍵回調裏發出的，
/// 同步等待等於在別人的 UI 執行緒上等一個只有它自己才能給的鎖。
///
/// *為何一定要有真的組字*：Chromium 的 EditContext 只把「替換組字範圍」當正常上屏；
/// 直接往選取範圍塞字，它會用自己的路徑再插一次，使用者看到的就是同一個字重複。
/// 微軟拼音只有直接上屏的符號（，。數字）會重複，正是因為那些也沒有組字。
///
/// *與 Weasel 的唯一差別*：Weasel 的組字從第一個鍵活到整段輸入結束（inline preedit 要它），
/// 我們沒有 inline preedit，所以每次上屏都「建 → 寫 → 結束」，組字只活三個會話。
/// 因為活得這麼短，Weasel 那個「先塞零寬空格給 CUAS 量 GetTextExt」的手術（`Composition.cpp:45-56`）
/// 我們不做——那段時間裏我們一次 GetTextExt 都不會叫。
///
/// *組字被應用結束*：`ITfCompositionSink::OnCompositionTerminated` 一定接（Weasel 也接）。
/// 不接的話，Chromium 這類會主動終止組字的應用會讓我們握着一個已死的 `ITfComposition`。
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
    /// <summary>ITfInsertAtSelection：取插入點範圍用。</summary>
    private static readonly Guid IidInsertAtSelection = new("55CE16BA-3014-41C1-9CEB-FADE1446AC6C");
    /// <summary>ITfContextComposition：開組字用。</summary>
    private static readonly Guid IidContextComposition = new("D40C8AAE-AC92-4FC7-9A11-0EE0E23AA39B");
    /// <summary>ITfCompositionSink：組字被應用結束時的通知介面（msctf.h:7057）。</summary>
    private static readonly Guid IidCompositionSink = new("A781718C-579A-4B15-A280-32B8577ACC5E");

    private const int SOk = 0;
    private const int SFalse = 1;
    private const int ENoInterface = unchecked((int)0x80004002);
    private const int EFail = unchecked((int)0x80004005);
    private const int EInvalidArg = unchecked((int)0x80070057);
    private const int ENoAggregation = unchecked((int)0x80040110);


    /// <summary>
    /// 編輯會話旗標；數值一律照 `msctf.h:2278-2286`，不憑記憶（先前把 ASYNCDONTCARE 寫成 0x8，那其實是 ASYNC）。
    /// </summary>
    /// <remarks>
    /// `TF_ES_ASYNCDONTCARE` 就是 0，Weasel 每一次 `RequestEditSession` 用的都是它配 READ 或 READWRITE。
    /// 我們所有會話也都用非同步：這段程式跑在應用行程的 UI 執行緒上，
    /// 同步會話（`TF_ES_SYNC`）在應用正持有文件鎖時只能等，而等的那個人就是被呼叫的自己。
    /// </remarks>
    private const uint TfEsRead = 0x2;
    private const uint TfEsReadWrite = 0x6;
    /// <summary>`TF_ES_ASYNCDONTCARE`（msctf.h:2278）：值就是 0，不是 0x8。</summary>
    private const uint TfEsAsyncDontCare = 0x0;
    /// <summary>`TF_ES_ASYNC`（msctf.h:2286）：0x8。本殼不用它；留著是為了別再把兩者搞混。</summary>
    private const uint TfEsAsync = 0x8;
    /// <summary>`TF_ANCHOR_START`（msctf.h）：把組字範圍收成起點。</summary>
    private const uint TfAnchorStart = 0;
    /// <summary>`TF_ANCHOR_END`：把範圍收成終點，游標留在文字之後。</summary>
    private const uint TfAnchorEnd = 1;
    /// <summary>`TF_S_ASYNC`（msctf.h:581）：非同步會話已排入佇列。</summary>
    private const int TfSAsync = 0x00040300;
    /// <summary>TF_IAS_QUERYONLY：只取插入點範圍，不插入文字（msctf.h:6545）。</summary>
    private const uint TfIasQueryOnly = 0x2;
    /// <summary>TF_ST_CORRECTION：把這次寫入標成「修正」（msctf.h:5817）；Weasel 塞零寬空格時用它。</summary>
    private const uint TfStCorrection = 1;

    /// <summary>
    /// 零寬空格（U+200B）；組字範圍在組字期間用它佔位，讓範圍非空。
    /// </summary>
    /// <remarks>
    /// 照 Weasel `Composition.cpp:45-56`：沒有 inline preedit 時，組字範圍裏先放一個零寬空格，
    /// 再把選取收在起點。2026-11-15 23:20 的實測說明了為何非這樣不可——
    /// 我們的「空組字 + 立刻寫字 + 立刻結束」在記事本上正常，在 VSCode（Chromium 的 EditContext）
    /// 上卻照樣重複上屏，代表那個空的、只活一毫秒的組字並沒有被當成一次組字。
    /// </remarks>
    private const string ZeroWidthSpace = "\u200B";

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
    /// <summary>ITfCompositionSink 的 vtable；組字被應用結束時 TSF 從這裡叫回來。</summary>
    private static readonly IntPtr* CompositionSinkVtbl;

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

        // ITfCompositionSink：IUnknown 三個 + OnCompositionTerminated（msctf.h:7071-7093）。
        // 交給 ITfContextComposition::StartComposition 當第三個參數——
        // Weasel 傳的是自己（WeaselTSF），我們傳這個 view。
        CompositionSinkVtbl = (IntPtr*)NativeMemory.Alloc(4, (nuint)sizeof(IntPtr));
        CompositionSinkVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&CompositionSinkQueryEntry;
        CompositionSinkVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&AddRefEntry;
        CompositionSinkVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&ReleaseEntry;
        CompositionSinkVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, uint, IntPtr, int>)&OnCompositionTerminatedEntry;

        // 先算出模組標籤：之後每一行日誌都帶著它，才分得出是哪一版的外殼寫的。
        SelfTag = ComputeSelfTag();
        RegisterCfgTargets();
        AddVectoredExceptionHandler(1, (IntPtr)(delegate* unmanaged<IntPtr, int>)&VectoredExceptionHandler);
        // 一律落盤：行程一載入這支 DLL 就留一行，連「TSF 有沒有來要 class factory」都還沒發生。
        // 排查「某個應用切不過來」時，這一行有沒有出現就是第一個分界。
        LogWarn("shell loaded pid=" + Environment.ProcessId + " path=" + (GetOwnModulePath() is { Length: > 0 } P ? P : "?"));
    }

    /// <summary>上屏的三個步驟，一一對應 Weasel 的三個編輯會話。</summary>
    private enum ECompositionStep {
        /// <summary>開始組字（Weasel `CStartCompositionEditSession`）。</summary>
        Start = 0,
        /// <summary>把最終文字寫進組字範圍（Weasel `CInsertTextEditSession`）。</summary>
        Insert = 1,
        /// <summary>結束組字（Weasel `CEndCompositionEditSession`）。</summary>
        End = 2,
    }

    /// <summary>佇列裏的一步組字操作；一步只做一件事，對應一次非同步編輯會話。</summary>
    private struct CompositionJob {
        /// <summary>這一步要做什麼。</summary>
        public ECompositionStep Step;
        /// <summary>Insert 用：要上屏的文字。</summary>
        public string Text;
        /// <summary>End 用：結束前要不要把組字範圍清空（Weasel 上屏時傳 false，保留剛寫進去的字）。</summary>
        public bool Clear;
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
        /// <summary>
        /// 目前的組字（ITfComposition，自己持有一份參考）；0 表示沒有組字。
        /// </summary>
        /// <remarks>
        /// 只由三個地方改：開始組字（寫入）、結束組字與 <see cref="OnCompositionTerminatedCore"/>
        /// （兩者都用 Interlocked 換出再釋放，避免重複釋放同一個指標）。
        /// </remarks>
        public IntPtr Composition;
        /// <summary>ITfCompositionSink 的介面指標；交給 StartComposition 當第三個參數。</summary>
        public IntPtr CompositionSinkView;
        /// <summary>ITfCompositionSink 的 GCHandle。</summary>
        public IntPtr CompositionSinkHandle;
        /// <summary>
        /// 待執行的組字步驟佇列；一個步驟對應一次非同步編輯會話。
        /// </summary>
        /// <remarks>
        /// *為何要佇列*：上屏不是一個動作，而是「開始組字→寫入→結束」三個會話
        /// （照 Weasel）。會話是非同步的，同一個 edit session 物件只能表達「這次要做哪一步」，
        /// 所以步驟必須排隊；少了佇列，第二顆很快的上屏鍵會把第一步覆蓋掉，字就丟了。
        /// </remarks>
        public readonly List<CompositionJob> CompositionJobs = new();
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
            State.CompositionSinkView = MakeView(CompositionSinkVtbl, State, out var CompositionSinkHandle);
            State.CompositionSinkHandle = CompositionSinkHandle;

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
            // 只有在真的掛上按鍵接收器之後才需要看門狗：它要回答的是「這個行程卡死時停在誰身上」。
            EnsureWatchdog();
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
            AbandonComposition(State);
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
            LogWarn("OnSetFocus fg=" + IsForeground + " pid=" + Environment.ProcessId);
            var State = GetState(ThisPtr);
            if (State is null) {
                return SOk;
            }
            if (IsForeground == 0) {
                // 失去焦點：按鍵記錄與插入點都不再有意義，留著只會被下一個控件繼承。
                State.Policy.Reset();
                CancelCaretMeasure(State);
                State.CaretValid = false;
                AbandonComposition(State);
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
    private static void HandleKeyEvent(TsfState State, IntPtr Context, int VirtualKey, bool IsUp, bool IsTest, IntPtr LParam, int* IsEaten) {
        *IsEaten = 0;
        // 高解析度時鐘：這段程式跑在別人的 UI 執行緒上，逐鍵耗時（尤其管道往返）要能直接讀，
        // 不能用 15.6ms 粒度的 TickCount。
        var StartedTs = Stopwatch.GetTimestamp();
        var Mods = GetModifiers();
        // lParam 第 16 到 23 位是掃描碼、第 24 位是擴展鍵位：
        // VK_SHIFT 對左右是同一個值，只有靠掃描碼才分得出 El 左 Shift 與右 Shift。
        var Scan = (int)((LParam.ToInt64() >> 16) & 0xFF);
        var Ext = (int)((LParam.ToInt64() >> 24) & 1);
        TsfReply? Reply = null;
        // 記下「目前正在處理哪一顆鍵」，只存在記憶體裏：看門狗在卡死時會把它寫出來，
        // 於是「卡在我們自己的回調裏」與「我們早就交還了、應用自己卡」可以直接分辨，
        // 又不必為每一顆鍵多寫一行檔（那是每顆鍵都要付的 IO 成本）。
        Interlocked.Exchange(ref CurrentOpVk, VirtualKey + 1);
        Interlocked.Exchange(ref CurrentOpFlags, (IsUp ? 1 : 0) | (IsTest ? 2 : 0));
        Interlocked.Exchange(ref CurrentOpStart, StartedTs);
        try {
            // step 1: 結算反方向尚未領走的徵詢結論（應用可能只送其中一半的事件）。
            State.Policy.SettleOpposite(IsUp);
            // 照 Weasel：所有認得的鍵都送引擎，吃不吃由引擎的回答決定，
            // 不為修飾鍵另立例外（Weasel `KeyEventSink.cpp:11` 就是這樣做的）。
            // 先前那條「修飾鍵一律放行」讓 Alt 永遠到不了引擎，引擎因此沒有機會處理它。
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
                Reply = Cached;
                if (Cached is not null) {
                    // 上屏只在「執行」這條路徑做一次。
                    // 先前徵詢那條也上屏，同一份回應因此被插進文件兩次（同字重複上屏）。
                    ScheduleCompositionWork(State, Context, Cached);
                    MaybeMeasureCaret(State, Context, Cached);
                    ClearReply(State, IsUp);
                }
                return;
            }
            ResolveKeyAnchor(State, out var AnchorX, out var AnchorY);
            Reply = QueryKey(VirtualKey, Mods, AnchorX, AnchorY, IsUp, Scan, Ext);
            var Eat = State.Policy.DecideEat(VirtualKey, IsUp, Reply is { Eaten: true });
            *IsEaten = Eat ? 1 : 0;
            if (Eat) {
                // 只有吃掉才需要把結果留給 OnKeyXxx；沒吃掉時應用的按鍵照常走，不必再問一次。
                SetReply(State, IsUp, Reply);
            }
            else {
                ClearReply(State, IsUp);
            }
            if (!IsTest && Reply is not null) {
                // 兩條上屏路徑只留這一條：
                // + 應用送「徵詢 → 執行」時，上屏發生在執行那條（上面 TryTakeKey 的分支）；
                // + 應用只送「執行」時，上屏發生在這裏。
                ScheduleCompositionWork(State, Context, Reply);
                MaybeMeasureCaret(State, Context, Reply);
            }
            if (IsTest && Eat) {
                State.Policy.SetTest(IsUp, VirtualKey, true);
            }
        }
        finally {
            Interlocked.Exchange(ref CurrentOpVk, 0);
            Interlocked.Exchange(ref LastCallbackTs, Stopwatch.GetTimestamp());
            // 每一條返回路徑都留一行；修飾鍵提早放行那條也記，否則看不到 Alt 的 down／up。
            // 附上「這顆鍵在我們這裏花了多少毫秒」，用來分辨「我們拖慢了按鍵」與「應用自己慢」。
            DiagKey(State, VirtualKey, Mods, IsUp, IsTest, LParam, *IsEaten, Reply, StartedTs);
        }
    }

    /// <summary>
    /// 只有在「有東西要顯示」時才量插入點。
    /// </summary>
    /// <param name="State">文字服務狀態。</param>
    /// <param name="Context">按鍵所屬的 context。</param>
    /// <param name="Reply">引擎這次的回答。</param>
    /// <remarks>
    /// *為何不再每顆鍵都量*：`ITfContextView::GetTextExt` 在 Chromium 是同步問 renderer 的，
    /// 而我們是在應用的 UI 執行緒上叫它。退格、方向鍵、修飾鍵這些根本不會顯示候選窗的鍵，
    /// 先前也各量一次，等於把 UI 執行緒借給應用去等 renderer。現在只在這顆鍵的回應
    /// 真的帶了候選或 preedit 時才量；位置仍然是一顆鍵的延遲（本來就是），但風險面積小得多。
    /// </remarks>
    private static void MaybeMeasureCaret(TsfState State, IntPtr Context, TsfReply Reply) {
        if (Reply.Candidates.Count == 0 && string.IsNullOrEmpty(Reply.Preedit)) {
            return;
        }
        BeginCaretMeasure(State, Context);
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

    private static int OnTestKeyDownCore(IntPtr ThisPtr, IntPtr Context, IntPtr WParam, IntPtr LParam, int* IsEaten) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                *IsEaten = 0;
                return EFail;
            }
            HandleKeyEvent(State, Context, (int)WParam.ToInt64(), false, true, LParam, IsEaten);
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
            HandleKeyEvent(State, Context, (int)WParam.ToInt64(), false, false, LParam, IsEaten);
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
            HandleKeyEvent(State, Context, (int)WParam.ToInt64(), true, true, LParam, IsEaten);
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
            HandleKeyEvent(State, Context, (int)WParam.ToInt64(), true, false, LParam, IsEaten);
            return SOk;
        }
        catch (Exception Exception) {
            LogError("OnKeyUp EX " + Exception);
            *IsEaten = 0;
            return EFail;
        }
    }

    /// <summary>
    /// 依引擎這一輪的回答，安排組字要做的動作：開組字、寫入上屏文字、結束組字。
    /// </summary>
    /// <param name="State">文字服務狀態。</param>
    /// <param name="Context">按鍵所屬的 context。</param>
    /// <param name="Reply">引擎的回答。</param>
    /// <remarks>
    /// *這是照 Weasel `WeaselTSF::DoEditSession`（`EditSession.cpp:18-45`）搬過來的判斷*：
    /// + 有上屏文字：若還沒組字就先開組字 → 寫入最終文字 → 結束組字（保留文字）；
    /// + 沒有上屏但有 preedit：若還沒組字就開組字，讓它在打字期間一直存在；
    /// + 兩者都沒有而我們還握着組字：結束它並清空。
    ///
    /// *與舊版最關鍵的差別*：組字不再是「上屏那一刻才建、一毫秒內拆掉」，而是從組字開始
    /// 活到上屏或取消。Chromium 的 EditContext 只認得這種有生命週期的組字；
    /// 空的、瞬生瞬滅的組字它會當成不存在，於是文字又被它自己插一次（VSCode 的重複上屏）。
    ///
    /// *為何不在這裏等結果*：非同步會話的成敗由各自的 `DoEditSession` 寫進日誌，
    /// 按鍵路徑只負責排隊。要嘛相信日誌，要嘛就會為了知道結果去阻塞別人的 UI 執行緒。
    /// </remarks>
    private static void ScheduleCompositionWork(TsfState State, IntPtr Context, TsfReply Reply) {
        var HasCommit = Reply.Commits.Count > 0;
        var HasPreedit = !string.IsNullOrEmpty(Reply.Preedit);
        var HasComposition = Volatile.Read(ref State.Composition) != IntPtr.Zero;
        if (!HasCommit && !HasPreedit) {
            // 引擎說沒有組字了（上屏完畢、取消、或本來就沒在組字）。
            if (HasComposition) {
                EnqueueCompositionJob(State, ECompositionStep.End, "", true);
            }
            return;
        }
        State.Context = Context;
        if (HasCommit) {
            var Text = string.Concat(Reply.Commits);
            LogWarn("Commit begin text=[" + Text + "] len=" + Text.Length
                + " ctx=" + Context.ToString("X")
                + " comp=" + (HasComposition ? "有" : "無"));
            if (!HasComposition) {
                EnqueueCompositionJob(State, ECompositionStep.Start, "", false);
            }
            EnqueueCompositionJob(State, ECompositionStep.Insert, Text, false);
            EnqueueCompositionJob(State, ECompositionStep.End, "", false);
            if (HasPreedit) {
                // 自動上屏之後引擎可能立刻開始下一段組字（Weasel 同一個分支）。
                EnqueueCompositionJob(State, ECompositionStep.Start, "", false);
            }
            return;
        }
        // 只有 preedit：組字開始。Weasel 也是在這裏讓組字活起來，而不是等到上屏才建。
        if (!HasComposition) {
            LogWarn("組字 開始（引擎組字中） ctx=" + Context.ToString("X"));
            EnqueueCompositionJob(State, ECompositionStep.Start, "", false);
        }
    }

    /// <summary>組字佇列的上限；到頂就代表「會話排了卻沒被執行」，記錄下來，不讓它無界成長。</summary>
    private const int MaxCompositionJobs = 32;

    /// <summary>把一步組字操作排進佇列，並替它排一次非同步編輯會話。</summary>
    /// <param name="State">文字服務狀態。</param>
    /// <param name="Step">要做哪一步。</param>
    /// <param name="Text">Insert 用的上屏文字。</param>
    /// <param name="Clear">End 用：結束前是否清空組字範圍。</param>
    private static void EnqueueCompositionJob(TsfState State, ECompositionStep Step, string Text, bool Clear) {
        var Job = new CompositionJob { Step = Step, Text = Text, Clear = Clear };
        lock (State.CompositionJobs) {
            if (State.CompositionJobs.Count >= MaxCompositionJobs) {
                LogError("組字佇列已滿（" + State.CompositionJobs.Count + "），丟棄 step=" + Step + " text=[" + Text + "]");
                return;
            }
            State.CompositionJobs.Add(Job);
        }
        var Hr = RequestCompositionSession(State, State.Context, Step);
        if (Hr < 0) {
            // 排不進去就收回剛加的那一步：留著會讓下一個會話拿到錯的步驟，整段上屏都會亂。
            lock (State.CompositionJobs) {
                if (State.CompositionJobs.Count > 0) {
                    State.CompositionJobs.RemoveAt(State.CompositionJobs.Count - 1);
                }
            }
            LogError("組字步驟排不進會話 step=" + Step + " hr=0x" + Hr.ToString("X8") + " text=[" + Text + "]");
        }
    }

    /// <summary>
    /// 替佇列裏的一步排一次非同步、可寫的編輯會話（與 Weasel 的旗標完全相同）。
    /// </summary>
    /// <param name="State">文字服務狀態。</param>
    /// <param name="Context">按鍵所屬的 context。</param>
    /// <param name="Step">這一步的名稱，只用於日誌。</param>
    /// <returns>排入結果；小於 0 表示排不進去。</returns>
    private static int RequestCompositionSession(TsfState State, IntPtr Context, ECompositionStep Step) {
        var ContextGen = ToContextGen(Context, "排入編輯會話");
        if (ContextGen is null) {
            return EFail;
        }
        var Flags = TfEsAsyncDontCare | TfEsReadWrite;
        var SessionResult = 0;
        var Hr = ContextGen.RequestEditSession(State.ClientId, State.EditSessionView, Flags, out SessionResult);
        LogWarn("RequestEditSession step=" + Step
            + " flags=0x" + Flags.ToString("X")
            + " hr=0x" + Hr.ToString("X8")
            + " phr=0x" + SessionResult.ToString("X8")
            + (SessionResult == TfSAsync ? "（TF_S_ASYNC）" : ""));
        if (Hr < 0) {
            return Hr;
        }
        return SessionResult < 0 ? SessionResult : SOk;
    }

    /// <summary>
    /// 換焦點或停用時放棄還沒結束的組字（照 Weasel `OnSetFocus(false)` → `_AbortComposition`）。
    /// </summary>
    /// <param name="State">文字服務狀態。</param>
    /// <remarks>
    /// 只排一個「結束並清空」的會話，不直接呼叫 COM：這條路徑同樣在 TSF 的回調裏，
    /// 直接叫 `EndComposition` 需要寫入 cookie，而我們手上沒有。
    /// 會話排不進去時 `EnqueueCompositionJob` 會記 Error，組字就留在應用那邊——
    /// 那會在日誌裏留下痕跡，不會變成一個沒有人知道的空組字。
    /// </remarks>
    private static void AbandonComposition(TsfState State) {
        if (Volatile.Read(ref State.Composition) == IntPtr.Zero) {
            return;
        }
        LogWarn("離開焦點／停用：排入結束組字（清空） comp=" + State.Composition.ToString("X"));
        EnqueueCompositionJob(State, ECompositionStep.End, "", true);
    }

    private static int OnPreservedKeyCore(IntPtr ThisPtr, IntPtr Context, Guid* PreservedKey, int* IsEaten) {
        LogWarn("OnPreservedKey key=" + *PreservedKey + " pid=" + Environment.ProcessId);
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

    /// <summary>把 context 指標換成托管介面；失敗時把原因寫進日誌並回 null。</summary>
    /// <param name="Context">ITfContext 的原始指標。</param>
    /// <param name="Where">呼叫端名稱，只用於日誌。</param>
    /// <returns>托管介面；失敗為 null。</returns>
    private static ITfContextGen? ToContextGen(IntPtr Context, string Where) {
        if (Context == IntPtr.Zero) {
            LogError(Where + ": context 是 0");
            return null;
        }
        var Gen = ComInterfaceMarshaller<ITfContextGen>.ConvertToManaged((void*)Context);
        if (Gen is null) {
            LogError(Where + ": context 轉換失敗 ctx=" + Context.ToString("X"));
        }
        return Gen;
    }

    /// <summary>
    /// 執行佇列裏的一步組字操作。
    /// </summary>
    /// <remarks>
    /// 一個 edit session 物件、一個佇列、三種步驟：TSF 依排入順序叫回來，
    /// 每次取走最前面那一步。這正是 Weasel 用三個不同的 `CEditSession` 子類別做的事，
    /// 差別只在我們少了三次 `new`。
    /// </remarks>
    private static int DoEditSessionCore(IntPtr ThisPtr, uint EditCookie) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                return EFail;
            }
            CompositionJob Job;
            int Remaining;
            lock (State.CompositionJobs) {
                if (State.CompositionJobs.Count == 0) {
                    // 遲到的會話：排入時被收回、或應用重放了請求。記下來，不要再做任何事。
                    LogWarn("DoEditSession 沒有待辦步驟（多餘或遲到的會話） ec=0x" + EditCookie.ToString("X"));
                    return SOk;
                }
                Job = State.CompositionJobs[0];
                State.CompositionJobs.RemoveAt(0);
                Remaining = State.CompositionJobs.Count;
            }
            LogWarn("DoEditSession step=" + Job.Step
                + " ec=0x" + EditCookie.ToString("X")
                + " 剩餘=" + Remaining
                + (Job.Text.Length > 0 ? " text=[" + Job.Text + "]" : ""));
            int Hr;
            switch (Job.Step) {
                case ECompositionStep.Start:
                    Hr = StartCompositionCore(State, EditCookie);
                    break;
                case ECompositionStep.Insert:
                    Hr = InsertCommitTextCore(State, EditCookie, Job.Text);
                    break;
                case ECompositionStep.End:
                    Hr = EndCompositionCore(State, EditCookie, Job.Clear);
                    break;
                default:
                    Hr = SOk;
                    break;
            }
            LogWarn("DoEditSession done step=" + Job.Step + " hr=0x" + Hr.ToString("X8"));
            return Hr;
        }
        catch (Exception Exception) {
            LogError("DoEditSession EX " + Exception);
            return EFail;
        }
    }

    /// <summary>
    /// 開始一次組字（照 Weasel `CStartCompositionEditSession::DoEditSession`，`Composition.cpp:26-70`）。
    /// </summary>
    /// <param name="State">文字服務狀態。</param>
    /// <param name="EditCookie">這次編輯會話的 cookie。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>
    /// 步驟與 Weasel 一字不差：
    /// `ITfInsertAtSelection::InsertTextAtSelection(TF_IAS_QUERYONLY)` 取插入點範圍 →
    /// `ITfContextComposition::StartComposition(ec, 範圍, sink, &組字)` →
    /// 在範圍裏寫一個**零寬空格**（`SetText(TF_ST_CORRECTION)`）→
    /// `Collapse(TF_ANCHOR_START)` → `SetSelection(ec, 1, &選取)`。
    /// sink 是我們自己實作的 `ITfCompositionSink`（Weasel 傳的是 WeaselTSF 自己）。
    ///
    /// *為何一定要塞那個零寬空格*：2026-11-15 23:20 的實測——只建空組字、立刻寫字、立刻結束，
    /// 記事本正常、VSCode 照樣重複上屏。空的組字範圍在 Chromium 眼裏等於不存在；
    /// 這也是 Weasel 在沒有 inline preedit 時仍然塞零寬空格的原因。
    /// </remarks>
    private static int StartCompositionCore(TsfState State, uint EditCookie) {
        if (Volatile.Read(ref State.Composition) != IntPtr.Zero) {
            LogWarn("組字已存在，略過開始 comp=" + State.Composition.ToString("X"));
            return SOk;
        }
        var InsertIid = IidInsertAtSelection;
        var Qhr = Marshal.QueryInterface(State.Context, in InsertIid, out var InsertPtr);
        Log("組字 QI(ITfInsertAtSelection) hr=0x" + Qhr.ToString("X8") + " ptr=" + InsertPtr.ToString("X"));
        if (Qhr < 0 || InsertPtr == IntPtr.Zero) {
            return Qhr < 0 ? Qhr : EFail;
        }
        try {
            var Insert = ComInterfaceMarshaller<ITfInsertAtSelectionGen>.ConvertToManaged((void*)InsertPtr);
            if (Insert is null) {
                LogError("組字: ITfInsertAtSelection 轉換失敗");
                return EFail;
            }
            var RangeHr = Insert.InsertTextAtSelection(EditCookie, TfIasQueryOnly, IntPtr.Zero, 0, out var InsertRange);
            LogWarn("組字 InsertTextAtSelection hr=0x" + RangeHr.ToString("X8") + " range=" + InsertRange.ToString("X"));
            if (RangeHr < 0 || InsertRange == IntPtr.Zero) {
                return RangeHr < 0 ? RangeHr : EFail;
            }
            try {
                var CompIid = IidContextComposition;
                var Chr = Marshal.QueryInterface(State.Context, in CompIid, out var CompPtr);
                Log("組字 QI(ITfContextComposition) hr=0x" + Chr.ToString("X8") + " ptr=" + CompPtr.ToString("X"));
                if (Chr < 0 || CompPtr == IntPtr.Zero) {
                    return Chr < 0 ? Chr : EFail;
                }
                try {
                    var Comp = ComInterfaceMarshaller<ITfContextCompositionGen>.ConvertToManaged((void*)CompPtr);
                    if (Comp is null) {
                        LogError("組字: ITfContextComposition 轉換失敗");
                        return EFail;
                    }
                    var StartHr = Comp.StartComposition(EditCookie, InsertRange, State.CompositionSinkView, out var CompositionPtr);
                    LogWarn("組字 StartComposition hr=0x" + StartHr.ToString("X8")
                        + " comp=" + CompositionPtr.ToString("X")
                        + " sink=" + State.CompositionSinkView.ToString("X"));
                    if (StartHr < 0 || CompositionPtr == IntPtr.Zero) {
                        return StartHr < 0 ? StartHr : EFail;
                    }
                    var Old = Interlocked.Exchange(ref State.Composition, CompositionPtr);
                    if (Old != IntPtr.Zero) {
                        Marshal.Release(Old);
                    }
                    LogWarn("組字 已建立 comp=" + CompositionPtr.ToString("X"));
                    // 照 Weasel `Composition.cpp:45-66`：先塞零寬空格讓範圍非空，再把選取收在起點。
                    var Range = ComInterfaceMarshaller<ITfRangeGen>.ConvertToManaged((void*)InsertRange);
                    if (Range is not null) {
                        int ZwspHr;
                        fixed (char* Zwsp = ZeroWidthSpace) {
                            ZwspHr = Range.SetText(EditCookie, TfStCorrection, (IntPtr)Zwsp, ZeroWidthSpace.Length);
                        }
                        LogWarn("組字 塞零寬空格 hr=0x" + ZwspHr.ToString("X8"));
                        var CollapseHr = Range.Collapse(EditCookie, TfAnchorStart);
                        LogWarn("組字 Collapse(START) hr=0x" + CollapseHr.ToString("X8"));
                        var Selection = new TfSelection { Range = InsertRange, ActiveEnd = 0, Interim = 0 };
                        var ContextGen = ToContextGen(State.Context, "組字 SetSelection");
                        if (ContextGen is not null) {
                            var SelectHr = ContextGen.SetSelection(EditCookie, 1, (IntPtr)(&Selection));
                            LogWarn("組字 SetSelection hr=0x" + SelectHr.ToString("X8"));
                        }
                    }
                    else {
                        LogError("組字: ITfRange 轉換失敗，零寬空格沒塞（應用可能不認這次組字）");
                    }
                    return SOk;
                }
                finally {
                    Marshal.Release(CompPtr);
                }
            }
            finally {
                Marshal.Release(InsertRange);
            }
        }
        finally {
            Marshal.Release(InsertPtr);
        }
    }

    /// <summary>
    /// 把上屏文字寫進組字範圍（照 Weasel `CInsertTextEditSession::DoEditSession`，`Composition.cpp:334-360`）。
    /// </summary>
    /// <param name="State">文字服務狀態。</param>
    /// <param name="EditCookie">這次編輯會話的 cookie。</param>
    /// <param name="Text">要上屏的文字。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>
    /// Weasel 的順序照抄：`GetRange` → `SetText(ec, 0, 文字)` → `Collapse(TF_ANCHOR_END)` →
    /// `SetSelection(ec, 1, &選取)`。少了 `SetSelection`，Chromium 會以為游標還在原處。
    ///
    /// *沒有組字時走退回路徑*：開始組字失敗（應用不支援）就退回
    /// 「取選取範圍 → SetText → Collapse → SetSelection」，至少讓字進得去。
    /// 日誌會明說走的是哪一條，不讓「照 Weasel」變成一句空話。
    /// </remarks>
    private static int InsertCommitTextCore(TsfState State, uint EditCookie, string Text) {
        if (Text.Length == 0) {
            LogWarn("上屏步驟沒有文字，略過");
            return SOk;
        }
        var ContextGen = ToContextGen(State.Context, "上屏");
        if (ContextGen is null) {
            return EFail;
        }
        var CompPtr = Volatile.Read(ref State.Composition);
        if (CompPtr == IntPtr.Zero) {
            LogWarn("沒有組字（開始步驟失敗或應用已終止），退回選取範圍上屏 text=[" + Text + "]");
            return InsertBySelectionCore(ContextGen, EditCookie, Text);
        }
        var Comp = ComInterfaceMarshaller<ITfCompositionGen>.ConvertToManaged((void*)CompPtr);
        if (Comp is null) {
            LogError("上屏: ITfComposition 轉換失敗 comp=" + CompPtr.ToString("X"));
            return InsertBySelectionCore(ContextGen, EditCookie, Text);
        }
        var GetHr = Comp.GetRange(out var RangePtr);
        Log("上屏 Composition.GetRange hr=0x" + GetHr.ToString("X8") + " range=" + RangePtr.ToString("X"));
        if (GetHr < 0 || RangePtr == IntPtr.Zero) {
            return GetHr < 0 ? GetHr : EFail;
        }
        try {
            var Range = ComInterfaceMarshaller<ITfRangeGen>.ConvertToManaged((void*)RangePtr);
            if (Range is null) {
                LogError("上屏: ITfRange 轉換失敗");
                return EFail;
            }
            int Hr;
            fixed (char* TextPtr = Text) {
                Hr = Range.SetText(EditCookie, 0, (IntPtr)TextPtr, Text.Length);
            }
            LogWarn("上屏 組字 SetText hr=0x" + Hr.ToString("X8") + " len=" + Text.Length + " text=[" + Text + "]");
            if (Hr < 0) {
                return Hr;
            }
            var CollapseHr = Range.Collapse(EditCookie, TfAnchorEnd);
            Log("上屏 組字 Collapse hr=0x" + CollapseHr.ToString("X8"));
            var Selection = new TfSelection { Range = RangePtr, ActiveEnd = 0, Interim = 0 };
            var SelectHr = ContextGen.SetSelection(EditCookie, 1, (IntPtr)(&Selection));
            if (SelectHr < 0) {
                LogError("上屏 組字 SetSelection hr=0x" + SelectHr.ToString("X8"));
            }
            else {
                Log("上屏 組字 SetSelection hr=0x" + SelectHr.ToString("X8"));
            }
            return Hr;
        }
        finally {
            Marshal.Release(RangePtr);
        }
    }

    /// <summary>
    /// 退回路徑：取目前的選取範圍、把文字寫進去、收成插入點、設定選取。
    /// </summary>
    /// <param name="ContextGen">目前的 context。</param>
    /// <param name="EditCookie">這次編輯會話的 cookie。</param>
    /// <param name="Text">要上屏的文字。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>
    /// 這條路 Chromium 會再插一次（見本檔類註釋），所以只要日誌裏出現「退回」兩個字，
    /// 就代表那個應用上屏會重複——不是猜測，是這條路的已知後果。
    /// </remarks>
    private static int InsertBySelectionCore(ITfContextGen ContextGen, uint EditCookie, string Text) {
        var Selection = default(TfSelection);
        var Hr = ContextGen.GetSelection(EditCookie, 0, 1, out Selection, out var Fetched);
        LogWarn("上屏(退回) GetSelection hr=0x" + Hr.ToString("X8") + " fetched=" + Fetched + " text=[" + Text + "]");
        if (Hr < 0) {
            return Hr;
        }
        if (Fetched != 1 || Selection.Range == IntPtr.Zero) {
            LogError("上屏(退回): 取不到選取範圍");
            return EFail;
        }
        try {
            var Range = ComInterfaceMarshaller<ITfRangeGen>.ConvertToManaged((void*)Selection.Range);
            if (Range is null) {
                LogError("上屏(退回): ITfRange 轉換失敗");
                return EFail;
            }
            int SetHr;
            fixed (char* TextPtr = Text) {
                SetHr = Range.SetText(EditCookie, 0, (IntPtr)TextPtr, Text.Length);
            }
            LogWarn("上屏(退回) SetText hr=0x" + SetHr.ToString("X8") + " len=" + Text.Length + " text=[" + Text + "]");
            if (SetHr < 0) {
                return SetHr;
            }
            var CollapseHr = Range.Collapse(EditCookie, TfAnchorEnd);
            Log("上屏(退回) Collapse hr=0x" + CollapseHr.ToString("X8"));
            var Select = new TfSelection { Range = Selection.Range, ActiveEnd = 0, Interim = 0 };
            var SelectHr = ContextGen.SetSelection(EditCookie, 1, (IntPtr)(&Select));
            if (SelectHr < 0) {
                LogError("上屏(退回) SetSelection hr=0x" + SelectHr.ToString("X8"));
            }
            else {
                Log("上屏(退回) SetSelection hr=0x" + SelectHr.ToString("X8"));
            }
            return SetHr;
        }
        finally {
            Marshal.Release(Selection.Range);
        }
    }

    /// <summary>
    /// 結束組字（照 Weasel `CEndCompositionEditSession::DoEditSession`，`Composition.cpp:100-120`）。
    /// </summary>
    /// <param name="State">文字服務狀態。</param>
    /// <param name="EditCookie">這次編輯會話的 cookie。</param>
    /// <param name="Clear">true 表示結束前把組字範圍清空（取消組字用）；上屏時傳 false，保留剛寫進去的字。</param>
    /// <returns>HRESULT。</returns>
    private static int EndCompositionCore(TsfState State, uint EditCookie, bool Clear) {
        var CompPtr = Interlocked.Exchange(ref State.Composition, IntPtr.Zero);
        if (CompPtr == IntPtr.Zero) {
            LogWarn("結束組字：沒有組字（可能已被應用終止） clear=" + (Clear ? 1 : 0));
            return SOk;
        }
        try {
            var Comp = ComInterfaceMarshaller<ITfCompositionGen>.ConvertToManaged((void*)CompPtr);
            if (Comp is null) {
                LogError("結束組字: ITfComposition 轉換失敗 comp=" + CompPtr.ToString("X"));
                return EFail;
            }
            if (Clear) {
                var GetHr = Comp.GetRange(out var RangePtr);
                if (GetHr >= 0 && RangePtr != IntPtr.Zero) {
                    try {
                        var Range = ComInterfaceMarshaller<ITfRangeGen>.ConvertToManaged((void*)RangePtr);
                        if (Range is not null) {
                            var ClearHr = Range.SetText(EditCookie, 0, IntPtr.Zero, 0);
                            Log("結束組字 清空範圍 hr=0x" + ClearHr.ToString("X8"));
                        }
                    }
                    finally {
                        Marshal.Release(RangePtr);
                    }
                }
            }
            var Hr = Comp.EndComposition(EditCookie);
            LogWarn("結束組字 EndComposition hr=0x" + Hr.ToString("X8") + " clear=" + (Clear ? 1 : 0));
            return Hr;
        }
        finally {
            Marshal.Release(CompPtr);
        }
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

    /// <summary>ITfCompositionSink 的 QueryInterface；只支援 IUnknown 與自己。</summary>
    private static int CompositionSinkQueryCore(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
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

    /// <summary>
    /// 應用主動結束組字時的通知（Weasel `WeaselTSF::OnCompositionTerminated`，`Composition.cpp:389-398`）。
    /// </summary>
    /// <param name="ThisPtr">ITfCompositionSink 的介面指標。</param>
    /// <param name="EditCookie">應用給的寫入 cookie。</param>
    /// <param name="Composition">被結束的組字。</param>
    /// <returns>一律 S_OK。</returns>
    /// <remarks>
    /// Weasel 的註釋寫得很白：組字字串為空時，連「正常結束」也會走進來，所以不能假設這是異常。
    /// 這裡只做「放掉手上的組字」，不主動再叫 `EndComposition`——那個呼叫很可能就是觸發通知的人，
    /// 重入只會在別人的會話裏再排一次會話。日誌留一行，之後才看得出是「我們結束的」還是「應用結束的」。
    /// </remarks>
    private static int OnCompositionTerminatedCore(IntPtr ThisPtr, uint EditCookie, IntPtr Composition) {
        try {
            var State = GetState(ThisPtr);
            if (State is null) {
                return SOk;
            }
            var Owned = Interlocked.Exchange(ref State.Composition, IntPtr.Zero);
            if (Owned != IntPtr.Zero) {
                Marshal.Release(Owned);
            }
            LogWarn("OnCompositionTerminated ec=0x" + EditCookie.ToString("X")
                + " comp=" + Composition.ToString("X")
                + " 我們持有的那份=" + (Owned != IntPtr.Zero ? "已釋放" : "早就沒了"));
            return SOk;
        }
        catch (Exception Exception) {
            LogError("OnCompositionTerminated EX " + Exception);
            return SOk;
        }
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
            CollectTargets(CompositionSinkVtbl, 4, Targets);
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
            // C++（0xE06D7363）與 .NET（0xE0434352）的例外在 Chromium 裏是常態：
            // 每一個 throw 都會走到這裏，而我們在這裏寫檔。只記真正的 SEH 錯誤，
            // 免得在別人的例外派發路徑上做幾十次檔案 IO，還把日誌灌滿。
            if (Code == 0xE06D7363 || Code == 0xE0434352) {
                return 0;
            }
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
    private static int CompositionSinkQueryEntry(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) => CompositionSinkQueryCore(ThisPtr, Riid, Ppv);

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
