namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using Ngan.Ime.Tsf.Ipc;

[Doc($$"""
NativeAOT 版的 TSF 薄外殼。整個 DLL 不帶 CLR，只做四件事：
1. 提供 DllGetClassObject 與 IClassFactory，讓 Windows 建立文字服務；
2. 實作 ITfTextInputProcessor、ITfKeyEventSink、ITfEditSession、ITfCompositionSink 的 vtable
   （手寫；不以 `[ComImport]` 走內建 COM）；
3. 把每一顆按鍵送給服務端，吃不吃照引擎的回答；
4. 上屏照 Weasel：在真正的組字範圍裡寫字。
對外呼叫的介面分兩類，別搞混：
+ *已被呼叫端*（實作 ITfTextInputProcessor、ITfKeyEventSink、ITfEditSession、
  ITfCompositionSink、IClassFactory）：仍舊手寫 vtable，表格見下。
+ *呼叫端*（ITfContext、ITfContextView、ITfRange、ITfKeystrokeMgr）：
  ITfContextView 已改用生成式宣告（見 `ITfContextViewGen.cs`），其餘仍舊手寫。
  遷移中的狀態，兩者在同一個檔案裏並存是刻意的，不是漏改。

vtable 對照表（依 mingw 的 `msctf.h` 逐一核對，2026-11-15）：
+ `ITfContext`：3 RequestEditSession、4 InWriteSession、5 GetSelection、
  6 SetSelection、7 GetStart、8 GetEnd、9 GetActiveView、10 EnumViews、11 GetStatus。
  少算 6 的 SetSelection 一格，GetActiveView 就會指到 GetEnd——2026-11-15 的 notepad 崩潰就是這樣。
+ `ITfContextView`：3 GetRangeFromPoint、4 GetTextExt、5 GetScreenExt、6 GetWnd。
+ `ITfRange`：3 GetText、4 SetText、…、15 Collapse。
+ `ITfEditSession`：3 DoEditSession。
+ `ITfCompositionSink`：3 OnCompositionTerminated（`msctf.h:7071-7093`）。
+ `ITfKeystrokeMgr`：3 AdviseKeyEventSink、4 UnadviseKeyEventSink。
+ `ITfTextInputProcessor`：3 Activate、4 Deactivate。
+ `ITfKeyEventSink`：3 OnSetFocus、4 OnTestKeyDown、5 OnTestKeyUp、6 OnKeyDown、
  7 OnKeyUp、8 OnPreservedKey。

*上屏照 Weasel 的組字生命週期*（2026-11-15 重寫）：一顆會上屏的鍵，依序排三個
**非同步可寫**編輯會話——開始組字、把最終文字寫進組字範圍、結束組字
（Weasel `EditSession.cpp:26-30` 的提交段，各步的會話在 `Composition.cpp:73`、`:365`、`:121`）。
三個會話都走 `TF_ES_ASYNCDONTCARE | TF_ES_READWRITE`，**沒有任何同步會話**：
Weasel 整份原始碼一次都沒用 `TF_ES_SYNC`，而我們的會話是從按鍵回調裏發出的，
同步等待等於在別人的 UI 執行緒上等一個只有它自己才能給的鎖。

*為何一定要有真的組字*：Chromium 的 EditContext 只把「替換組字範圍」當正常上屏；
直接往選取範圍塞字，它會用自己的路徑再插一次，使用者看到的就是同一個字重複。
微軟拼音只有直接上屏的符號（，。數字）會重複，正是因為那些也沒有組字。

*與 Weasel 的唯一差別*：Weasel 的組字從第一個鍵活到整段輸入結束（inline preedit 要它），
我們沒有 inline preedit，所以每次上屏都「建 → 寫 → 結束」，組字只活三個會話。
因為活得這麼短，Weasel 那個「先塞一個空格給 CUAS 量 GetTextExt」的手術（`Composition.cpp:52`）
我們不做——那段時間裏我們一次 GetTextExt 都不會叫。

*組字被應用結束*：`ITfCompositionSink::OnCompositionTerminated` 一定接（Weasel 也接）。
不接的話，Chromium 這類會主動終止組字的應用會讓我們握着一個已死的 `ITfComposition`。
""")]
internal static unsafe partial class NativeTsf {
	[Doc($$"""
	標準 COM 介面識別碼。
	""")]
	private static readonly Guid IidIUnknown = new("00000000-0000-0000-C000-000000000046");
	[Doc($$"""
	IClassFactory。
	""")]
	private static readonly Guid IidClassFactory = new("00000001-0000-0000-C000-000000000046");
	[Doc($$"""
	ITfTextInputProcessor。
	""")]
	private static readonly Guid IidTextInputProcessor = new("AA80E7F7-2021-11D2-93E0-0060B067B86E");
	[Doc($$"""
	ITfKeyEventSink。
	""")]
	private static readonly Guid IidKeyEventSink = new("AA80E7F5-2021-11D2-93E0-0060B067B86E");
	[Doc($$"""
	ITfKeystrokeMgr。
	""")]
	private static readonly Guid IidKeystrokeMgr = new("AA80E7F0-2021-11D2-93E0-0060B067B86E");
	private static readonly Guid IidEditSession = new("AA80E803-2021-11D2-93E0-0060B067B86E");
	[Doc($$"""
	ITfInsertAtSelection：取插入點範圍用。
	""")]
	private static readonly Guid IidInsertAtSelection = new("55CE16BA-3014-41C1-9CEB-FADE1446AC6C");
	[Doc($$"""
	ITfContextComposition：開組字用。
	""")]
	private static readonly Guid IidContextComposition = new("D40C8AAE-AC92-4FC7-9A11-0EE0E23AA39B");
	[Doc($$"""
	ITfCompositionSink：組字被應用結束時的通知介面（msctf.h:7057）。
	""")]
	private static readonly Guid IidCompositionSink = new("A781718C-579A-4B15-A280-32B8577ACC5E");

	private const i32 SOk = 0;
	private const i32 SFalse = 1;
	private const i32 ENoInterface = unchecked((i32)0x80004002);
	private const i32 EFail = unchecked((i32)0x80004005);
	private const i32 EInvalidArg = unchecked((i32)0x80070057);
	private const i32 ENoAggregation = unchecked((i32)0x80040110);


	[Doc($$"""
	編輯會話旗標；數值一律照 `msctf.h:2278-2286`，不憑記憶（先前把 ASYNCDONTCARE 寫成 0x8，那其實是 ASYNC）。

	*哪幾段用同步*：照 Weasel 0.15.0——
	`_StartComposition`／`_EndComposition` 用 `TF_ES_SYNC | TF_ES_READWRITE`，
	`_InsertText`／`_UpdateComposition` 用 `TF_ES_ASYNCDONTCARE | TF_ES_READWRITE`。
	先前我們一律用非同步，於是按鍵回來時組字範圍與那個佔位空格**還沒寫進文件**，
	同一顆鍵裡排的量測會話讀不到 `State.Composition`，只能退回去量選取範圍——
	候選窗因此先出現在上一個字的位置，下一顆鍵才搬過去（使用者看到的就是跳一下）。
	""")]
	private const u32 TfEsSync = 0x1;
	private const u32 TfEsRead = 0x2;
	private const u32 TfEsReadWrite = 0x6;
	[Doc($$"""
	`TF_ES_ASYNCDONTCARE`（msctf.h:2278）：值就是 0，不是 0x8。
	""")]
	private const u32 TfEsAsyncDontCare = 0x0;
	[Doc($$"""
	`TF_ES_ASYNC`（msctf.h:2286）：0x8。本殼不用它；留著是為了別再把兩者搞混。
	""")]
	private const u32 TfEsAsync = 0x8;
	[Doc($$"""
	`TF_ANCHOR_START`（msctf.h）：把組字範圍收成起點。
	""")]
	private const u32 TfAnchorStart = 0;

	[Doc($$"""
	`TF_ANCHOR_END`：把範圍收成終點，游標留在文字之後。

	*組字起點也要用它*：Weasel 0.15.0 `CStartCompositionEditSession::DoEditSession` 是
	先 `SetText(..., L" ", 1)`，再**無條件** `Collapse(TF_ANCHOR_END)`，最後 `SetSelection`——
	所以佔位空格在遊標左邊、遊標在空格之後。master（0.16）才改成依 `inline_preedit` 分支；
	先前照那個分支抄成 START，空格才會跑到遊標右邊。
	""")]
	private const u32 TfAnchorEnd = 1;
	[Doc($$"""
	`TF_S_ASYNC`（msctf.h:581）：非同步會話已排入佇列。
	""")]
	private const i32 TfSAsync = 0x00040300;
	[Doc($$"""
	TF_IAS_QUERYONLY：只取插入點範圍，不插入文字（msctf.h:6545）。
	""")]
	private const u32 TfIasQueryOnly = 0x2;
	[Doc($$"""
	TF_ST_CORRECTION：把這次寫入標成「修正」（msctf.h:5817）；Weasel 塞佔位空格時用它。
	""")]
	private const u32 TfStCorrection = 1;
	[Doc($$"""
	`TF_DEFAULT_SELECTION`（`msctf.h:2301` → `TS_DEFAULT_SELECTION` = `~0`）：取「預設的那個選取」。
	Weasel 的 `GetSelection` 一律傳這個值（`WeaselTSF/Composition.cpp:169`、`TextEditSink.cpp:35`），
	而不是傳 0（0 是「第 0 個選取」，語義靠猜）。兩者在單一選取的控件上結果相同，
	但照 Weasel 傳常量不必猜。
	""")]
	private const u32 TfDefaultSelection = 0xFFFFFFFFu;
	[Doc($$"""
	一次只取／設一個選取範圍（`GetSelection`／`SetSelection` 的 count 參數）。
	""")]
	private const u32 OneSelection = 1;

	[Doc($$"""
	組字範圍的佔位字元：**真空格 U+0020**。
	照 Weasel 0.15.0 `WeaselTSF/Composition.cpp:52`：
	`pRangeComposition->SetText(ec, TF_ST_CORRECTION, L" ", 1);` —— 那是真空格，
	它上面的註釋寫「zero width space」是註釋過時，程式碼才是準的。
	*為何不能用零寬空格*：這個佔位字元在某些應用／輸入情境下會漏進正文；
	真空格漏出來看得見、當場就能發現，零寬空格漏出來完全感知不到。
	2026-11-15 23:20 的實測說明了為何非放不可——「空組字 + 立刻寫字 + 立刻結束」
	在記事本上正常，在 VSCode（Chromium 的 EditContext）上卻重複上屏，
	代表那個空的、只活一毫秒的組字並沒有被當成一次組字。
	""")]
	private const str CompositionPlaceholder = " ";

	[Doc($$"""
	螢幕矩形；手寫 vtable 與生成式宣告共用這一個結構。
	""")]
	[StructLayout(LayoutKind.Sequential)]
	internal struct NativeRect {
		public i32 Left;
		public i32 Top;
		public i32 Right;
		public i32 Bottom;
	}

	[Doc($$"""
	選取範圍；手寫 vtable 與生成式宣告共用。
	""")]
	[StructLayout(LayoutKind.Sequential)]
	internal struct TfSelection {
		public IntPtr Range;
		public u32 ActiveEnd;
		public i32 Interim;
	}

	private static readonly IntPtr* TipVtbl;
	private static readonly IntPtr* SinkVtbl;
	private static readonly IntPtr* FactoryVtbl;
	private static readonly IntPtr* EditSessionVtbl;
	[Doc($$"""
	ITfCompositionSink 的 vtable；組字被應用結束時 TSF 從這裡叫回來。
	""")]
	private static readonly IntPtr* CompositionSinkVtbl;

	private static readonly object FactoryGate = new();
	private static IntPtr FactoryView;
	private static i32 FactoryRefCount = 1;

