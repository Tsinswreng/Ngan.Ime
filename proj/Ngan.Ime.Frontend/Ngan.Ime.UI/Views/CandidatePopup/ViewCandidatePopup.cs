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

	[Doc("內容的根容器；組字列與候選行容器的先後由 {{nameof(Reversed)}} 決定。")]
	public StackPanel? _Root;

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
	翻到插入點上方時整塊倒排（照 Weasel 的 `m_istorepos`：`WeaselPanel.cpp:909` 清旗標、
	`:918` 設旗標、`:730-745` 把 preedit 移到候選之後、`:805-807` 連狀態圖示也一起搬）。
	由 {{nameof(ShowAt)}}／{{nameof(MoveTo)}} 依定位結果維護。
	""")]
	public bool Reversed;

	[Doc($$"""
	依 {{nameof(Reversed)}} 排定「組字列」與「候選行容器」的先後：正排是
	組字列在上、候選在下；倒排相反，讓第一候選緊接組字列、組字列緊接光標。
	""")]
	partial void ApplyOrder();

	[Doc($$"""
	套好內容後呼叫：排版、定尺寸、定位、顯示一次完成；順序照 Weasel
	（`Refresh` 裡 `DoLayout → _ResizeWindow → _RepositionWindow`，`WeaselUI/WeaselPanel.cpp:163-165`）。
	#Prm[{{nameof(Anchor)}}][插入點下緣與左邊（螢幕物理像素）。]
	#Prm[{{nameof(Top)}}][插入點矩形上緣（螢幕物理像素）。]
	""")]
	public partial void ShowAt(PixelPoint Anchor, i32 Top);

	[Doc("只換位置（內容沒變）；不負責顯示。")]
	public partial void MoveTo(PixelPoint Anchor, i32 Top);
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
