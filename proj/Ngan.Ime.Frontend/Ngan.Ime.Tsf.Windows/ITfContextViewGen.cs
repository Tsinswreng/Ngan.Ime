namespace Ngan.Ime.Tsf.Windows;

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

[Doc($$"""
`ITfContextView` 的生成式宣告（源生成器負責排 vtable，不再手寫槽位索引）。
槽位順序必須與 `msctf.h` 一致：
3 GetRangeFromPoint、4 GetTextExt、5 GetScreenExt、6 GetWnd。
生成器照宣告順序排，所以宣告不能亂序，也不能省略中間的方法。

2026-11-15 的驗證結果（NativeAOT x64，見 `scripts/spike-source-gen-com`）：
`[GeneratedComInterface]` 與 `[GeneratedComClass]` 在 AOT 下都可用；
`ComInterfaceMarshaller<T>.ConvertToManaged((void*)Ptr)` 能取得托管介面，
結構與 `out` 參數都封送正確，`StrategyBasedComWrappers` 也能把托管物件包成原生可呼叫的 CCW。
正式遷移時，其餘介面照同一模式補齊即可。
""")]
[GeneratedComInterface]
[Guid("2433BF8E-0F9B-435C-BA2C-180611978C30")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ITfContextViewGen {
	[Doc($$"""
	把螢幕座標換成文件範圍。
	""")]
	[PreserveSig] i32 GetRangeFromPoint(u32 Ec, IntPtr Pt, u32 Flags, out IntPtr Range);
	[Doc($$"""
	取得選取範圍在螢幕上的矩形；這是量插入點真正要用的一個。
	""")]
	[PreserveSig] i32 GetTextExt(u32 Ec, IntPtr Range, out NativeTsf.NativeRect Rc, out i32 Clipped);
	[Doc($$"""
	取得整個檢視在螢幕上的矩形。
	""")]
	[PreserveSig] i32 GetScreenExt(out NativeTsf.NativeRect Rc);
	[Doc($$"""
	取得檢視所在的視窗。
	""")]
	[PreserveSig] i32 GetWnd(out IntPtr Hwnd);
}