	static NativeTsf() {
		TipVtbl = (IntPtr*)NativeMemory.Alloc(5, (nuint)sizeof(IntPtr));
		TipVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, i32>)&QueryInterfaceEntry;
		TipVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, i32>)&AddRefEntry;
		TipVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, i32>)&ReleaseEntry;
		TipVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, u32, i32>)&ActivateEntry;
		TipVtbl[4] = (IntPtr)(delegate* unmanaged<IntPtr, i32>)&DeactivateEntry;

		SinkVtbl = (IntPtr*)NativeMemory.Alloc(9, (nuint)sizeof(IntPtr));
		SinkVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, i32>)&QueryInterfaceEntry;
		SinkVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, i32>)&AddRefEntry;
		SinkVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, i32>)&ReleaseEntry;
		SinkVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, i32, i32>)&OnSetFocusEntry;
		SinkVtbl[4] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, i32*, i32>)&OnTestKeyDownEntry;
		SinkVtbl[5] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, i32*, i32>)&OnTestKeyUpEntry;
		SinkVtbl[6] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, i32*, i32>)&OnKeyDownEntry;
		SinkVtbl[7] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, i32*, i32>)&OnKeyUpEntry;
		SinkVtbl[8] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, Guid*, i32*, i32>)&OnPreservedKeyEntry;

		FactoryVtbl = (IntPtr*)NativeMemory.Alloc(5, (nuint)sizeof(IntPtr));
		FactoryVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, i32>)&FactoryQueryInterfaceEntry;
		FactoryVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, i32>)&FactoryAddRefEntry;
		FactoryVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, i32>)&FactoryReleaseEntry;
		FactoryVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, Guid*, IntPtr*, i32>)&CreateInstanceEntry;
		FactoryVtbl[4] = (IntPtr)(delegate* unmanaged<IntPtr, i32, i32>)&LockServerEntry;

		EditSessionVtbl = (IntPtr*)NativeMemory.Alloc(4, (nuint)sizeof(IntPtr));
		EditSessionVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, i32>)&EditSessionQueryEntry;
		EditSessionVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, i32>)&AddRefEntry;
		EditSessionVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, i32>)&ReleaseEntry;
		EditSessionVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, u32, i32>)&DoEditSessionEntry;

		// ITfCompositionSink：IUnknown 三個 + OnCompositionTerminated（msctf.h:7071-7093）。
		// 交給 ITfContextComposition::StartComposition 當第三個參數——
		// Weasel 傳的是自己（WeaselTSF），我們傳這個 view。
		CompositionSinkVtbl = (IntPtr*)NativeMemory.Alloc(4, (nuint)sizeof(IntPtr));
		CompositionSinkVtbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, i32>)&CompositionSinkQueryEntry;
		CompositionSinkVtbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, i32>)&AddRefEntry;
		CompositionSinkVtbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, i32>)&ReleaseEntry;
		CompositionSinkVtbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, u32, IntPtr, i32>)&OnCompositionTerminatedEntry;

		// 先算出模組標籤：之後每一行日誌都帶著它，才分得出是哪一版的外殼寫的。
		SelfTag = ComputeSelfTag();
		RegisterCfgTargets();
		AddVectoredExceptionHandler(1, (IntPtr)(delegate* unmanaged<IntPtr, i32>)&VectoredExceptionHandler);
		// 一律落盤：行程一載入這支 DLL 就留一行，連「TSF 有沒有來要 class factory」都還沒發生。
		// 排查「某個應用切不過來」時，這一行有沒有出現就是第一個分界。
		LogWarn("shell loaded pid=" + Environment.ProcessId + " path=" + (GetOwnModulePath() is { Length: > 0 } P ? P : "?"));
	}

	[Doc($$"""
	佇列裏的一步組字操作；一步只做一件事，對應一次非同步編輯會話。
	""")]
	private struct CompositionJob {
		[Doc($$"""
		這一步要做什麼。
		""")]
		public ETsfCompositionStep Step;
		[Doc($$"""
		insert 用：要上屏的文字。
		""")]
		public str Text;
		[Doc($$"""
		End 用：結束前要不要把組字範圍清空（Weasel 上屏時傳 false，保留剛寫進去的字）。
		""")]
		public bool Clear;
		[Doc($$"""
		排這一步時所屬的 `ITfContext`；**這一步自己持有一份參考**（`AddRef`）。
		照 Weasel：每個 `CEditSession` 物件在建構時就持一份 `com_ptr<ITfContext>`
		（`WeaselTSF/EditSession.h`），所以會話執行時用的是「排它時那個 context」，
		不是「最新的那個」。少了這一份，切換控件之後排隊中的步驟會作用在新文件上。
		由 `DoEditSessionCore` 在執行完之後釋放。
		""")]
		public IntPtr Context;
		[Doc($$"""
		排這一步時那顆鍵的按鍵序號。
		只有 {{nameof(ETsfCompositionStep.Measure)}} 用得到：位置訊息要帶著它送給服務端，
		服務端才分得出「這條位置屬於哪一顆鍵」。排隊時就記下來，因為會話回來時
		可能已經有下一顆鍵送出了（`LastKeySeq` 會變）。
		""")]
		public i32 Seq;
	}

	[Doc($$"""
	單一文字服務執行個體的狀態；兩個介面指標共用同一份參考計數。
	""")]
	private sealed class TsfState {
		[Doc($$"""
		COM 參考計數，兩個 view 共用。
		""")]
		public i32 RefCount = 1;
		[Doc($$"""
		ITfTextInputProcessor 的介面指標。
		""")]
		public IntPtr TipView;
		[Doc($$"""
		ITfKeyEventSink 的介面指標。
		""")]
		public IntPtr SinkView;
		[Doc($$"""
		對應的 GCHandle，釋放時要一起 Free。
		""")]
		public IntPtr TipHandle;
		[Doc($$"""
		對應的 GCHandle，釋放時要一起 Free。
		""")]
		public IntPtr SinkHandle;
		[Doc($$"""
		TSF 指派的 client id。
		""")]
		public u32 ClientId;
		[Doc($$"""
		ITfKeystrokeMgr 的原始指標，Deactivate 時釋放。
		""")]
		public IntPtr KeystrokeMgr;
		[Doc($$"""
		ITfEditSession 的介面指標（自己實作，交給 ITfContext::RequestEditSession）。
		""")]
		public IntPtr EditSessionView;
		[Doc($$"""
		ITfEditSession 的 GCHandle。
		""")]
		public IntPtr EditSessionHandle;
		[Doc($$"""
		目前按鍵所屬的 ITfContext（只在 edit session 內有效）。
		""")]
		public IntPtr Context;
		[Doc($$"""
		目前的組字（ITfComposition，自己持有一份參考）；0 表示沒有組字。
		只由三個地方改：開始組字（寫入）、結束組字與 {{nameof(OnCompositionTerminatedCore)}}
		（兩者都用 Interlocked 換出再釋放，避免重複釋放同一個指標）。
		""")]
		public IntPtr Composition;
		[Doc($$"""
		ITfCompositionSink 的介面指標；交給 StartComposition 當第三個參數。
		""")]
		public IntPtr CompositionSinkView;
		[Doc($$"""
		ITfCompositionSink 的 GCHandle。
		""")]
		public IntPtr CompositionSinkHandle;
		[Doc($$"""
		待執行的組字步驟佇列；一個步驟對應一次非同步編輯會話。
		*為何要佇列*：上屏不是一個動作，而是「開始組字→寫入→結束」三個會話
		（照 Weasel）。會話是非同步的，同一個 edit session 物件只能表達「這次要做哪一步」，
		所以步驟必須排隊；少了佇列，第二顆很快的上屏鍵會把第一步覆蓋掉，字就丟了。
		""")]
		public readonly List<CompositionJob> CompositionJobs = new();
		[Doc($$"""
		按下鍵的查詢結果；等 OnKeyDown 領取。
		""")]
		public TsfReply? ReplyDown;
		[Doc($$"""
		擡起鍵的查詢結果；等 OnKeyUp 領取。
		""")]
		public TsfReply? ReplyUp;
		[Doc($$"""
		按鍵決策：吃不吃、徵詢與執行怎麼配對、上下成對的保證。
		這一項取代了原本的 DownPending／UpPending 兩個布林旗標。
		那兩個旗標沒有定義「合法組合」，應用不照協議送事件時就會留下殘留狀態，
		之後每一顆鍵都被誤判成「已問過且已吃掉」。
		""")]
		public readonly Ngan.Ime.Tsf.Ipc.TsfKeyPolicy Policy = new();
		[Doc($$"""
		插入點在螢幕上的左邊界（像素）；僅在 CaretValid 為 true 時有意義。
		""")]
		public i32 CaretPointX;
		[Doc($$"""
		插入點在螢幕上的下邊界（像素）；僅在 CaretValid 為 true 時有意義。
		""")]
		public i32 CaretPointY;
		[Doc($$"""
		插入點矩形的上邊界（像素）；浮窗翻到文字行上方時用它定位。
		""")]
		public i32 CaretPointTop;
		[Doc($$"""
		是否量到過插入點。false 時 x/y 的 0 代表「未知」，不是螢幕左上角。
		""")]
		public bool CaretValid;
	}

	[UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
	private static i32 DllGetClassObject(Guid* Rclsid, Guid* Riid, IntPtr* Ppv) {
		// 判空必須在所有解引用之前：這是整個 DLL 的最外層入口，出錯就是宿主行程直接 AV。
		if (Rclsid is null || Riid is null || Ppv is null) {
			return EInvalidArg;
		}
		try {
			LogWarn("DllGetClassObject pid=" + Environment.ProcessId + " clsid=" + *Rclsid + " riid=" + *Riid);
			// 不再比對 CLSID：每次安裝用屬於自己的 CLSID 註冊（版本化安裝＝一個獨立輸入法），
			// 只要系統按註冊表找上這個 DLL，就回同一個 class factory。
			EnsureFactory();
			return FactoryQueryCore(FactoryView, Riid, Ppv);
		} catch (Exception Exception) {
			Log("DllGetClassObject EX " + Exception);
			return EFail;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
	private static i32 DllCanUnloadNow() {
		return SFalse;
	}

	private static void EnsureFactory() {
		lock (FactoryGate) {
			if (FactoryView == IntPtr.Zero) {
				var block = (IntPtr*)NativeMemory.Alloc(2, (nuint)sizeof(IntPtr));
				block[0] = (IntPtr)FactoryVtbl;
				block[1] = IntPtr.Zero;
				FactoryView = (IntPtr)block;
			}
		}
	}

	private static IntPtr MakeView(IntPtr* Vtbl, TsfState State, out IntPtr Handle) {
		var block = (IntPtr*)NativeMemory.Alloc(2, (nuint)sizeof(IntPtr));
		block[0] = (IntPtr)Vtbl;
		var gcHandle = GCHandle.Alloc(State);
		Handle = GCHandle.ToIntPtr(gcHandle);
		block[1] = Handle;
		return (IntPtr)block;
	}

	private static TsfState? GetState(IntPtr ThisPtr) {
		if (ThisPtr == IntPtr.Zero) {
			return null;
		}
		var block = (IntPtr*)ThisPtr;
		var handle = block[1];
		if (handle == IntPtr.Zero) {
			return null;
		}
		return GCHandle.FromIntPtr(handle).Target as TsfState;
	}

	[Doc($$"""
	釋放一個 view 區塊與它對應的 GCHandle。
	只在「物件還沒交給 TSF」的失敗路徑上呼叫；交出去之後生命週期由 COM 參考計數決定。
	#Prm[{{nameof(View)}}][介面指標（指向兩格的區塊：vtable 與 GCHandle）。]
	#Prm[{{nameof(Handle)}}][該 view 的 GCHandle。]
	""")]
	private static void FreeView(IntPtr View, IntPtr Handle) {
		if (Handle != IntPtr.Zero) {
			GCHandle.FromIntPtr(Handle).Free();
		}
		if (View != IntPtr.Zero) {
			NativeMemory.Free((void*)View);
		}
	}

	[Doc($$"""
	把一個還沒交出去的 {{nameof(TsfState)}} 的五個 view 全部回收。
	#Prm[{{nameof(State)}}][剛建立、尚未回給 TSF 的狀態。]
	""")]
	private static void FreeViews(TsfState State) {
		FreeView(State.TipView, State.TipHandle);
		FreeView(State.SinkView, State.SinkHandle);
		FreeView(State.EditSessionView, State.EditSessionHandle);
		FreeView(State.CompositionSinkView, State.CompositionSinkHandle);
	}

	private static i32 QueryCore(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
		Log("QI core this=" + ThisPtr.ToString("X"));
		var state = GetState(ThisPtr);
		if (state is null) {
			return ENoInterface;
		}
		Log("QI " + *Riid + " this=" + ThisPtr.ToString("X"));
		if (*Riid == IidIUnknown) {
			*Ppv = ThisPtr;
			Interlocked.Increment(ref state.RefCount);
			return SOk;
		}
		if (*Riid == IidTextInputProcessor) {
			*Ppv = state.TipView;
			Interlocked.Increment(ref state.RefCount);
			return SOk;
		}
		if (*Riid == IidKeyEventSink) {
			*Ppv = state.SinkView;
			Interlocked.Increment(ref state.RefCount);
			return SOk;
		}
		*Ppv = IntPtr.Zero;
		return ENoInterface;
	}

	private static i32 AddRefCore(IntPtr ThisPtr) {
		var state = GetState(ThisPtr);
		var count = state is null ? 0 : Interlocked.Increment(ref state.RefCount);
		Log("AddRef this=" + ThisPtr.ToString("X") + " -> " + count);
		return count;
	}

	private static i32 ReleaseCore(IntPtr ThisPtr) {
		var state = GetState(ThisPtr);
		if (state is null) {
			return 0;
		}
		var count = Interlocked.Decrement(ref state.RefCount);
		Log("release this=" + ThisPtr.ToString("X") + " -> " + count);
		// POC：物件在行程生命期內不釋放。
		// 之前一旦參考計數歸零就釋放 view，結果 TSF 之後仍拿著 sink 指標，拿到失效記憶體。
		return count;
	}

	private static i32 FactoryQueryCore(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
		if (*Riid == IidIUnknown || *Riid == IidClassFactory) {
			*Ppv = ThisPtr;
			Interlocked.Increment(ref FactoryRefCount);
			return SOk;
		}
		*Ppv = IntPtr.Zero;
		return ENoInterface;
	}

	private static i32 CreateInstanceCore(IntPtr ThisPtr, IntPtr Outer, Guid* Riid, IntPtr* Ppv) {
		try {
			if (Outer != IntPtr.Zero) {
				return ENoAggregation;
			}
			LogWarn("CreateInstance riid=" + *Riid);
			var state = new TsfState();
			state.TipView = MakeView(TipVtbl, state, out var tipHandle);
			state.TipHandle = tipHandle;
			state.SinkView = MakeView(SinkVtbl, state, out var sinkHandle);
			state.SinkHandle = sinkHandle;
			state.EditSessionView = MakeView(EditSessionVtbl, state, out var editSessionHandle);
			state.EditSessionHandle = editSessionHandle;
			state.CompositionSinkView = MakeView(CompositionSinkVtbl, state, out var compositionSinkHandle);
			state.CompositionSinkHandle = compositionSinkHandle;

			if (*Riid == IidIUnknown || *Riid == IidTextInputProcessor) {
				*Ppv = state.TipView;
				LogWarn("CreateInstance ok tip=" + state.TipView.ToString("X"));
				return SOk;
			}
			if (*Riid == IidKeyEventSink) {
				*Ppv = state.SinkView;
				LogWarn("CreateInstance ok sink=" + state.SinkView.ToString("X"));
				return SOk;
			}
			*Ppv = IntPtr.Zero;
			// 走到這裡代表 TSF 要一個我們不支援的介面。上面已經配置了五個 view 與五個 GCHandle，
			// 全部回收再回 E_NOINTERFACE：COM 入口的洩漏是累積型的（TSF 對不認識的 riid 會來問）。
			LogWarn("CreateInstance E_NOINTERFACE riid=" + *Riid);
			FreeViews(state);
			return ENoInterface;
		} catch (Exception Exception) {
			Log("CreateInstance EX " + Exception);
			return EFail;
		}
	}

	[Doc($$"""
	啟動文字服務：取得 ITfKeystrokeMgr 並把本物件的 key event sink 掛上去。
	*為何要 AdviseKeyEventSink 的第三個參數為 1*：`fForeground` 表示「即使輸入焦點不在本服務
	也要收到按鍵」。少了它，密碼框這類停用輸入法的控件會讓所有按鍵都到不了引擎。
	
	*已移除的探針*：先前這裏有 `NGAN_TSF_MODE=none/unadvise/nullsink` 三個分支與一段
	「self-test sink QI」，那是 POC 期在查 CFG 與 advise 是否生效用的。
	正式路徑只留一條，探針的代價是 60 行樣板把真正的邏輯埋掉。
	""")]
	private static i32 ActivateCore(IntPtr ThisPtr, IntPtr ThreadManager, u32 ClientId) {
		try {
			var state = GetState(ThisPtr);
			if (state is null) {
				return EFail;
			}
			state.ClientId = ClientId;
			var iid = IidKeystrokeMgr;
			IntPtr KeystrokeMgr = IntPtr.Zero;
			var hr = Marshal.QueryInterface(ThreadManager, in iid, out KeystrokeMgr);
			if (hr < 0) {
				LogError("Activate QI(ITfKeystrokeMgr) hr=0x" + hr.ToString("X8"));
				return hr;
			}
			var ksGen = ComInterfaceMarshaller<ITfKeystrokeMgrGen>.ConvertToManaged((void*)KeystrokeMgr);
			if (ksGen is null) {
				LogError("Activate ConvertToManaged(ITfKeystrokeMgr) 回空");
				ReleaseCom(KeystrokeMgr);
				return EFail;
			}
			LogWarn("Activate QI(ITfKeystrokeMgr) ok mgr=" + KeystrokeMgr.ToString("X"));
			hr = ksGen.AdviseKeyEventSink(ClientId, state.SinkView, 1);
			if (hr < 0) {
				LogError("Activate AdviseKeyEventSink hr=0x" + hr.ToString("X8"));
				ReleaseCom(KeystrokeMgr);
				return hr;
			}
			state.KeystrokeMgr = KeystrokeMgr;
			LogWarn("Activate ok clientId=" + ClientId + " pid=" + Environment.ProcessId);
			// 只有在真的掛上按鍵接收器之後才需要看門狗：它要回答的是「這個行程卡死時停在誰身上」。
			EnsureWatchdog();
			return SOk;
		} catch (Exception Exception) {
			LogError("Activate EX " + Exception);
			return EFail;
		}
	}

	private static i32 DeactivateCore(IntPtr ThisPtr) {
		try {
			var state = GetState(ThisPtr);
			if (state is null) {
				return EFail;
			}
			if (state.KeystrokeMgr != IntPtr.Zero) {
				var ksGen = ComInterfaceMarshaller<ITfKeystrokeMgrGen>.ConvertToManaged((void*)state.KeystrokeMgr);
				if (ksGen is not null) {
					ksGen.UnadviseKeyEventSink(state.ClientId);
				}
				ReleaseCom(state.KeystrokeMgr);
				state.KeystrokeMgr = IntPtr.Zero;
			}
			state.Policy.Reset();
			// 插入點快取作廢：下一個控件的位置與這個控件無關。
			state.CaretValid = false;
			AbandonComposition(state);
			SendHide();
			LogWarn("Deactivate done clientId=" + state.ClientId);
			return SOk;
		} catch (Exception Exception) {
			LogError("Deactivate EX " + Exception);
			return EFail;
		}
	}

	private static i32 OnSetFocusCore(IntPtr ThisPtr, i32 IsForeground) {
		try {
			LogWarn("OnSetFocus fg=" + IsForeground + " pid=" + Environment.ProcessId);
			var state = GetState(ThisPtr);
			if (state is null) {
				return SOk;
			}
			if (IsForeground == 0) {
				// 失去焦點：按鍵記錄與插入點都不再有意義，留著只會被下一個控件繼承。
				state.Policy.Reset();
				state.CaretValid = false;
				AbandonComposition(state);
				SendHide();
			}
			return SOk;
		} catch (Exception Exception) {
			LogError("OnSetFocus EX " + Exception);
			return EFail;
		}
	}

	[Doc($$"""
	目前按著的修飾鍵位；位元值由協議層定義，殼端不再自己寫一份數字。
	*取樣方式照 Weasel*：`WeaselTSF/KeyEventSink.cpp:26` 在處理這顆鍵時才 `GetKeyboardState`，
	而 `KeyEvent.cpp:14-28` 就從那份狀態推 mask。這代表「修飾鍵自己那顆事件」也會把自己算成按著
	（按 Shift 時 `keyState[VK_SHIFT]` 已經是 down）——Weasel 也是如此，不要「修正」這個。
	
	*已知偏離*：Weasel 另外送 `LOCK_MASK`（CapsLock 的 toggled 位），並在 CapsLock 按下時
	`mask ^= LOCK_MASK` 把這一位抵掉（`KeyEvent.cpp:20-35`）。我們的協議只有 Shift／Ctrl／Alt 三位，
	沒有 lock 位，故 CapsLock 的 toggled 狀態到不了引擎。目前沒有觀察到因此產生的症狀；
	若日後 CapsLock 行為異常，先補這一位（協議升版），而不是在殼裡另加規則。
	""")]
	private static i32 GetModifiers() {
		var mods = 0;
		if ((GetKeyState(VkShift) & 0x8000) != 0) { mods |= TsfIpc.ModShift; }
		if ((GetKeyState(VkControl) & 0x8000) != 0) { mods |= TsfIpc.ModCtrl; }
		if ((GetKeyState(VkMenu) & 0x8000) != 0) { mods |= TsfIpc.ModAlt; }
		return mods;
	}

	[Doc($$"""
	VK_SHIFT；與 {{nameof(TsfKeyPolicy.VkShift)}} 同值，這裡留常量是為了讀代碼時不必跳檔。
	""")]
	private const i32 VkShift = TsfKeyPolicy.VkShift;
	[Doc($$"""
	VK_CONTROL。
	""")]
	private const i32 VkControl = TsfKeyPolicy.VkCtrl;
	[Doc($$"""
	VK_MENU（Alt）。
	""")]
	private const i32 VkMenu = TsfKeyPolicy.VkMenu;

	[Doc($$"""
	一顆鍵的「按下／擡起」共同流程：結算反向事件、量插入點、決定吃不吃、必要時上屏。
	四個回調（TestKeyDown／TestKeyUp／KeyDown／KeyUp）只差三件事：
	方向、要不要問引擎、問完之後要不要把結果留給對向回調領。
	把流程收在一處，避免「某一條路徑忘了清旗標」這種不對稱缺陷。
	#Prm[{{nameof(State)}}][文字服務狀態。]
	#Prm[{{nameof(context)}}][這次按鍵所屬的 ITfContext。]
	#Prm[{{nameof(VirtualKey)}}][Win32 虛擬鍵碼。]
	#Prm[{{nameof(IsUp)}}][true 表示擡起。]
	#Prm[{{nameof(IsTest)}}][true 表示來自 OnTestKeyXxx（結果要留給 OnKeyXxx 領）。]
	#Prm[{{nameof(IsEaten)}}][回報給 TSF 的「是否吃掉」。]
	""")]
	private static void HandleKeyEvent(TsfState State, IntPtr context, i32 VirtualKey, bool IsUp, bool IsTest, IntPtr LParam, i32* IsEaten) {
		*IsEaten = 0;
		// 高解析度時鐘：這段程式跑在別人的 UI 執行緒上，逐鍵耗時（尤其管道往返）要能直接讀，
		// 不能用 15.6ms 粒度的 TickCount。
		var startedTs = Stopwatch.GetTimestamp();
		var mods = GetModifiers();
		// lParam 第 16 到 23 位是掃描碼、第 24 位是擴展鍵位：
		// VK_SHIFT 對左右是同一個值，只有靠掃描碼才分得出 El 左 Shift 與右 Shift。
		var scan = (i32)((LParam.ToInt64() >> 16) & 0xFF);
		var ext = (i32)((LParam.ToInt64() >> 24) & 1);
		TsfReply? Reply = null;
		// 記下「目前正在處理哪一顆鍵」，只存在記憶體裏：看門狗在卡死時會把它寫出來，
		// 於是「卡在我們自己的回調裏」與「我們早就交還了、應用自己卡」可以直接分辨，
		// 又不必為每一顆鍵多寫一行檔（那是每顆鍵都要付的 IO 成本）。
		Interlocked.Exchange(ref CurrentOpVk, VirtualKey + 1);
		Interlocked.Exchange(ref CurrentOpFlags, (IsUp ? 1 : 0) | (IsTest ? 2 : 0));
		Interlocked.Exchange(ref CurrentOpStart, startedTs);
		try {
			// step 0: 這顆鍵的 context 存一份並持有參考（照 Weasel 的 _pEditSessionContext）。
			// 組字與量測都是非同步會話，回調回來時按鍵早已處理完，靠這份參考讓 context 還活着。
			SetContext(State, context);
			// step 1: 結算反方向尚未領走的徵詢結論（應用可能只送其中一半的事件）。
			State.Policy.SettleOpposite(IsUp);
			// 照 Weasel：所有認得的鍵都送引擎，吃不吃由引擎的回答決定，
			// 不為修飾鍵另立例外（Weasel `KeyEventSink.cpp:11` 就是這樣做的）。
			// 先前那條「修飾鍵一律放行」讓 Alt 永遠到不了引擎，引擎因此沒有機會處理它。
			var cached = IsUp ? State.ReplyUp : State.ReplyDown;
			if (IsTest) {
				// 同一顆鍵的重複徵詢：沿用上一次的結論，不再送管道。
				// *這裡寫死 1 是照 Weasel*：pending 旗標只在引擎說吃掉時才設
				// （`KeyEventSink.cpp:101-104`），而 `OnKeyDown` 領到 pending 時一律回 TRUE
				// （`:111-118`），它也不回去看具體值。改動前先讀 `TsfKeyPolicy` 類註釋的對照段。
				if (State.Policy.TryReuseTest(IsUp, VirtualKey)) {
					*IsEaten = 1;
					return;
				}
				cached = null;
			} else if (State.Policy.TryTakeKey(IsUp, VirtualKey, out var pendingEaten)) {
				// 徵詢已經問過，這裡只領結果，不再問引擎（Weasel `OnKeyDown` 的 pending 分支）。
				*IsEaten = pendingEaten ? 1 : 0;
				Reply = cached;
				if (cached is not null) {
					// 上屏只在「執行」這條路徑做一次。
					// 先前徵詢那條也上屏，同一份回應因此被插進文件兩次（同字重複上屏）。
					// 量測排在組字步驟之後（佇列有序），且只有這一輪真的有東西要顯示時才排。
					ScheduleCompositionWork(State, context, cached, Measure: !IsUp && NeedPosition(cached));
					ClearReply(State, IsUp);
				}
				return;
			}
			ResolveKeyAnchor(State, out var anchorX, out var anchorY);
			Reply = QueryKey(VirtualKey, mods, anchorX, anchorY, IsUp, scan, ext);
			var eat = State.Policy.DecideEat(VirtualKey, IsUp, Reply is { Eaten: true });
			*IsEaten = eat ? 1 : 0;
			if (eat) {
				// 只有吃掉才需要把結果留給 OnKeyXxx；沒吃掉時應用的按鍵照常走，不必再問一次。
				SetReply(State, IsUp, Reply);
			} else {
				ClearReply(State, IsUp);
			}
			if (!IsTest && Reply is not null) {
				// 兩條上屏路徑只留這一條：
				// + 應用送「徵詢 → 執行」時，上屏發生在執行那條（上面 TryTakeKey 的分支）；
				// + 應用只送「執行」時，上屏發生在這裏。
				// 擡起不量：這一輪沒有寫入，量到的必然與按下那次相同；
				// 少一條位置訊息，服務端就不必再擋一次重複。
				ScheduleCompositionWork(State, context, Reply, Measure: !IsUp && NeedPosition(Reply));
			}
			if (IsTest && eat) {
				// 照 Weasel：pending 只在引擎說吃掉時才設（`KeyEventSink.cpp:101-104`）。
				// 這也是為什麼「引擎說不吃時，執行階段會再問一次引擎」——Weasel 原樣，不要改。
				State.Policy.SetTest(IsUp, VirtualKey, true);
			}
		} finally {
			Interlocked.Exchange(ref CurrentOpVk, 0);
			Interlocked.Exchange(ref LastCallbackTs, Stopwatch.GetTimestamp());
			// 每一條返回路徑都留一行；修飾鍵提早放行那條也記，否則看不到 Alt 的 down／up。
			// 附上「這顆鍵在我們這裏花了多少毫秒」，用來分辨「我們拖慢了按鍵」與「應用自己慢」。
			DiagKey(State, VirtualKey, mods, IsUp, IsTest, LParam, *IsEaten, Reply, startedTs);
		}
	}

	[Doc($$"""
	這一輪的回答需不需要一條位置訊息：有候選或組字串時才要。
	*為何不每顆鍵都量*：`ITfContextView::GetTextExt` 在 Chromium 是同步問 renderer 的，
	而我們是在應用的 UI 執行緒上叫它。退格、方向鍵、修飾鍵這些根本不會顯示候選窗的鍵，
	量了只是把 UI 執行緒借給應用去等 renderer。
	*與服務端的一致性*：服務端只在 `preedit` 或候選非空時才留下「待顯示內容」
	（`SvcTsfPipeServer.ShowPopup`），所以這裡的條件就是它的補集——
	凡是會顯示內容的鍵都一定有一條位置，凡是不會顯示的都不必量。
	#Prm[{{nameof(Reply)}}][引擎這一輪的回答。]
	#Rtn[true 表示這一輪要排一步量測。]
	""")]
	private static bool NeedPosition(TsfReply Reply) {
		return Reply.Candidates.Count > 0 || !str.IsNullOrEmpty(Reply.Preedit);
	}

	[Doc($$"""
	記下這一顆鍵的引擎回應，等對向回調領取。
	""")]
	private static void SetReply(TsfState State, bool IsUp, TsfReply? Reply) {
		if (IsUp) {
			State.ReplyUp = Reply;
		} else {
			State.ReplyDown = Reply;
		}
	}

	[Doc($$"""
	丟掉上一顆鍵留下的引擎回應。
	""")]
	private static void ClearReply(TsfState State, bool IsUp) {
		if (IsUp) {
			State.ReplyUp = null;
		} else {
			State.ReplyDown = null;
		}
	}

	[Doc($$"""
	記住這顆鍵所屬的 `ITfContext`，並**持有一份參考**。
	*這是照 Weasel 抄的，不是我們的發明*：Weasel 的 `_pEditSessionContext` 是
	`com_ptr<ITfContext>`（`WeaselTSF/Composition.cpp:380-389` 寫入），
	而每個 `CEditSession` 也在建構時持一份 `com_ptr<ITfContext>`（`WeaselTSF/EditSession.h`）。
	它們都靠 COM 參考計數讓 context 活過回調。
	
	*為何非持參考不可*：組字與量測都是**非同步**編輯會話（`TF_ES_ASYNCDONTCARE`），
	真正執行時按鍵回調早已返回；TSF 對 `ITfContext` 的有效性只保證在回調期間。
	先前只存裸指標，等於對一個可能已釋放的物件發 `QueryInterface`／`SetSelection`——
	那會崩在別人的行程裡，而且沒有我們的堆疊。
	
	*為何不清成 0*：Weasel 也不清。失焦時 `_AbortComposition` 用的就是這一份
	（`Composition.cpp:402-409`），清掉反而讓「離開焦點時結束組字」這條路走不通。
	下一次按鍵會以新 context 覆寫並釋放舊的；行程結束則由 COM 回收。
	
	*執行緒*：只由 TSF 回調那條 UI 執行緒呼叫。
	#Prm[{{nameof(State)}}][文字服務狀態。]
	#Prm[{{nameof(context)}}][TSF 傳進回調的 context 指標；{{nameof(IntPtr.Zero)}} 表示沒有。]
	""")]
	private static void SetContext(TsfState State, IntPtr context) {
		if (context != IntPtr.Zero) {
			AddRefCom(context);
		}
		var old = State.Context;
		State.Context = context;
		if (old != IntPtr.Zero) {
			ReleaseCom(old);
		}
	}

	private static i32 OnTestKeyDownCore(IntPtr ThisPtr, IntPtr context, IntPtr WParam, IntPtr LParam, i32* IsEaten) {
		try {
			var state = GetState(ThisPtr);
			if (state is null) {
				*IsEaten = 0;
				return EFail;
			}
			HandleKeyEvent(state, context, (i32)WParam.ToInt64(), false, true, LParam, IsEaten);
			return SOk;
		} catch (Exception Exception) {
			LogError("OnTestKeyDown EX " + Exception);
			*IsEaten = 0;
			return EFail;
		}
	}

	private static i32 OnKeyDownCore(IntPtr ThisPtr, IntPtr context, IntPtr WParam, IntPtr LParam, i32* IsEaten) {
		try {
			var state = GetState(ThisPtr);
			if (state is null) {
				*IsEaten = 0;
				return EFail;
			}
			HandleKeyEvent(state, context, (i32)WParam.ToInt64(), false, false, LParam, IsEaten);
			return SOk;
		} catch (Exception Exception) {
			LogError("OnKeyDown EX " + Exception);
			*IsEaten = 0;
			return EFail;
		}
	}

	private static i32 OnTestKeyUpCore(IntPtr ThisPtr, IntPtr context, IntPtr WParam, IntPtr LParam, i32* IsEaten) {
		try {
			var state = GetState(ThisPtr);
			if (state is null) {
				*IsEaten = 0;
				return EFail;
			}
			HandleKeyEvent(state, context, (i32)WParam.ToInt64(), true, true, LParam, IsEaten);
			return SOk;
		} catch (Exception Exception) {
			LogError("OnTestKeyUp EX " + Exception);
			*IsEaten = 0;
			return EFail;
		}
	}

	private static i32 OnKeyUpCore(IntPtr ThisPtr, IntPtr context, IntPtr WParam, IntPtr LParam, i32* IsEaten) {
		try {
			var state = GetState(ThisPtr);
			if (state is null) {
				*IsEaten = 0;
				return EFail;
			}
			HandleKeyEvent(state, context, (i32)WParam.ToInt64(), true, false, LParam, IsEaten);
			return SOk;
		} catch (Exception Exception) {
			LogError("OnKeyUp EX " + Exception);
			*IsEaten = 0;
			return EFail;
		}
	}

	[Doc($$"""
	依引擎這一輪的回答，安排組字要做的動作：開組字、寫入上屏文字、結束組字。
	*這是照 Weasel `WeaselTSF::DoEditSession`（`EditSession.cpp:18-45`）搬過來的判斷*：
	+ 有上屏文字：若還沒組字就先開組字 → 寫入最終文字 → 結束組字（保留文字）；
	+ 沒有上屏但有 preedit：若還沒組字就開組字，讓它在打字期間一直存在；
	+ 兩者都沒有而我們還握着組字：結束它並清空。
	
	*與舊版最關鍵的差別*：組字不再是「上屏那一刻才建、一毫秒內拆掉」，而是從組字開始
	活到上屏或取消。Chromium 的 EditContext 只認得這種有生命週期的組字；
	空的、瞬生瞬滅的組字它會當成不存在，於是文字又被它自己插一次（VSCode 的重複上屏）。
	
	*為何不在這裏等結果*：非同步會話的成敗由各自的 `DoEditSession` 寫進日誌，
	按鍵路徑只負責排隊。要嘛相信日誌，要嘛就會為了知道結果去阻塞別人的 UI 執行緒。
	
	*與 Weasel 的唯一時序差異*：Weasel 在 `OnTestKeyDown` 就呼叫 `_UpdateComposition(pContext)`
	（`KeyEventSink.cpp:103`），而 `OnKeyDown` 的 pending 分支不再更新；我們是反過來——
	徵詢那條只快取回應，更新發生在執行那條（含 pending 分支）。
	兩者「每顆鍵更新一次」的結果相同，而我們的時序是 2026-11-15 深夜實測修好重複上屏之後的版本，
	*不要再改成 Weasel 那一邊*，除非能重新做一次真機驗證。
	#Prm[{{nameof(State)}}][文字服務狀態。]
	#Prm[{{nameof(context)}}][按鍵所屬的 context。]
	#Prm[{{nameof(Reply)}}][引擎的回答。]
	#Prm[{{nameof(Measure)}}][排完組字步驟之後，要不要再排一步「量插入點並送位置」。]
	""")]
	private static void ScheduleCompositionWork(TsfState State, IntPtr context, TsfReply Reply, bool Measure) {
		var hasCommit = Reply.Commits.Count > 0;
		var hasPreedit = !str.IsNullOrEmpty(Reply.Preedit);
		var hasComposition = Volatile.Read(ref State.Composition) != IntPtr.Zero;
		var commitText = hasCommit ? str.Concat(Reply.Commits) : "";
		if (hasCommit) {
			LogWarn("Commit begin text=[" + commitText + "] len=" + commitText.Length
				+ " ctx=" + context.ToString("X")
				+ " comp=" + (hasComposition ? "有" : "無"));
		}
		// 步驟的決定是純函數（`TsfCompositionPlanner`），這裡只負責排隊與日誌。
		var steps = TsfCompositionPlanner.Plan(hasComposition, hasCommit, hasPreedit);
		// 這顆鍵的按鍵序號：佇列裏的每一步（含量測那一步）都帶著它，
		// 服務端靠它分辨「這條位置屬於哪一顆鍵」。排隊時就定下來，不等會話回來再讀。
		var Seq = LastKeySeq;
		foreach (var one in steps) {
			// insert 是唯一需要文字的一步；其餘傳空串。
			var text = one.Step == ETsfCompositionStep.Insert ? commitText : "";
			if (one.Step == ETsfCompositionStep.Start && !hasCommit) {
				LogWarn("組字 開始（引擎組字中） ctx=" + context.ToString("X"));
			}
			EnqueueCompositionJob(State, one.Step, text, one.Clear, Seq);
		}
		// 位置量測排在最後一步之後：同一個 context 上的編輯會話依序執行，
		// 所以量到的是「這一顆鍵的寫入都做完之後」的插入點（照 Weasel `EditSession.cpp:38`）：
		// 它在 `DoEditSession` 尾端才量，而不是另開一條與寫入無序的會話。
		if (Measure) {
			EnqueueCompositionJob(State, ETsfCompositionStep.Measure, "", false, Seq);
		}
	}

	[Doc($$"""
	組字佇列的上限；到頂就代表「會話排了卻沒被執行」，記錄下來，不讓它無界成長。
	""")]
	private const i32 MaxCompositionJobs = 32;

	[Doc($$"""
	把一步組字操作排進佇列，並替它排一次非同步編輯會話。
	這一步會把當下的 {{"TsfState.Context"}} **再持一份參考**放進工作項：
	會話是非同步的，回來時可能已經換了控件；照 Weasel 的做法（每個 `CEditSession`
	各持一份 `com_ptr<ITfContext>`），每一步固定作用在「排它時那個 context」。
	釋放由 {{nameof(DoEditSessionCore)}} 在執行完之後做。
	#Prm[{{nameof(State)}}][文字服務狀態。]
	#Prm[{{nameof(Step)}}][要做哪一步。]
	#Prm[{{nameof(Text)}}][insert 用的上屏文字。]
	#Prm[{{nameof(Clear)}}][End 用：結束前是否清空組字範圍。]
	#Prm[{{nameof(Seq)}}][這一步屬於哪一顆鍵（按鍵序號）；只有量測那一步用得到。]
	""")]
	private static void EnqueueCompositionJob(TsfState State, ETsfCompositionStep Step, str Text, bool Clear, i32 Seq) {
		var job = new CompositionJob { Step = Step, Text = Text, Clear = Clear, Context = State.Context, Seq = Seq };
		if (job.Context != IntPtr.Zero) {
			AddRefCom(job.Context);
		}
		lock (State.CompositionJobs) {
			if (State.CompositionJobs.Count >= MaxCompositionJobs) {
				LogError("組字佇列已滿（" + State.CompositionJobs.Count + "），丟棄 step=" + Step + " text=[" + Text + "]");
				ReleaseComIfAny(job.Context);
				return;
			}
			State.CompositionJobs.Add(job);
		}
		var hr = RequestCompositionSession(State, job.Context, Step);
		if (hr < 0) {
			// 排不進去就收回剛加的那一步：留著會讓下一個會話拿到錯的步驟，整段上屏都會亂。
			lock (State.CompositionJobs) {
				if (State.CompositionJobs.Count > 0) {
					State.CompositionJobs.RemoveAt(State.CompositionJobs.Count - 1);
				}
			}
			ReleaseComIfAny(job.Context);
			LogError("組字步驟排不進會話 step=" + Step + " hr=0x" + hr.ToString("X8") + " text=[" + Text + "]");
		}
	}

	[Doc($$"""
	釋放一個可能為 0 的 COM 介面指標。
	#Prm[{{nameof(ComObject)}}][介面指標；{{nameof(IntPtr.Zero)}} 時什麼都不做。]
	""")]
	private static void ReleaseComIfAny(IntPtr ComObject) {
		if (ComObject != IntPtr.Zero) {
			ReleaseCom(ComObject);
		}
	}

	[Doc($$"""
	替佇列裏的一步排一次非同步、可寫的編輯會話（與 Weasel 的旗標完全相同）。
	#Prm[{{nameof(State)}}][文字服務狀態。]
	#Prm[{{nameof(context)}}][按鍵所屬的 context。]
	#Prm[{{nameof(Step)}}][這一步的名稱，只用於日誌。]
	#Rtn[排入結果；小於 0 表示排不進去。]
	""")]
	private static i32 RequestCompositionSession(TsfState State, IntPtr context, ETsfCompositionStep Step) {
		var contextGen = ToContextGen(context, "排入編輯會話");
		if (contextGen is null) {
			return EFail;
		}
		// 旗標照 Weasel 0.15.0 分流：Start／End 同步、Insert 非同步（見 TfEsSync 的註釋）。
		var Sync = Step is ETsfCompositionStep.Start or ETsfCompositionStep.End;
		// 量測那一步只讀：它不寫文件，要寫入權限只會讓 TSF 更難排進來。
		var Access = Step is ETsfCompositionStep.Measure ? TfEsRead : TfEsReadWrite;
		var flags = (Sync ? TfEsSync : TfEsAsyncDontCare) | Access;
		var sessionResult = 0;
		var hr = contextGen.RequestEditSession(State.ClientId, State.EditSessionView, flags, out sessionResult);
		LogWarn("RequestEditSession step=" + Step
			+ " flags=0x" + flags.ToString("X")
			+ " hr=0x" + hr.ToString("X8")
			+ " phr=0x" + sessionResult.ToString("X8")
			+ (sessionResult == TfSAsync ? "（TF_S_ASYNC）" : ""));
		if (hr < 0) {
			return hr;
		}
		return sessionResult < 0 ? sessionResult : SOk;
	}

	[Doc($$"""
	換焦點或停用時放棄還沒結束的組字（照 Weasel `OnSetFocus(false)` → `_AbortComposition`）。
	只排一個「結束並清空」的會話，不直接呼叫 COM：這條路徑同樣在 TSF 的回調裏，
	直接叫 `EndComposition` 需要寫入 cookie，而我們手上沒有。
	會話排不進去時 `EnqueueCompositionJob` 會記 Error，組字就留在應用那邊——
	那會在日誌裏留下痕跡，不會變成一個沒有人知道的空組字。
	#Prm[{{nameof(State)}}][文字服務狀態。]
	""")]
	private static void AbandonComposition(TsfState State) {
		if (Volatile.Read(ref State.Composition) == IntPtr.Zero) {
			return;
		}
		LogWarn("離開焦點／停用：排入結束組字（清空） comp=" + State.Composition.ToString("X"));
		// 這一步不是任何按鍵引起的：序號沿用最後一顆鍵，只為了讓服務端看得懂來源。
		EnqueueCompositionJob(State, ETsfCompositionStep.End, "", true, LastKeySeq);
	}

	[Doc($$"""
	應用送來保留鍵的通知；本殼不用保留鍵，一律回「沒吃掉」。
	#Prm[{{nameof(ThisPtr)}}][ITfKeyEventSink 的介面指標。]
	#Prm[{{nameof(context)}}][按鍵所屬的 context（本殼不用）。]
	#Prm[{{nameof(PreservedKey)}}][被觸發的保留鍵 GUID；可能為 null。]
	#Prm[{{nameof(IsEaten)}}][回報是否吃掉。]
	""")]
	private static i32 OnPreservedKeyCore(IntPtr ThisPtr, IntPtr context, Guid* PreservedKey, i32* IsEaten) {
		// 判空在解引用之前：這是 COM 入口，指標由 TSF 給，但錯了就是宿主行程 AV。
		if (IsEaten is null) {
			return EInvalidArg;
		}
		*IsEaten = 0;
		if (PreservedKey is not null) {
			LogWarn("OnPreservedKey key=" + *PreservedKey + " pid=" + Environment.ProcessId);
		}
		return SOk;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct NativePoint {
		public i32 X;
		public i32 Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct GuiThreadInfo {
		public u32 Size;
		public u32 Flags;
		public IntPtr Active;
		public IntPtr Focus;
		public IntPtr Capture;
		public IntPtr MenuOwner;
		public IntPtr MoveSize;
		public IntPtr Caret;
		public NativeRect CaretRect;
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool GetGUIThreadInfo(u32 ThreadId, ref GuiThreadInfo Info);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool ClientToScreen(IntPtr Window, ref NativePoint Point);

	[DllImport("kernel32.dll")]
	private static extern u32 GetCurrentThreadId();

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern u32 GetWindowThreadProcessId(IntPtr Hwnd, out u32 ProcessId);

	[DllImport("user32.dll")]
	private static extern i16 GetKeyState(i32 VirtualKey);

	[Doc($$"""
	對一個 COM 介面指標呼叫 release（vtable slot 2）。
	""")]
	private static void ReleaseCom(IntPtr ComObject) {
		var vtbl = *(IntPtr***)ComObject;
		var release = (delegate* unmanaged<IntPtr, i32>)vtbl[2];
		release(ComObject);
	}

	[Doc($$"""
	對一個 COM 介面指標呼叫 AddRef（vtable slot 1）。
	""")]
	private static void AddRefCom(IntPtr ComObject) {
		var vtbl = *(IntPtr***)ComObject;
		var addRef = (delegate* unmanaged<IntPtr, i32>)vtbl[1];
		addRef(ComObject);
	}


	[Doc($$"""
	兜底位置離前景視窗左上角的位移（像素）；只是「別黏在螢幕角落」的意思。
	""")]
	private const i32 FallbackAnchorOffsetX = 24;

	[Doc($$"""
	兜底位置離前景視窗頂端的位移（像素）；避開標題列。
	""")]
	private const i32 FallbackAnchorOffsetY = 96;


	[Doc($$"""
	用 `ITfContextView::GetTextExt` 量出插入點矩形，並判斷這個結果能不能用。

	*量的是選取範圍，不是組字範圍*：照 Weasel 0.15.0 的 `CGetTextExtentEditSession`
	（`WeaselTSF/Composition.cpp:158` 的 `GetSelection`、`:161` 的
	`GetTextExt(ec, selection.range, ...)`）。組字期間的選取範圍是「佔位空格之後的插入點」
	（`CStartCompositionEditSession` 收在 `Collapse(TF_ANCHOR_END)`，`:60-62`），
	那才是候選窗該貼着的位置。
	*先前量的是「組字範圍 collapse 到 START」*（組字左緣），註釋標着「照 Weasel」卻不是它的行為：
	面板因此先出現在整段組字的左邊、等下一條位置再往右搬一次——那就是「每次往右跳一點」。
	#Prm[{{nameof(State)}}][文字服務狀態（採用與否會寫回插入點快取）。]
	#Prm[{{nameof(context)}}][按鍵所屬的 context（已 AddRef）。]
	#Prm[{{nameof(View)}}][該 context 的 active view（已 AddRef）。]
	#Prm[{{nameof(EditCookie)}}][session 的 cookie。]
	#Prm[{{nameof(Rect)}}][量到的矩形。]
	#Prm[{{nameof(Hr)}}][GetTextExt 的 HRESULT。]
	#Prm[{{nameof(Clipped)}}][控件是否回報「文字被裁切」。]
	#Rtn[true 表示這個矩形已被採用。]
	""")]
	private static bool MeasureCaret(TsfState State, ITfContextGen context, ITfContextViewGen View, u32 EditCookie, out NativeRect Rect, out i32 Hr, out i32 Clipped) {
		Rect = default;
		Clipped = 0;
		var selection = default(TfSelection);
		Hr = context.GetSelection(EditCookie, TfDefaultSelection, OneSelection, out selection, out var fetched);
		if (Hr < 0 || fetched != 1 || selection.Range == IntPtr.Zero) {
			// 拿不到選取範圍，後面就不可能量到座標。
			LogWarn("Caret GetSelection hr=0x" + Hr.ToString("X8") + " fetched=" + fetched + " range=" + selection.Range.ToString("X"));
			return false;
		}
		try {
			// 源生成式 COM：vtable 由生成器排，這裡不再手寫槽位索引（見 ITfContextViewGen）。
			// ConvertToManaged 會替這個介面指標建立托管包裝，包裝自帶一份參考；
			// 這一份由 GC 在包裝不可達時釋放，我們自己那份仍照舊在 finally 裏放。
			// 直接量選取範圍——不再把組字範圍 collapse 到 START 再量（見方法 Doc 的說明）。
			var clippedFlag = 0;
			Hr = View.GetTextExt(EditCookie, selection.Range, out var measured, out clippedFlag);
			Rect = measured;
			Clipped = clippedFlag;
			if (Hr < 0) {
				return false;
			}
			return TryAdoptCaretRect(State, measured);
		} finally {
			ReleaseCom(selection.Range);
		}
	}

	[Doc($$"""
	在組字編輯會話裏量一次插入點，並把位置送給服務端。

	*為何在這裏量*：量測必須發生在「這顆鍵的組字寫入生效之後」。Weasel 就是在
	`DoEditSession` 的尾端呼叫 `_UpdateCompositionWindow`（`EditSession.cpp:38`），
	而那條量測會話是 `TF_ES_SYNC | TF_ES_READ`（`Composition.cpp:203`）——同一個 cookie、
	嚴格有序。我們把量測當成佇列裏的一步（{{nameof(ETsfCompositionStep.Measure)}}），
	排在這一顆鍵所有寫入之後，於是同樣得到「寫完再量」的順序，
	而不必從按鍵路徑去要一條同步會話（那會在應用正持有會話時換來 `TF_E_SYNCHRONOUS`）。
	*先前是另開一條非同步會話量*：它與寫入沒有先後保證，日誌上同一顆鍵出現兩個 rect
	（`716` 與 `751`，差一個已上屏詞的寬度）就是這樣來的。

	*為何量不到仍要送*：內容是另一條訊息送來的，服務端收到位置才顯示；
	這一顆鍵若因為量不到而不送，就等於這一顆鍵沒有候選窗。
	#Prm[{{nameof(State)}}][文字服務狀態。]
	#Prm[{{nameof(Context)}}][這一步所屬的 context（工作項持有一份參考）。]
	#Prm[{{nameof(EditCookie)}}][這個編輯會話的 cookie。]
	#Prm[{{nameof(Seq)}}][觸發這次量測的按鍵序號；服務端靠它分辨遲到的位置屬於哪顆鍵。]
	""")]
	private static void MeasureAndSendCaret(TsfState State, IntPtr Context, u32 EditCookie, i32 Seq) {
		var contextGen = ToContextGen(Context, "量測插入點");
		if (contextGen is null) {
			return;
		}
		// 1: 取這個 context 的 active view；沒有 view 就沒有版面，也就沒有插入點。
		var viewHr = contextGen.GetActiveView(out var viewGen);
		if (viewHr < 0 || viewGen is null) {
			LogWarn("Caret GetActiveView hr=0x" + viewHr.ToString("X8") + " ctx=" + Context.ToString("X"));
			return;
		}
		// 2: 量一次並採用；成敗都記——呼叫失敗與「四邊全零（控件不報版面）」處置不同。
		var adopted = MeasureCaret(State, contextGen, viewGen, EditCookie, out var rect, out var hr, out var clipped);
		LogWarn("Caret src=sel seq=" + Seq + " hr=0x" + hr.ToString("X8")
			+ " rect=" + rect.Left + "," + rect.Top + "," + rect.Right + "," + rect.Bottom
			+ " clipped=" + clipped + " adopted=" + (adopted ? 1 : 0));
		// 3: 從未量到過才退回 Win32 的 caret；有過有效座標就寧可沿用舊值，
		//    否則浮窗會在「量到」與「退回」兩種來源之間跳。
		if (!adopted && !State.CaretValid) {
			TryTakeCaretFromGui(State);
		}
		// 4: 單向送出（不等待）：服務端收到位置才知道把面板擺哪；丟掉也無妨，下一顆鍵會再送。
		if (State.CaretValid) {
			SendCaretPosition(State.CaretPointX, State.CaretPointY, State.CaretPointTop, Seq);
		}
	}

	[Doc($$"""
	把量到的矩形換成螢幕座標錨點；能用就寫進狀態並立刻推給服務端。

	判斷本身在 {{nameof(TsfCaretPolicy)}}（純函數）；這裡只負責取前景視窗與 caret：
	+ 矩形全零 → 不用（控件沒有版面）；
	+ 矩形落在前景視窗外 → 當成客戶區座標，用「前景視窗原點 − 矩形原點 ＋ caret」平移
		（照 Weasel `Composition.cpp:186-206` 的 enhanced_position，不再像先前那樣直接丟掉）；
	+ 錨點取左下角（左邊、下緣）。
	採用後照 Weasel 的時機立刻送出位置：`_SetCompositionPosition` 一量到就
	`_cand->UpdateInputPosition(rc)`，候選窗因此緊貼光標而不是慢一顆鍵。
	""")]
	private static bool TryAdoptCaretRect(TsfState State, NativeRect Rect) {
		// 判斷本身是純函數（`TsfCaretPolicy`）；這裡只負責取前景視窗與 caret，
		// 並照 Weasel 的時機（`_SetCompositionPosition` → `UpdateInputPosition`）把位置推出去。
		var hasForeground = false;
		i32 fgLeft = 0, fgTop = 0, fgRight = 0, fgBottom = 0;
		var hwnd = GetForegroundWindow();
		if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var fg)) {
			hasForeground = true;
			fgLeft = fg.Left;
			fgTop = fg.Top;
			fgRight = fg.Right;
			fgBottom = fg.Bottom;
		}
		var caret = default(NativePoint);
		var hasCaret = GetCaretPos(ref caret);
		if (!TsfCaretPolicy.TryAdopt(Rect.Left, Rect.Top, Rect.Right, Rect.Bottom,
			fgLeft, fgTop, fgRight, fgBottom, hasForeground,
			hasCaret, caret.X, caret.Y, State.CaretValid, out var x, out var y, out var rectTop)) {
			// 沒有採用時**仍然要送位置**（沿用上一次的錨點）：內容是另一條訊息送來的，
			// 服務端在收到位置之前不顯示；這裡不送就等於這一顆鍵沒有候選窗。
			return false;
		}
		State.CaretPointX = x;
		State.CaretPointY = y;
		State.CaretPointTop = rectTop;
		State.CaretValid = true;
		return true;
	}

	[Doc($$"""
	點是否落在前景視窗的範圍（往外放宽一點，容許插入點貼在邊界上）。
	""")]
	private static bool IsNearForegroundWindow(i32 X, i32 Y) {
		var hwnd = GetForegroundWindow();
		if (hwnd == IntPtr.Zero) {
			return true;
		}
		if (!GetWindowRect(hwnd, out var rect)) {
			return true;
		}
		const i32 slack = 64;
		return X >= rect.Left - slack && X <= rect.Right + slack
			&& Y >= rect.Top - slack && Y <= rect.Bottom + slack;
	}

	[Doc($$"""
	用 Win32 的 GUI thread info 取得插入點，作為 GetTextExt 量不到時的兜底。
	*為何要看前景視窗的執行緒*：`GUITHREADINFO` 是「某個 GUI 執行緒」的狀態，
	而 TSF 的 OnTestKeyDown 不一定跑在應用建立視窗的那條執行緒上（Chromium 系就是另一條）。
	
	*為何要試兩種座標解釋*：`rcCaret` 依文檔是螢幕座標，但實務上有控件回報客戶區座標。
	這裡把兩種都算出來，取「落在前景視窗內」的那一個；兩個都合理時優先螢幕座標。
	先前版本一律再呼叫一次 `ClientToScreen`，對螢幕座標的來源就會偏掉一個客戶區原點。
	""")]
	private static void TryTakeCaretFromGui(TsfState State) {
		try {
			var foreground = GetForegroundWindow();
			var threadId = foreground != IntPtr.Zero
				? GetWindowThreadProcessId(foreground, out _)
				: GetCurrentThreadId();
			var info = new GuiThreadInfo { Size = (u32)Marshal.SizeOf<GuiThreadInfo>() };
			if (!GetGUIThreadInfo(threadId, ref info)) {
				return;
			}
			if (info.CaretRect.Right == 0 && info.CaretRect.Bottom == 0) {
				// 這個控件不回報 caret（Chromium 系就是這樣），維持原狀。
				return;
			}
			var hwnd = info.Caret != IntPtr.Zero
				? info.Caret
				: (info.Focus != IntPtr.Zero ? info.Focus : foreground);
			var asScreen = new NativePoint { X = info.CaretRect.Left, Y = info.CaretRect.Bottom };
			var asClient = asScreen;
			if (hwnd != IntPtr.Zero && ClientToScreen(hwnd, ref asClient)) {
				// 兩個候選裏挑一個落在前景視窗內的。
				var useClient = IsNearForegroundWindow(asClient.X, asClient.Y)
					&& !IsNearForegroundWindow(asScreen.X, asScreen.Y);
				if (useClient) {
					asScreen = asClient;
				}
			}
			State.CaretPointX = asScreen.X;
			State.CaretPointY = asScreen.Y;
			State.CaretValid = true;
			// 沒有量到矩形的上緣，用下緣當上緣（高度 0）。
			State.CaretPointTop = State.CaretPointY;
			LogWarn("Caret GUITHREADINFO adopted x=" + State.CaretPointX + " y=" + State.CaretPointY);
		} catch (Exception Exception) {
			LogError("TryTakeCaretFromGui EX " + Exception);
		}
	}

	[Doc($$"""
	送出按鍵請求時要附的插入點座標。
	座標有三級：量到的插入點 → 前景視窗左上角附近（從沒量到過時的兜底）→ (0,0)（未知）。
	(0,0) 由接收端解釋成「不知道位置」，候選窗因此不會被搬到螢幕原點。
	""")]
	private static void ResolveKeyAnchor(TsfState State, out i32 X, out i32 Y) {
		// 三級錨點（量到的 → 前景視窗左上角加位移 → (0,0)）是純函數，見 `TsfCaretPolicy`。
		var hasForeground = false;
		i32 fgLeft = 0, fgTop = 0;
		var hwnd = GetForegroundWindow();
		if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect)) {
			hasForeground = true;
			fgLeft = rect.Left;
			fgTop = rect.Top;
		}
		TsfCaretPolicy.ResolveAnchor(State.CaretValid, State.CaretPointX, State.CaretPointY,
			hasForeground, fgLeft, fgTop, FallbackAnchorOffsetX, FallbackAnchorOffsetY, out X, out Y);
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool GetWindowRect(IntPtr Hwnd, out NativeRect Rect);

	[Doc("取呼叫端執行緒目前 caret 的客戶區座標；Weasel 的 enhanced_position 用它修正座標系。")]
	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool GetCaretPos(ref NativePoint Point);

	[Doc($$"""
	把 context 指標換成托管介面；失敗時把原因寫進日誌並回 null。
	#Prm[{{nameof(context)}}][ITfContext 的原始指標。]
	#Prm[{{nameof(Where)}}][呼叫端名稱，只用於日誌。]
	#Rtn[托管介面；失敗為 null。]
	""")]
	private static ITfContextGen? ToContextGen(IntPtr context, str Where) {
		if (context == IntPtr.Zero) {
			LogError(Where + ": context 是 0");
			return null;
		}
		var gen = ComInterfaceMarshaller<ITfContextGen>.ConvertToManaged((void*)context);
		if (gen is null) {
			LogError(Where + ": context 轉換失敗 ctx=" + context.ToString("X"));
		}
		return gen;
	}

	[Doc($$"""
	執行佇列裏的一步組字操作。
	一個 edit session 物件、一個佇列、三種步驟：TSF 依排入順序叫回來，
	每次取走最前面那一步。這正是 Weasel 用三個不同的 `CEditSession` 子類別做的事，
	差別只在我們少了三次 `new`。
	""")]
	private static i32 DoEditSessionCore(IntPtr ThisPtr, u32 EditCookie) {
		try {
			var state = GetState(ThisPtr);
			if (state is null) {
				return EFail;
			}
			CompositionJob Job;
			i32 Remaining;
			lock (state.CompositionJobs) {
				if (state.CompositionJobs.Count == 0) {
					// 遲到的會話：排入時被收回、或應用重放了請求。記下來，不要再做任何事。
					LogWarn("DoEditSession 沒有待辦步驟（多餘或遲到的會話） ec=0x" + EditCookie.ToString("X"));
					return SOk;
				}
				Job = state.CompositionJobs[0];
				state.CompositionJobs.RemoveAt(0);
				Remaining = state.CompositionJobs.Count;
			}
			LogWarn("DoEditSession step=" + Job.Step
				+ " ec=0x" + EditCookie.ToString("X")
				+ " 剩餘=" + Remaining
				+ (Job.Text.Length > 0 ? " text=[" + Job.Text + "]" : ""));
			i32 Hr;
			// 每一步都作用在「排它時那個 context」（工作項自己持了一份參考），
			// 執行完（不論成敗）就把它還回去。
			try {
				switch (Job.Step) {
					case ETsfCompositionStep.Start:
						Hr = StartCompositionCore(state, EditCookie, Job.Context);
						break;
					case ETsfCompositionStep.Insert:
						Hr = InsertCommitTextCore(state, EditCookie, Job.Text, Job.Context);
						break;
					case ETsfCompositionStep.End:
						Hr = EndCompositionCore(state, EditCookie, Job.Clear);
						break;
					case ETsfCompositionStep.Measure:
						// 這一步不動文件：排在這一顆鍵的寫入之後，只為了「寫完再量」的順序。
						Hr = SOk;
						break;
					default:
						Hr = SOk;
						break;
				}
				// 量測在本會話內做完（照 Weasel `EditSession.cpp:38` 在 DoEditSession 尾端量）：
				// 同一個 cookie、同一個已存在的編輯會話，量到的必然是上面幾步寫入之後的插入點；
				// 另開一條會話量就會與寫入失去先後（2026-11-17 日誌：同一顆鍵 716 與 751）。
				if (Job.Step == ETsfCompositionStep.Measure) {
					MeasureAndSendCaret(state, Job.Context, EditCookie, Job.Seq);
				}
			} finally {
				ReleaseComIfAny(Job.Context);
			}
			LogWarn("DoEditSession done step=" + Job.Step + " hr=0x" + Hr.ToString("X8"));
			return Hr;
		} catch (Exception Exception) {
			LogError("DoEditSession EX " + Exception);
			return EFail;
		}
	}

	[Doc($$"""
	開始一次組字（照 Weasel `CStartCompositionEditSession::DoEditSession`，`Composition.cpp:26-70`）。
	步驟與 Weasel 一字不差：
	`ITfInsertAtSelection::InsertTextAtSelection(TF_IAS_QUERYONLY)` 取插入點範圍 →
	`ITfContextComposition::StartComposition(ec, 範圍, sink, &組字)` →
	在範圍裏寫一個**真空格**（`SetText(TF_ST_CORRECTION)`）→
	無條件 `Collapse(TF_ANCHOR_END)` → `SetSelection(ec, 1, &選取)`。（0.15.0 就是收在結尾，
	所以遊標落在空格之後；依 `inline_preedit` 分支的寫法是 0.16 之後才有的。）
	sink 是我們自己實作的 `ITfCompositionSink`（Weasel 傳的是 WeaselTSF 自己）。
	
	*為何一定要塞那個空格*：2026-11-15 23:20 的實測——只建空組字、立刻寫字、立刻結束，
	記事本正常、VSCode 照樣重複上屏。空的組字範圍在 Chromium 眼裏等於不存在；
	這也是 Weasel 在沒有 inline preedit 時仍然塞一個空格的原因。
	#Prm[{{nameof(State)}}][文字服務狀態。]
	#Prm[{{nameof(EditCookie)}}][這次編輯會話的 cookie。]
	#Prm[{{nameof(context)}}][排這一步時所屬的 context（工作項持有一份參考）。]
	#Rtn[HRESULT。]
	""")]
	private static i32 StartCompositionCore(TsfState State, u32 EditCookie, IntPtr context) {
		if (Volatile.Read(ref State.Composition) != IntPtr.Zero) {
			LogWarn("組字已存在，略過開始 comp=" + State.Composition.ToString("X"));
			return SOk;
		}
		if (context == IntPtr.Zero) {
			LogError("組字: 這一步沒有 context（排入時就沒有），無法開始組字");
			return EFail;
		}
		var insertIid = IidInsertAtSelection;
		var qhr = Marshal.QueryInterface(context, in insertIid, out var insertPtr);
		Log("組字 QI(ITfInsertAtSelection) hr=0x" + qhr.ToString("X8") + " ptr=" + insertPtr.ToString("X"));
		if (qhr < 0 || insertPtr == IntPtr.Zero) {
			return qhr < 0 ? qhr : EFail;
		}
		try {
			var insert = ComInterfaceMarshaller<ITfInsertAtSelectionGen>.ConvertToManaged((void*)insertPtr);
			if (insert is null) {
				LogError("組字: ITfInsertAtSelection 轉換失敗");
				return EFail;
			}
			var rangeHr = insert.InsertTextAtSelection(EditCookie, TfIasQueryOnly, IntPtr.Zero, 0, out var insertRange);
			LogWarn("組字 InsertTextAtSelection hr=0x" + rangeHr.ToString("X8") + " range=" + insertRange.ToString("X"));
			if (rangeHr < 0 || insertRange == IntPtr.Zero) {
				return rangeHr < 0 ? rangeHr : EFail;
			}
			try {
				var compIid = IidContextComposition;
				var chr = Marshal.QueryInterface(context, in compIid, out var compPtr);
				Log("組字 QI(ITfContextComposition) hr=0x" + chr.ToString("X8") + " ptr=" + compPtr.ToString("X"));
				if (chr < 0 || compPtr == IntPtr.Zero) {
					return chr < 0 ? chr : EFail;
				}
				try {
					var comp = ComInterfaceMarshaller<ITfContextCompositionGen>.ConvertToManaged((void*)compPtr);
					if (comp is null) {
						LogError("組字: ITfContextComposition 轉換失敗");
						return EFail;
					}
					var startHr = comp.StartComposition(EditCookie, insertRange, State.CompositionSinkView, out var compositionPtr);
					LogWarn("組字 StartComposition hr=0x" + startHr.ToString("X8")
						+ " comp=" + compositionPtr.ToString("X")
						+ " sink=" + State.CompositionSinkView.ToString("X"));
					if (startHr < 0 || compositionPtr == IntPtr.Zero) {
						return startHr < 0 ? startHr : EFail;
					}
					var old = Interlocked.Exchange(ref State.Composition, compositionPtr);
					if (old != IntPtr.Zero) {
						Marshal.Release(old);
					}
					LogWarn("組字 已建立 comp=" + compositionPtr.ToString("X"));
					// 照 Weasel 0.15.0：先塞真空格讓範圍非空，再把選取收在結尾（遊標在空格之後）。
					var range = ComInterfaceMarshaller<ITfRangeGen>.ConvertToManaged((void*)insertRange);
					if (range is not null) {
						i32 PlaceholderHr;
						fixed (char* Placeholder = CompositionPlaceholder) {
							PlaceholderHr = range.SetText(EditCookie, TfStCorrection, (IntPtr)Placeholder, CompositionPlaceholder.Length);
						}
						LogWarn("組字 塞佔位空格 hr=0x" + PlaceholderHr.ToString("X8"));
						// 照 Weasel 0.15.0 `CStartCompositionEditSession::DoEditSession`：收在**結尾**，
						// 遊標因此在空格之後（空格在遊標左邊）。
						var collapseHr = range.Collapse(EditCookie, TfAnchorEnd);
						LogWarn("組字 Collapse(END) hr=0x" + collapseHr.ToString("X8"));
						var selection = new TfSelection { Range = insertRange, ActiveEnd = 0, Interim = 0 };
						var contextGen = ToContextGen(context, "組字 SetSelection");
						if (contextGen is not null) {
							var selectHr = contextGen.SetSelection(EditCookie, OneSelection, (IntPtr)(&selection));
							LogWarn("組字 SetSelection hr=0x" + selectHr.ToString("X8"));
						}
					} else {
						LogError("組字: ITfRange 轉換失敗，佔位空格沒塞（應用可能不認這次組字）");
					}
					return SOk;
				} finally {
					Marshal.Release(compPtr);
				}
			} finally {
				Marshal.Release(insertRange);
			}
		} finally {
			Marshal.Release(insertPtr);
		}
	}

	[Doc($$"""
	把上屏文字寫進組字範圍（照 Weasel `CInsertTextEditSession::DoEditSession`，`Composition.cpp:334-360`）。
	Weasel 的順序照抄：`GetRange` → `SetText(ec, 0, 文字)` → `Collapse(TF_ANCHOR_END)` →
	`SetSelection(ec, 1, &選取)`。少了 `SetSelection`，Chromium 會以為游標還在原處。
	
	*沒有組字時走退回路徑*：開始組字失敗（應用不支援）就退回
	「取選取範圍 → SetText → Collapse → SetSelection」，至少讓字進得去。
	日誌會明說走的是哪一條，不讓「照 Weasel」變成一句空話。
	#Prm[{{nameof(State)}}][文字服務狀態。]
	#Prm[{{nameof(EditCookie)}}][這次編輯會話的 cookie。]
	#Prm[{{nameof(Text)}}][要上屏的文字。]
	#Prm[{{nameof(context)}}][排這一步時所屬的 context（工作項持有一份參考）。]
	#Rtn[HRESULT。]
	""")]
	private static i32 InsertCommitTextCore(TsfState State, u32 EditCookie, str Text, IntPtr context) {
		if (Text.Length == 0) {
			LogWarn("上屏步驟沒有文字，略過");
			return SOk;
		}
		var contextGen = ToContextGen(context, "上屏");
		if (contextGen is null) {
			return EFail;
		}
		var compPtr = Volatile.Read(ref State.Composition);
		if (compPtr == IntPtr.Zero) {
			LogWarn("沒有組字（開始步驟失敗或應用已終止），退回選取範圍上屏 text=[" + Text + "]");
			return InsertBySelectionCore(contextGen, EditCookie, Text);
		}
		var comp = ComInterfaceMarshaller<ITfCompositionGen>.ConvertToManaged((void*)compPtr);
		if (comp is null) {
			LogError("上屏: ITfComposition 轉換失敗 comp=" + compPtr.ToString("X"));
			return InsertBySelectionCore(contextGen, EditCookie, Text);
		}
		var getHr = comp.GetRange(out var rangePtr);
		Log("上屏 Composition.GetRange hr=0x" + getHr.ToString("X8") + " range=" + rangePtr.ToString("X"));
		if (getHr < 0 || rangePtr == IntPtr.Zero) {
			return getHr < 0 ? getHr : EFail;
		}
		try {
			var range = ComInterfaceMarshaller<ITfRangeGen>.ConvertToManaged((void*)rangePtr);
			if (range is null) {
				LogError("上屏: ITfRange 轉換失敗");
				return EFail;
			}
			i32 Hr;
			fixed (char* TextPtr = Text) {
				Hr = range.SetText(EditCookie, 0, (IntPtr)TextPtr, Text.Length);
			}
			LogWarn("上屏 組字 SetText hr=0x" + Hr.ToString("X8") + " len=" + Text.Length + " text=[" + Text + "]");
			if (Hr < 0) {
				return Hr;
			}
			var collapseHr = range.Collapse(EditCookie, TfAnchorEnd);
			Log("上屏 組字 Collapse hr=0x" + collapseHr.ToString("X8"));
			var selection = new TfSelection { Range = rangePtr, ActiveEnd = 0, Interim = 0 };
			var selectHr = contextGen.SetSelection(EditCookie, OneSelection, (IntPtr)(&selection));
			if (selectHr < 0) {
				LogError("上屏 組字 SetSelection hr=0x" + selectHr.ToString("X8"));
			} else {
				Log("上屏 組字 SetSelection hr=0x" + selectHr.ToString("X8"));
			}
			return Hr;
		} finally {
			Marshal.Release(rangePtr);
		}
	}

	[Doc($$"""
	退回路徑：取目前的選取範圍、把文字寫進去、收成插入點、設定選取。
	這條路 Chromium 會再插一次（見本檔類註釋），所以只要日誌裏出現「退回」兩個字，
	就代表那個應用上屏會重複——不是猜測，是這條路的已知後果。
	#Prm[{{nameof(ContextGen)}}][目前的 context。]
	#Prm[{{nameof(EditCookie)}}][這次編輯會話的 cookie。]
	#Prm[{{nameof(Text)}}][要上屏的文字。]
	#Rtn[HRESULT。]
	""")]
	private static i32 InsertBySelectionCore(ITfContextGen ContextGen, u32 EditCookie, str Text) {
		var selection = default(TfSelection);
		var hr = ContextGen.GetSelection(EditCookie, TfDefaultSelection, OneSelection, out selection, out var fetched);
		LogWarn("上屏(退回) GetSelection hr=0x" + hr.ToString("X8") + " fetched=" + fetched + " text=[" + Text + "]");
		if (hr < 0) {
			return hr;
		}
		if (fetched != 1 || selection.Range == IntPtr.Zero) {
			LogError("上屏(退回): 取不到選取範圍");
			return EFail;
		}
		try {
			var range = ComInterfaceMarshaller<ITfRangeGen>.ConvertToManaged((void*)selection.Range);
			if (range is null) {
				LogError("上屏(退回): ITfRange 轉換失敗");
				return EFail;
			}
			i32 SetHr;
			fixed (char* TextPtr = Text) {
				SetHr = range.SetText(EditCookie, 0, (IntPtr)TextPtr, Text.Length);
			}
			LogWarn("上屏(退回) SetText hr=0x" + SetHr.ToString("X8") + " len=" + Text.Length + " text=[" + Text + "]");
			if (SetHr < 0) {
				return SetHr;
			}
			var collapseHr = range.Collapse(EditCookie, TfAnchorEnd);
			Log("上屏(退回) Collapse hr=0x" + collapseHr.ToString("X8"));
			var select = new TfSelection { Range = selection.Range, ActiveEnd = 0, Interim = 0 };
			var selectHr = ContextGen.SetSelection(EditCookie, OneSelection, (IntPtr)(&select));
			if (selectHr < 0) {
				LogError("上屏(退回) SetSelection hr=0x" + selectHr.ToString("X8"));
			} else {
				Log("上屏(退回) SetSelection hr=0x" + selectHr.ToString("X8"));
			}
			return SetHr;
		} finally {
			Marshal.Release(selection.Range);
		}
	}

	[Doc($$"""
	結束組字（照 Weasel `CEndCompositionEditSession::DoEditSession`，`Composition.cpp:100-120`）。
	#Prm[{{nameof(State)}}][文字服務狀態。]
	#Prm[{{nameof(EditCookie)}}][這次編輯會話的 cookie。]
	#Prm[{{nameof(Clear)}}][true 表示結束前把組字範圍清空（取消組字用）；上屏時傳 false，保留剛寫進去的字。]
	#Rtn[HRESULT。]
	""")]
	private static i32 EndCompositionCore(TsfState State, u32 EditCookie, bool Clear) {
		var compPtr = Interlocked.Exchange(ref State.Composition, IntPtr.Zero);
		if (compPtr == IntPtr.Zero) {
			LogWarn("結束組字：沒有組字（可能已被應用終止） clear=" + (Clear ? 1 : 0));
			return SOk;
		}
		try {
			var comp = ComInterfaceMarshaller<ITfCompositionGen>.ConvertToManaged((void*)compPtr);
			if (comp is null) {
				LogError("結束組字: ITfComposition 轉換失敗 comp=" + compPtr.ToString("X"));
				return EFail;
			}
			if (Clear) {
				var getHr = comp.GetRange(out var rangePtr);
				if (getHr >= 0 && rangePtr != IntPtr.Zero) {
					try {
						var range = ComInterfaceMarshaller<ITfRangeGen>.ConvertToManaged((void*)rangePtr);
						if (range is not null) {
							var clearHr = range.SetText(EditCookie, 0, IntPtr.Zero, 0);
							Log("結束組字 清空範圍 hr=0x" + clearHr.ToString("X8"));
						}
					} finally {
						Marshal.Release(rangePtr);
					}
				}
			}
			var hr = comp.EndComposition(EditCookie);
			LogWarn("結束組字 EndComposition hr=0x" + hr.ToString("X8") + " clear=" + (Clear ? 1 : 0));
			return hr;
		} finally {
			Marshal.Release(compPtr);
		}
	}



	[Doc($$"""
	編輯會話所屬的物件查詢；只支援 IUnknown 與 ITfEditSession。
	""")]
	private static i32 EditSessionQueryCore(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
		var state = GetState(ThisPtr);
		if (state is null) {
			return ENoInterface;
		}
		if (*Riid == IidIUnknown || *Riid == IidEditSession) {
			*Ppv = ThisPtr;
			Interlocked.Increment(ref state.RefCount);
			return SOk;
		}
		*Ppv = IntPtr.Zero;
		return ENoInterface;
	}

	[Doc($$"""
	ITfCompositionSink 的 QueryInterface；只支援 IUnknown 與自己。
	""")]
	private static i32 CompositionSinkQueryCore(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
		var state = GetState(ThisPtr);
		if (state is null) {
			return ENoInterface;
		}
		if (*Riid == IidIUnknown || *Riid == IidCompositionSink) {
			*Ppv = ThisPtr;
			Interlocked.Increment(ref state.RefCount);
			return SOk;
		}
		*Ppv = IntPtr.Zero;
		return ENoInterface;
	}

	[Doc($$"""
	應用主動結束組字時的通知（Weasel `WeaselTSF::OnCompositionTerminated`，`Composition.cpp:389-398`）。
	Weasel 的註釋寫得很白：組字字串為空時，連「正常結束」也會走進來，所以不能假設這是異常。
	這裡只做「放掉手上的組字」，不主動再叫 `EndComposition`——那個呼叫很可能就是觸發通知的人，
	重入只會在別人的會話裏再排一次會話。日誌留一行，之後才看得出是「我們結束的」還是「應用結束的」。
	#Prm[{{nameof(ThisPtr)}}][ITfCompositionSink 的介面指標。]
	#Prm[{{nameof(EditCookie)}}][應用給的寫入 cookie。]
	#Prm[{{nameof(Composition)}}][被結束的組字。]
	#Rtn[一律 S_OK。]
	""")]
	private static i32 OnCompositionTerminatedCore(IntPtr ThisPtr, u32 EditCookie, IntPtr Composition) {
		try {
			var state = GetState(ThisPtr);
			if (state is null) {
				return SOk;
			}
			var owned = Interlocked.Exchange(ref state.Composition, IntPtr.Zero);
			if (owned != IntPtr.Zero) {
				Marshal.Release(owned);
			}
			LogWarn("OnCompositionTerminated ec=0x" + EditCookie.ToString("X")
				+ " comp=" + Composition.ToString("X")
				+ " 我們持有的那份=" + (owned != IntPtr.Zero ? "已釋放" : "早就沒了"));
			return SOk;
		} catch (Exception Exception) {
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
	private static extern bool SetProcessValidCallTargets(IntPtr Process, IntPtr VirtualAddress, UIntPtr RegionSize, u32 NumberOfOffsets, [In] CfgCallTargetInfo[] OffsetInformation);

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetCurrentProcess();

	[Doc($$"""
	把我們 vtable 裡的函式位址註冊成 CFG 合法間接呼叫目標。
	TSF（msctf.dll）是 CFG 保護的，它間接呼叫我們的 sink 時若目標不在 bitmap 內，
	會直接 fail-fast，連我們的函式都不會進去；NativeAOT 產生的 DLL 沒有 guard CF function table，
	所以必須自己註冊。
	""")]
	private static void CollectTargets(IntPtr* Table, i32 count, HashSet<IntPtr> Targets) {
		for (var index = 0; index < count; index++) {
			var slot = Table[index];
			if (slot != IntPtr.Zero && !Targets.Contains(slot)) {
				Targets.Add(slot);
			}
		}
	}

	private static void RegisterCfgTargets() {
		try {
			var targets = new HashSet<IntPtr>();
			CollectTargets(TipVtbl, 5, targets);
			CollectTargets(SinkVtbl, 9, targets);
			CollectTargets(FactoryVtbl, 5, targets);
			CollectTargets(EditSessionVtbl, 4, targets);
			CollectTargets(CompositionSinkVtbl, 4, targets);
			foreach (var target in targets) {
				var page = (IntPtr)(target.ToInt64() & ~0xFFFL);
				var info = new[] { new CfgCallTargetInfo { Offset = (IntPtr)(target.ToInt64() - page.ToInt64()), Flags = (IntPtr)1 } };
				var ok = SetProcessValidCallTargets(GetCurrentProcess(), page, (UIntPtr)0x1000, 1, info);
				Log("CFG register " + target.ToString("X") + " ok=" + ok);
			}
		} catch (Exception Exception) {
			Log("CFG register EX " + Exception);
		}
	}

	[DllImport("kernel32.dll")]
	private static extern IntPtr AddVectoredExceptionHandler(u32 First, IntPtr Handler);

	[UnmanagedCallersOnly]
	private static i32 VectoredExceptionHandler(IntPtr ExceptionInfo) {
		try {
			// EXCEPTION_RECORD: code(+0) Flags(+4) Next(+8) address(+16) NumberParameters(+24) Information(+32)
			var info = (IntPtr*)ExceptionInfo;
			var record = (IntPtr*)info[0];
			var code = (u32)record[0].ToInt64();
			// C++（0xE06D7363）與 .NET（0xE0434352）的例外在 Chromium 裏是常態：
			// 每一個 throw 都會走到這裏，而我們在這裏寫檔。只記真正的 SEH 錯誤，
			// 免得在別人的例外派發路徑上做幾十次檔案 IO，還把日誌灌滿。
			if (code == 0xE06D7363 || code == 0xE0434352) {
				return 0;
			}
			var address = record[2];
			var paramCount = (u32)(record[3].ToInt64() & 0xFFFFFFFF);
			var info0 = record[4];
			var info1 = record[5];
			Log("VEH code=0x" + code.ToString("X8") + " addr=" + address.ToString("X") + " n=" + paramCount + " info0=" + info0.ToString("X") + " info1=" + info1.ToString("X"));
		} catch {
		}
		return 0;
	}

	[UnmanagedCallersOnly]
	private static i32 EditSessionQueryEntry(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
		return EditSessionQueryCore(ThisPtr, Riid, Ppv);
	}

	[UnmanagedCallersOnly]
	private static i32 DoEditSessionEntry(IntPtr ThisPtr, u32 EditCookie) {
		return DoEditSessionCore(ThisPtr, EditCookie);
	}

	[UnmanagedCallersOnly]
	private static i32 CompositionSinkQueryEntry(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
		return CompositionSinkQueryCore(ThisPtr, Riid, Ppv);
	}

	[UnmanagedCallersOnly]
	private static i32 OnCompositionTerminatedEntry(IntPtr ThisPtr, u32 EditCookie, IntPtr Composition) {
		return OnCompositionTerminatedCore(ThisPtr, EditCookie, Composition);
	}

	[UnmanagedCallersOnly]
	private static i32 QueryInterfaceEntry(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) { Log("QI-entry this=" + ThisPtr.ToString("X")); return QueryCore(ThisPtr, Riid, Ppv); }

	[UnmanagedCallersOnly]
	private static i32 AddRefEntry(IntPtr ThisPtr) { Log("AddRef-entry this=" + ThisPtr.ToString("X")); return AddRefCore(ThisPtr); }

	[UnmanagedCallersOnly]
	private static i32 ReleaseEntry(IntPtr ThisPtr) { Log("release-entry this=" + ThisPtr.ToString("X")); return ReleaseCore(ThisPtr); }

	[UnmanagedCallersOnly]
	private static i32 ActivateEntry(IntPtr ThisPtr, IntPtr ThreadManager, u32 ClientId) {
		return ActivateCore(ThisPtr, ThreadManager, ClientId);
	}

	[UnmanagedCallersOnly]
	private static i32 DeactivateEntry(IntPtr ThisPtr) {
		return DeactivateCore(ThisPtr);
	}

	[UnmanagedCallersOnly]
	private static i32 OnSetFocusEntry(IntPtr ThisPtr, i32 IsForeground) { Log("OnSetFocus-entry this=" + ThisPtr.ToString("X")); return OnSetFocusCore(ThisPtr, IsForeground); }

	[UnmanagedCallersOnly]
	private static i32 OnTestKeyDownEntry(IntPtr ThisPtr, IntPtr context, IntPtr WParam, IntPtr LParam, i32* IsEaten) {
		return OnTestKeyDownCore(ThisPtr, context, WParam, LParam, IsEaten);
	}

	[UnmanagedCallersOnly]
	private static i32 OnTestKeyUpEntry(IntPtr ThisPtr, IntPtr context, IntPtr WParam, IntPtr LParam, i32* IsEaten) {
		return OnTestKeyUpCore(ThisPtr, context, WParam, LParam, IsEaten);
	}

	[UnmanagedCallersOnly]
	private static i32 OnKeyDownEntry(IntPtr ThisPtr, IntPtr context, IntPtr WParam, IntPtr LParam, i32* IsEaten) {
		return OnKeyDownCore(ThisPtr, context, WParam, LParam, IsEaten);
	}

	[UnmanagedCallersOnly]
	private static i32 OnKeyUpEntry(IntPtr ThisPtr, IntPtr context, IntPtr WParam, IntPtr LParam, i32* IsEaten) {
		return OnKeyUpCore(ThisPtr, context, WParam, LParam, IsEaten);
	}

	[UnmanagedCallersOnly]
	private static i32 OnPreservedKeyEntry(IntPtr ThisPtr, IntPtr context, Guid* PreservedKey, i32* IsEaten) {
		return OnPreservedKeyCore(ThisPtr, context, PreservedKey, IsEaten);
	}

	[UnmanagedCallersOnly]
	private static i32 FactoryQueryInterfaceEntry(IntPtr ThisPtr, Guid* Riid, IntPtr* Ppv) {
		return FactoryQueryCore(ThisPtr, Riid, Ppv);
	}

	[UnmanagedCallersOnly]
	private static i32 FactoryAddRefEntry(IntPtr ThisPtr) {
		return Interlocked.Increment(ref FactoryRefCount);
	}

	[UnmanagedCallersOnly]
	private static i32 FactoryReleaseEntry(IntPtr ThisPtr) {
		return Interlocked.Decrement(ref FactoryRefCount);
	}

	[UnmanagedCallersOnly]
	private static i32 CreateInstanceEntry(IntPtr ThisPtr, IntPtr Outer, Guid* Riid, IntPtr* Ppv) {
		return CreateInstanceCore(ThisPtr, Outer, Riid, Ppv);
	}

	[UnmanagedCallersOnly]
	private static i32 LockServerEntry(IntPtr ThisPtr, i32 Lock) {
		return SOk;
	}
}
