namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

// 這個檔案是「外殼呼叫 TSF」那一半的生成式宣告。
// 槽位順序由宣告順序決定，因此每一段宣告都必須與 msctf.h 的順序逐字對齊，
// 也不能省略中間用不到的方法；省略或亂序就是把槽位算錯，2026-11-15 的 notepad 崩潰就是這樣來的。
// 驗證方式：scripts/check-msctf-order 會重新解析 msctf.h 並比對本檔的宣告順序。

/// <summary>`ITfContext` 的前 10 個方法；本殼只用到 3、5、9。</summary>
/// <remarks>
/// 3 RequestEditSession、4 InWriteSession、5 GetSelection、6 SetSelection、
/// 7 GetStart、8 GetEnd、9 GetActiveView。
/// 6 是 SetSelection，少算它一格就會把 9 的 GetActiveView 指到 8 的 GetEnd。
/// </remarks>
[GeneratedComInterface]
[Guid("AA80E7FD-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfContextGen {
	/// <summary>排入一次編輯會話；`Pes` 是手寫的 ITfEditSession 介面指標。</summary>
	[PreserveSig] int RequestEditSession(uint Tid, IntPtr Pes, uint DwFlags, out int PhrSession);
	/// <summary>查詢目前是否在寫入會話內。</summary>
	[PreserveSig] int InWriteSession(uint Tid, out int PfWriteSession);
	/// <summary>取出選取範圍。</summary>
	[PreserveSig] int GetSelection(uint Ec, uint UlIndex, uint UlCount, out NativeTsf.TfSelection PSelection, out uint PcFetched);
	/// <summary>設定選取範圍；本殼不用，只為對齊槽位。</summary>
	[PreserveSig] int SetSelection(uint Ec, uint UlCount, IntPtr PSelection);
	/// <summary>文件起點；本殼不用，只為對齊槽位。</summary>
	[PreserveSig] int GetStart(uint Ec, out IntPtr PpStart);
	/// <summary>文件終點；本殼不用，只為對齊槽位。</summary>
	[PreserveSig] int GetEnd(uint Ec, out IntPtr PpEnd);
	/// <summary>取得使用中的檢視；量插入點要用它。</summary>
	[PreserveSig] int GetActiveView(out ITfContextViewGen PpView);
}

/// <summary>`ITfRange` 的前 16 個方法；本殼只用到 4 與 15。</summary>
/// <remarks>
/// 3 GetText、4 SetText、5 GetFormattedText、6 GetEmbedded、7 InsertEmbedded、
/// 8 ShiftStart、9 ShiftEnd、10 ShiftStartToRange、11 ShiftEndToRange、
/// 12 ShiftStartRegion、13 ShiftEndRegion、14 IsEmpty、15 Collapse。
/// </remarks>
[GeneratedComInterface]
[Guid("AA80E7FF-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfRangeGen {
	/// <summary>讀取範圍內的文字。</summary>
	[PreserveSig] int GetText(uint Ec, uint DwFlags, IntPtr PchText, uint CchMax, out uint Pcch);
	/// <summary>把文字寫進範圍；上屏用它。</summary>
	[PreserveSig] int SetText(uint Ec, uint DwFlags, IntPtr PchText, int Cch);
	/// <summary>取出格式化的資料物件；本殼不用。</summary>
	[PreserveSig] int GetFormattedText(uint Ec, out IntPtr PpDataObject);
	/// <summary>取出內嵌物件；本殼不用。</summary>
	[PreserveSig] int GetEmbedded(uint Ec, IntPtr RguidService, IntPtr Riid, out IntPtr Ppunk);
	/// <summary>插入內嵌物件；本殼不用。</summary>
	[PreserveSig] int InsertEmbedded(uint Ec, uint DwFlags, IntPtr PDataObject);
	/// <summary>移動起點；本殼不用。</summary>
	[PreserveSig] int ShiftStart(uint Ec, int CchReq, out int Pcch, IntPtr PHalt);
	/// <summary>移動終點；本殼不用。</summary>
	[PreserveSig] int ShiftEnd(uint Ec, int CchReq, out int Pcch, IntPtr PHalt);
	/// <summary>把起點移到另一個範圍；本殼不用。</summary>
	[PreserveSig] int ShiftStartToRange(uint Ec, IntPtr PRange, uint APos);
	/// <summary>把終點移到另一個範圍；本殼不用。</summary>
	[PreserveSig] int ShiftEndToRange(uint Ec, IntPtr PRange, uint APos);
	/// <summary>把起點移出區域；本殼不用。</summary>
	[PreserveSig] int ShiftStartRegion(uint Ec, uint Dir, out int PfNoRegion);
	/// <summary>把終點移出區域；本殼不用。</summary>
	[PreserveSig] int ShiftEndRegion(uint Ec, uint Dir, out int PfNoRegion);
	/// <summary>判斷範圍是否為空；本殼不用。</summary>
	[PreserveSig] int IsEmpty(uint Ec, out int PfEmpty);
	/// <summary>把範圍收成一個插入點；上屏後把游標移到文字之後用它。</summary>
	[PreserveSig] int Collapse(uint Ec, uint APos);
}

/// <summary>`ITfKeystrokeMgr` 的前 5 個方法；本殼只用到 3 與 4。</summary>
/// <remarks>
/// 3 AdviseKeyEventSink、4 UnadviseKeyEventSink。
/// `PSink` 仍以 IntPtr 傳入：那是手寫的 sink 介面指標，這一階段不改成托管物件。
/// </remarks>
[GeneratedComInterface]
[Guid("AA80E7F0-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfKeystrokeMgrGen {
	/// <summary>掛上按鍵事件接收器。</summary>
	[PreserveSig] int AdviseKeyEventSink(uint Tid, IntPtr PSink, int FForeground);
	/// <summary>卸下按鍵事件接收器。</summary>
	[PreserveSig] int UnadviseKeyEventSink(uint Tid);
	/// <summary>查詢前景輸入法；本殼不用。</summary>
	[PreserveSig] int GetForeground(out Guid Pclsid);
	/// <summary>徵詢按鍵；本殼不用。</summary>
	[PreserveSig] int TestKeyDown(IntPtr WParam, IntPtr LParam, out int PfEaten);
	/// <summary>徵詢擡起；本殼不用。</summary>
	[PreserveSig] int TestKeyUp(IntPtr WParam, IntPtr LParam, out int PfEaten);
}

/// <summary>`ITfInsertAtSelection` 的前 4 個方法；本殼只用到 3。</summary>
/// <remarks>
/// 3 InsertTextAtSelection(ec, dwFlags, pchText, cch, ppRange)、
/// 4 InsertEmbeddedAtSelection(...)。
/// 用 `TF_IAS_QUERYONLY` 呼叫時不插入文字，只取回「插入點所在的範圍」，
/// 這正是 Weasel 建立組字範圍的起點（`WeaselTSF/Composition.cpp:34`）。
/// </remarks>
[GeneratedComInterface]
[Guid("55CE16BA-3014-41C1-9CEB-FADE1446AC6C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfInsertAtSelectionGen {
	/// <summary>在插入點插入文字，或只取回插入點的範圍。</summary>
	[PreserveSig] int InsertTextAtSelection(uint Ec, uint DwFlags, IntPtr PchText, int Cch, out IntPtr PpRange);
	/// <summary>在插入點插入內嵌物件；本殼不用。</summary>
	[PreserveSig] int InsertEmbeddedAtSelection(uint Ec, uint DwFlags, IntPtr PDataObject, out IntPtr PpRange);
}

/// <summary>`ITfContextComposition` 的前 4 個方法；本殼只用到 3。</summary>
/// <remarks>
/// 3 StartComposition(ecWrite, pCompositionRange, pSink, ppComposition)、
/// 4 EnumCompositions、5 FindComposition、6 TakeOwnership。
/// 官方標頭檔在這之後還有 EndComposition；本殼用 `ITfComposition::EndComposition` 結束，因此不必宣告它。
/// </remarks>
[GeneratedComInterface]
[Guid("D40C8AAE-AC92-4FC7-9A11-0EE0E23AA39B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfContextCompositionGen {
	/// <summary>在指定範圍上開始一次組字。</summary>
	[PreserveSig] int StartComposition(uint EcWrite, IntPtr PCompositionRange, IntPtr PSink, out IntPtr PpComposition);
	/// <summary>列舉組字；本殼不用。</summary>
	[PreserveSig] int EnumCompositions(out IntPtr PpEnum);
	/// <summary>找出範圍所在的組字；本殼不用。</summary>
	[PreserveSig] int FindComposition(uint EcRead, IntPtr PTestRange, out IntPtr PpEnum);
	/// <summary>接管既有的組字；本殼不用。</summary>
	[PreserveSig] int TakeOwnership(uint EcWrite, IntPtr PComposition, IntPtr PSink, out IntPtr PpComposition);
}

/// <summary>`ITfComposition` 的 4 個方法。</summary>
/// <remarks>
/// 3 GetRange、4 ShiftStart、5 ShiftEnd、6 EndComposition。
/// 上屏流程是：取組字範圍 → 寫入最終文字 → 收成插入點 → 結束組字。
/// </remarks>
[GeneratedComInterface]
[Guid("20168D64-5A8F-4A5A-B7BD-CFA29F4D0FD9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfCompositionGen {
	/// <summary>取得組字的範圍。</summary>
	[PreserveSig] int GetRange(out IntPtr PpRange);
	/// <summary>移動組字起點；本殼不用。</summary>
	[PreserveSig] int ShiftStart(uint EcWrite, IntPtr PNewStart);
	/// <summary>移動組字終點；本殼不用。</summary>
	[PreserveSig] int ShiftEnd(uint EcWrite, IntPtr PNewEnd);
	/// <summary>結束組字。</summary>
	[PreserveSig] int EndComposition(uint EcWrite);
}
