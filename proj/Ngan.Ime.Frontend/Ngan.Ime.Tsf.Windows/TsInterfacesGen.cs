namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

// 這個檔案是「外殼呼叫 TSF」那一半的生成式宣告。
// 槽位順序由宣告順序決定，因此每一段宣告都必須與 msctf.h 的順序逐字對齊，
// 也不能省略中間用不到的方法；省略或亂序就是把槽位算錯，2026-11-15 的 notepad 崩潰就是這樣來的。
// 驗證方式：scripts/check-msctf-order 會重新解析 msctf.h 並比對本檔的宣告順序。

[Doc($$"""
`ITfContext` 的前 10 個方法；本殼只用到 3、5、9。
3 RequestEditSession、4 InWriteSession、5 GetSelection、6 SetSelection、
7 GetStart、8 GetEnd、9 GetActiveView。
6 是 SetSelection，少算它一格就會把 9 的 GetActiveView 指到 8 的 GetEnd。
""")]
[GeneratedComInterface]
[Guid("AA80E7FD-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfContextGen {
	[Doc($$"""
	排入一次編輯會話；`Pes` 是手寫的 ITfEditSession 介面指標。
	""")]
	[PreserveSig] i32 RequestEditSession(u32 Tid, IntPtr Pes, u32 DwFlags, out i32 PhrSession);
	[Doc($$"""
	查詢目前是否在寫入會話內。
	""")]
	[PreserveSig] i32 InWriteSession(u32 Tid, out i32 PfWriteSession);
	[Doc($$"""
	取出選取範圍。
	""")]
	[PreserveSig] i32 GetSelection(u32 Ec, u32 UlIndex, u32 UlCount, out NativeTsf.TfSelection PSelection, out u32 PcFetched);
	[Doc($$"""
	設定選取範圍；本殼不用，只為對齊槽位。
	""")]
	[PreserveSig] i32 SetSelection(u32 Ec, u32 UlCount, IntPtr PSelection);
	[Doc($$"""
	文件起點；本殼不用，只為對齊槽位。
	""")]
	[PreserveSig] i32 GetStart(u32 Ec, out IntPtr PpStart);
	[Doc($$"""
	文件終點；本殼不用，只為對齊槽位。
	""")]
	[PreserveSig] i32 GetEnd(u32 Ec, out IntPtr PpEnd);
	[Doc($$"""
	取得使用中的檢視；量插入點要用它。
	""")]
	[PreserveSig] i32 GetActiveView(out ITfContextViewGen PpView);
}

[Doc($$"""
`ITfRange` 的前 16 個方法；本殼只用到 4 與 15。
3 GetText、4 SetText、5 GetFormattedText、6 GetEmbedded、7 InsertEmbedded、
8 ShiftStart、9 ShiftEnd、10 ShiftStartToRange、11 ShiftEndToRange、
12 ShiftStartRegion、13 ShiftEndRegion、14 IsEmpty、15 Collapse。
""")]
[GeneratedComInterface]
[Guid("AA80E7FF-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfRangeGen {
	[Doc($$"""
	讀取範圍內的文字。
	""")]
	[PreserveSig] i32 GetText(u32 Ec, u32 DwFlags, IntPtr PchText, u32 CchMax, out u32 Pcch);
	[Doc($$"""
	把文字寫進範圍；上屏用它。
	""")]
	[PreserveSig] i32 SetText(u32 Ec, u32 DwFlags, IntPtr PchText, i32 Cch);
	[Doc($$"""
	取出格式化的資料物件；本殼不用。
	""")]
	[PreserveSig] i32 GetFormattedText(u32 Ec, out IntPtr PpDataObject);
	[Doc($$"""
	取出內嵌物件；本殼不用。
	""")]
	[PreserveSig] i32 GetEmbedded(u32 Ec, IntPtr RguidService, IntPtr Riid, out IntPtr Ppunk);
	[Doc($$"""
	插入內嵌物件；本殼不用。
	""")]
	[PreserveSig] i32 InsertEmbedded(u32 Ec, u32 DwFlags, IntPtr PDataObject);
	[Doc($$"""
	移動起點；本殼不用。
	""")]
	[PreserveSig] i32 ShiftStart(u32 Ec, i32 CchReq, out i32 Pcch, IntPtr PHalt);
	[Doc($$"""
	移動終點；本殼不用。
	""")]
	[PreserveSig] i32 ShiftEnd(u32 Ec, i32 CchReq, out i32 Pcch, IntPtr PHalt);
	[Doc($$"""
	把起點移到另一個範圍；本殼不用。
	""")]
	[PreserveSig] i32 ShiftStartToRange(u32 Ec, IntPtr PRange, u32 APos);
	[Doc($$"""
	把終點移到另一個範圍；本殼不用。
	""")]
	[PreserveSig] i32 ShiftEndToRange(u32 Ec, IntPtr PRange, u32 APos);
	[Doc($$"""
	把起點移出區域；本殼不用。
	""")]
	[PreserveSig] i32 ShiftStartRegion(u32 Ec, u32 Dir, out i32 PfNoRegion);
	[Doc($$"""
	把終點移出區域；本殼不用。
	""")]
	[PreserveSig] i32 ShiftEndRegion(u32 Ec, u32 Dir, out i32 PfNoRegion);
	[Doc($$"""
	判斷範圍是否為空；本殼不用。
	""")]
	[PreserveSig] i32 IsEmpty(u32 Ec, out i32 PfEmpty);
	[Doc($$"""
	把範圍收成一個插入點；上屏後把游標移到文字之後用它。
	""")]
	[PreserveSig] i32 Collapse(u32 Ec, u32 APos);
}

[Doc($$"""
`ITfKeystrokeMgr` 的前 5 個方法；本殼只用到 3 與 4。
3 AdviseKeyEventSink、4 UnadviseKeyEventSink。
`PSink` 仍以 IntPtr 傳入：那是手寫的 sink 介面指標，這一階段不改成托管物件。
""")]
[GeneratedComInterface]
[Guid("AA80E7F0-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfKeystrokeMgrGen {
	[Doc($$"""
	掛上按鍵事件接收器。
	""")]
	[PreserveSig] i32 AdviseKeyEventSink(u32 Tid, IntPtr PSink, i32 FForeground);
	[Doc($$"""
	卸下按鍵事件接收器。
	""")]
	[PreserveSig] i32 UnadviseKeyEventSink(u32 Tid);
	[Doc($$"""
	查詢前景輸入法；本殼不用。
	""")]
	[PreserveSig] i32 GetForeground(out Guid Pclsid);
	[Doc($$"""
	徵詢按鍵；本殼不用。
	""")]
	[PreserveSig] i32 TestKeyDown(IntPtr WParam, IntPtr LParam, out i32 PfEaten);
	[Doc($$"""
	徵詢擡起；本殼不用。
	""")]
	[PreserveSig] i32 TestKeyUp(IntPtr WParam, IntPtr LParam, out i32 PfEaten);
}

[Doc($$"""
`ITfInsertAtSelection` 的前 4 個方法；本殼只用到 3。
3 InsertTextAtSelection(ec, dwFlags, pchText, cch, ppRange)、
4 InsertEmbeddedAtSelection(...)。
用 `TF_IAS_QUERYONLY` 呼叫時不插入文字，只取回「插入點所在的範圍」，
這正是 Weasel 建立組字範圍的起點（`WeaselTSF/Composition.cpp:34`）。
""")]
[GeneratedComInterface]
[Guid("55CE16BA-3014-41C1-9CEB-FADE1446AC6C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfInsertAtSelectionGen {
	[Doc($$"""
	在插入點插入文字，或只取回插入點的範圍。
	""")]
	[PreserveSig] i32 InsertTextAtSelection(u32 Ec, u32 DwFlags, IntPtr PchText, i32 Cch, out IntPtr PpRange);
	[Doc($$"""
	在插入點插入內嵌物件；本殼不用。
	""")]
	[PreserveSig] i32 InsertEmbeddedAtSelection(u32 Ec, u32 DwFlags, IntPtr PDataObject, out IntPtr PpRange);
}

[Doc($$"""
`ITfContextComposition` 的前 4 個方法；本殼只用到 3。
3 StartComposition(ecWrite, pCompositionRange, pSink, ppComposition)、
4 EnumCompositions、5 FindComposition、6 TakeOwnership。
官方標頭檔在這之後還有 EndComposition；本殼用 `ITfComposition::EndComposition` 結束，因此不必宣告它。
""")]
[GeneratedComInterface]
[Guid("D40C8AAE-AC92-4FC7-9A11-0EE0E23AA39B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfContextCompositionGen {
	[Doc($$"""
	在指定範圍上開始一次組字。
	""")]
	[PreserveSig] i32 StartComposition(u32 EcWrite, IntPtr PCompositionRange, IntPtr PSink, out IntPtr PpComposition);
	[Doc($$"""
	列舉組字；本殼不用。
	""")]
	[PreserveSig] i32 EnumCompositions(out IntPtr PpEnum);
	[Doc($$"""
	找出範圍所在的組字；本殼不用。
	""")]
	[PreserveSig] i32 FindComposition(u32 EcRead, IntPtr PTestRange, out IntPtr PpEnum);
	[Doc($$"""
	接管既有的組字；本殼不用。
	""")]
	[PreserveSig] i32 TakeOwnership(u32 EcWrite, IntPtr PComposition, IntPtr PSink, out IntPtr PpComposition);
}

[Doc($$"""
`ITfComposition` 的 4 個方法。
3 GetRange、4 ShiftStart、5 ShiftEnd、6 EndComposition。
上屏流程是：取組字範圍 → 寫入最終文字 → 收成插入點 → 結束組字。
""")]
[GeneratedComInterface]
[Guid("20168D64-5A8F-4A5A-B7BD-CFA29F4D0FD9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfCompositionGen {
	[Doc($$"""
	取得組字的範圍。
	""")]
	[PreserveSig] i32 GetRange(out IntPtr PpRange);
	[Doc($$"""
	移動組字起點；本殼不用。
	""")]
	[PreserveSig] i32 ShiftStart(u32 EcWrite, IntPtr PNewStart);
	[Doc($$"""
	移動組字終點；本殼不用。
	""")]
	[PreserveSig] i32 ShiftEnd(u32 EcWrite, IntPtr PNewEnd);
	[Doc($$"""
	結束組字。
	""")]
	[PreserveSig] i32 EndComposition(u32 EcWrite);
}
