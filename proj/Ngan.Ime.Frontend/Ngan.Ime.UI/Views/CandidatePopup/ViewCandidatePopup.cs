namespace Ngan.Ime.UI.Views.CandidatePopup;

using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

[Doc($$"""
桌面候選浮窗：無邊框、置頂、不搶焦點，貼着插入點顯示。

*為何不繼承 `AppViewBase`*：那是給控件（UserControl）用的基底；
本類是 {{nameof(Window)}}，屬於頂層窗口，兩者的生命週期不同（視窗要自己 Show／Hide）。
故這裡保留同樣的「宣告與實現分離」與 `Vm` 慣例，但直接繼承 {{nameof(Window)}}。

*為何不用聲明式綁定*：候選數量每次都可能變，綁定做不到「按數量增減行」；
故由視圖按 VM 的內容重建行，但**只更新既有的行**，不整棵樹重建。

*只供桌面宿主*：Android／iOS／Browser 沒有這個視窗概念；
它們用內嵌的候選欄（`ViewCandidatesBar`），不會解析到本類。

#Eg[
	```cs
	var Popup = new ViewCandidatePopup(Vm);
	Popup.Refresh();
	```
]
""")]
public partial class ViewCandidatePopup : Window{

	[Doc("本視窗的 ViewModel。")]
	public VmCandidatePopup Vm{get;}

	[Doc("組字列控件；空串時隱藏。")]
	public TextBlock? _Preedit;

	[Doc("候選行的容器。")]
	public StackPanel? _CandList;

	[Doc("目前使用的候選行；數量隨候選增減，既有行會重用。")]
	public readonly List<CandidatePopupRow> Rows = [];

	[Doc("建立浮窗並綁定 ViewModel；不在此顯示。")]
	public partial ViewCandidatePopup(VmCandidatePopup Vm);

	[Doc("按 VM 內容刷新組字列與候選行；沒有內容時自行收起。")]
	public partial void Refresh();

	[Doc($$"""
	與 Weasel 相同的 6px 間距：`WeaselPanel::MoveTo` 先做 `m_inputPos.OffsetRect(0, 6)`
	（`WeaselUI/WeaselPanel.cpp:1129`），候選窗因此不會貼着、蓋着光標。
	""")]
	public const i32 AnchorGapPx = 6;

	[Doc($$"""
	一旦翻到插入點上方就固定在上方，避免視窗高度變動時來回跳（照 Weasel 的 `m_sticky`）。
	""")]
	public bool Sticky;

	[Doc("移到插入點：先放下方（間距 6px），下方不夠就翻到上方；一律夾在工作區內。")]
	public partial void MoveTo(PixelPoint Anchor);
}

[Doc($$"""
一行候選的三個控件：序號、候選詞、註釋。

*為何獨立成類*：浮窗每次刷新只需改文字與底色，不必碰控件樹結構；
把一行的控件攢在一起，刷新邏輯因此只是一次欄位賦值。
""")]
public sealed class CandidatePopupRow{

	[Doc("本行的外框；高亮時改它的底色，故整行含序號一起變色。")]
	public required Border Frame{get;init;}

	[Doc("序號文字（如「1.」）。")]
	public required TextBlock Index{get;init;}

	[Doc("候選詞文字。")]
	public required TextBlock Text{get;init;}

	[Doc("註釋文字；候選沒有註釋時隱藏。")]
	public required TextBlock Comment{get;init;}
}
