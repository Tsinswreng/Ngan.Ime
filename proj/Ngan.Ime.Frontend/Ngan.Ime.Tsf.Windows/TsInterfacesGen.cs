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
[Guid("5188183E-B383-488A-9FE0-F5C12E8A8B5F")]
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
