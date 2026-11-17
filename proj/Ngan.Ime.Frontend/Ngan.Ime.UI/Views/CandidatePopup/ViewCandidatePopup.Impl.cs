namespace Ngan.Ime.UI.Views.CandidatePopup;

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

[Doc($$"""
{{nameof(ViewCandidatePopup)}} 的實現：建立控件樹、按快照刷新、依插入點定位。

*刷新策略*：行數不足時補、多餘時撤、其餘只改文字與底色。
一顆鍵最多動到五、六個 {{nameof(TextBlock)}}，不重建控件樹。
""")]
public partial class ViewCandidatePopup{

	[Doc("建立視窗屬性與控件樹；此時還不顯示。")]
	public partial ViewCandidatePopup(VmCandidatePopup Vm){
		this.Vm = Vm;
		Topmost = true;
		ShowInTaskbar = false;
		ShowActivated = false;
		CanResize = false;
		// 無邊框：型別名要寫全，否則屬性名稱會遮蔽型別。
		WindowDecorations = Avalonia.Controls.WindowDecorations.None;
		// *尺寸由我們自己定*，不交給 SizeToContent：
		// 照 Weasel 的順序（`_ResizeWindow` 先定大小、`_RepositionWindow` 再拿真實大小算位置），
		// 位置必須用「視窗真正會有多大」來算。交給 SizeToContent 的話，大小是在 Show 之後、
		// 另一個版面流程裡才定案的，而位置卻在那之前就算完了——翻到上方時面板下緣就會壓進文字行。
		SizeToContent = SizeToContent.Manual;
		Background = UiCfg.Inst.PopupBgColor;
		TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
		MkContent();
		// 只訂閱「一份快照套完」這一個事件，不逐屬性訂閱：Apply 會改四個屬性，
		// 逐屬性通知等於一顆鍵刷新四次（見 VmCandidatePopup.Applied 的註釋）。
		Vm.Applied += Refresh;
	}

	[Doc("建立「組字列 ＋ 候選行容器」的控件樹；初始為正排（組字列在上）。")]
	void MkContent(){
		var Root = new StackPanel{
			Spacing = UiCfg.Inst.PopupRowSpacing,
		};
		_Root = Root;
		_Preedit = new TextBlock{
			FontFamily = UiCfg.Inst.FontFamily,
			FontSize = UiCfg.Inst.PopupPreeditFontSize,
			Foreground = UiCfg.Inst.PopupForeColor,
			IsVisible = false,
		};
		_CandList = new StackPanel{
			Spacing = UiCfg.Inst.PopupRowSpacing,
		};
		Root.Children.Add(_Preedit);
		Root.Children.Add(_CandList);
		Content = new Border{
			Background = UiCfg.Inst.PopupBgColor,
			Padding = UiCfg.Inst.PopupPadding,
			Child = Root,
		};
	}

	[Doc($$"""
	依 {{nameof(Reversed)}} 排定容器子序。

	*為何換容器子序而不是只倒排候選行*：照 Weasel 的 `m_istorepos`，翻到插入點上方時
	整塊鏡射——preedit 貼着光標在最下面、第一候選緊接在它上面、第 n 個候選在最上面。
	只倒排候選行會變成「preedit 仍在最上、候選倒序在下」，那正是使用者回報的錯誤形狀。
	*為何要先比對現狀*：每次定位都重建子樹等於整塊重新排版，`DesiredSize` 會跟著失效；
	順序沒變就直接返回。
	""")]
	partial void ApplyOrder(){
		if(_Root is null || _Preedit is null || _CandList is null){
			return;
		}
		// 倒排：[候選容器, 組字列]；正排：[組字列, 候選容器]。
		var First = Reversed ? (Control)_CandList : _Preedit;
		var Second = Reversed ? (Control)_Preedit : _CandList;
		if(_Root.Children.Count == 2 && ReferenceEquals(_Root.Children[0], First)){
			return;
		}
		_Root.Children.Clear();
		_Root.Children.Add(First);
		_Root.Children.Add(Second);
		// 組字列可見與否仍由 Refresh 決定（空串時隱藏），這裡不碰 IsVisible。
	}

	[Doc("按 VM 的內容刷新；沒有內容時收起。")]
	public partial void Refresh(){
		if(_Preedit is null || _CandList is null){
			return;
		}
		if(!Vm.HasContent){
			Hide();
			return;
		}
		_Preedit.Text = Vm.Preedit;
		_Preedit.IsVisible = Vm.Preedit.Length > 0;
		SyncRowCount(Vm.Candidates.Count);
		for(var I = 0; I < Vm.Candidates.Count; I++){
			// 翻到插入點上方時把行序倒過來（照 Weasel 的 `m_istorepos`）：
			// 第一候選留在最靠近光標的那一端，而不是跑到最上面。
			var Src = Reversed ? Vm.Candidates.Count - 1 - I : I;
			var Cand = Vm.Candidates[Src];
			var Row = Rows[I];
			// 序號由界面自己產生：引擎只給文字與註釋，序號是顯示層的事；
			// 倒排時序號跟著候選走，不能跟著列走。
			Row.Index.Text = (Src + 1).ToString() + ".";
			Row.Text.Text = Cand.Text ?? "";
			Row.Comment.Text = Cand.Comment ?? "";
			Row.Comment.IsVisible = !string.IsNullOrEmpty(Cand.Comment);
			Row.Frame.Background = Src == Vm.Highlighted
				? UiCfg.Inst.PopupHighlightColor
				: Brushes.Transparent;
		}
		// 不在這裡顯示：顯示與定位由 ShowAt 一次做完，否則會先出現在舊位置再被搬走。
	}

	[Doc("把候選行數量調到 Count：不足就補、多餘就撤。")]
	void SyncRowCount(i32 Count){
		while(Rows.Count > Count){
			var Last = Rows[^1];
			Rows.RemoveAt(Rows.Count - 1);
			_CandList!.Children.Remove(Last.Frame);
		}
		while(Rows.Count < Count){
			var Row = MkRow();
			Rows.Add(Row);
			_CandList!.Children.Add(Row.Frame);
		}
	}

	[Doc("建立一行候選的三個控件；字級與顏色全部取自 {{nameof(UiCfg)}}。")]
	CandidatePopupRow MkRow(){
		// 序號不設固定寬度：文字多寬就佔多寬，序號與候選詞之間只留一個小間距。
		// 固定寬度會在「1.」後面留出一段空白（先前 18px 寬、實際字寬約 11px）。
		var Index = new TextBlock{
			FontFamily = UiCfg.Inst.FontFamily,
			FontSize = UiCfg.Inst.PopupIndexFontSize,
			Foreground = UiCfg.Inst.PopupForeColor,
		};
		var Text = new TextBlock{
			FontFamily = UiCfg.Inst.FontFamily,
			FontSize = UiCfg.Inst.PopupCandidateFontSize,
			Foreground = UiCfg.Inst.PopupForeColor,
		};
		var Comment = new TextBlock{
			FontFamily = UiCfg.Inst.FontFamily,
			FontSize = UiCfg.Inst.PopupCommentFontSize,
			Foreground = UiCfg.Inst.PopupForeColor,
			VerticalAlignment = VAlign.Bottom,
		};
		var Line = new StackPanel{
			Orientation = Orientation.Horizontal,
			Spacing = UiCfg.Inst.PopupInnerSpacing,
		};
		Line.Children.Add(Index);
		Line.Children.Add(Text);
		Line.Children.Add(Comment);
		var Frame = new Border{
			Background = Brushes.Transparent,
			Child = Line,
		};
		return new CandidatePopupRow{
			Frame = Frame,
			Index = Index,
			Text = Text,
			Comment = Comment,
		};
	}

	[Doc($$"""
	顯示並定位；順序照 Weasel：先把內容排好、把視窗大小定案，算好位置，視窗最後才第一次出現。
	#Prm[{{nameof(Anchor)}}][插入點下緣。]
	#Prm[{{nameof(Top)}}][插入點矩形上緣。]
	""")]
	public partial void ShowAt(PixelPoint Anchor, i32 Top){
		var Scaled = ApplyPlacement(Anchor, Top);
		if(!IsVisible){
			Show();
			if(!Scaled){
				// 平台窗口還沒建之前可能取不到螢幕縮放（`Screens` 取決於平台實現）。
				// 這裡在同一則 UI 訊息裡補算一次：Show 之後才拿得到縮放，而畫面還沒渲染，
				// 所以使用者看不到中間狀態，也不會多出一幀。
				ApplyPlacement(Anchor, Top);
			}
			// 記一次「視窗真正的大小」：定位用的是我們設定的尺寸，這一條用來核對平台有沒有
			// 額外加邊框或取整。只記日誌、不改行為，下次有數字可查。
			AppLog.Info($"[Place] shown bounds={Bounds.Width:F1}x{Bounds.Height:F1}"
				+ $" client={ClientSize.Width:F1}x{ClientSize.Height:F1}"
				+ $" at=({Position.X},{Position.Y})");
		}
	}

	[Doc("只換位置（內容沒變時，例如光標移動）；不負責顯示。")]
	public partial void MoveTo(PixelPoint Anchor, i32 Top){
		ApplyPlacement(Anchor, Top);
	}

	[Doc($$"""
	算位置、定尺寸、寫進 {{nameof(Position)}}；不負責顯示。

	*以 Weasel 0.15.0 為準*，三段順序照它的 `Refresh`
	（`DoLayout → _ResizeWindow → _RepositionWindow`，`WeaselPanel.cpp:139-141`）：

	1. 依內容排版，量出內容尺寸（Avalonia 給的是版面單位 DIP）；
	2. 換成物理像素（乘螢幕縮放），用**這個**高度決定翻不翻、要不要倒排；
	3. 把視窗大小定案（DIP），再用像素座標定位。

	*為何一定要換單位*：Weasel 全程只在物理像素下算（`GetWindowRect`、`MonitorFromRect`、
	`rcWork`）。我們的 `Screens.WorkingArea` 與 `Window.Position` 也是物理像素，
	但 `DesiredSize`／`Bounds` 是 DIP；125% 縮放下直接混用，翻到上方時面板下緣會低
	25% 的高度，正好壓在文字行上——這就是「算式對、畫面錯」的原因。

	*為何不先 Show 再量*：`Bounds` 是上一輪內容的尺寸，候選行數一變就是錯的；
	而且那會變成「先畫在舊位置、下一幀再搬」。
	#Prm[{{nameof(Anchor)}}][插入點下緣與左邊（螢幕物理像素）。]
	#Prm[{{nameof(Top)}}][插入點矩形上緣（螢幕物理像素）。]
	#Rtn[true 表示螢幕縮放已取得（尺寸與位置都是像素座標）；false 表示用了 1.0 的兜底值。]
	""")]
	bool ApplyPlacement(PixelPoint Anchor, i32 Top){
		// 1: 排版並量內容；這一步之後 DesiredSize 才是這次的內容尺寸。
		MeasureContent();
		var ContentWidth = ContentSize.Width;
		var ContentHeight = ContentSize.Height;
		// 2: 取螢幕縮放與工作區（兩者都是物理像素的來源）。
		var Scaled = TryScalingAt(Anchor, out var Scale);
		var Work = WorkAreaAt(Anchor);
		var WidthPx = Px(ContentWidth, Scale);
		var HeightPx = Px(ContentHeight, Scale);
		// 3: 用真實像素高度決定翻不翻（翻上去時面板下緣貼着文字行上緣）。
		var Placement = PopupPlacer.Compute(
			new PopupAnchor{ X = Anchor.X, Y = Anchor.Y, Top = Top },
			WidthPx, HeightPx, Work);
		// 4: 翻轉會把候選行倒過來；行序一換就重排，再重量一次尺寸（行數不變，尺寸理應相同，
		//    但版面若因字寬不同而變，這裡拿到的才是這次真的要用的高度）。
		if(Placement.Reversed != Reversed){
			Reversed = Placement.Reversed;
			// 先把容器的子序換過來，再由 Refresh 依新的 Reversed 重排候選行序；
			// 兩者必須一致，否則第一候選會跑到離光標最遠的那一端。
			ApplyOrder();
			Refresh();
			MeasureContent();
			ContentWidth = ContentSize.Width;
			ContentHeight = ContentSize.Height;
			WidthPx = Px(ContentWidth, Scale);
			HeightPx = Px(ContentHeight, Scale);
			Placement = PopupPlacer.Compute(
				new PopupAnchor{ X = Anchor.X, Y = Anchor.Y, Top = Top },
				WidthPx, HeightPx, Work);
		}
		// 5: 先把視窗大小定案（Weasel 的 `_ResizeWindow`），再定位（`_RepositionWindow`）。
		//    次序反過來的話，位置是拿「我以為的高度」算的，不是視窗真正的高度。
		var WidthDip = Math.Ceiling(ContentWidth);
		var HeightDip = Math.Ceiling(ContentHeight);
		Width = WidthDip;
		Height = HeightDip;
		Position = new PixelPoint(Placement.X, Placement.Y);
		AppLog.Info($"[Place] anchor=({Anchor.X},{Anchor.Y}) top={Top} scale={Scale:F2}"
			+ $" dip={WidthDip:F0}x{HeightDip:F0} px={WidthPx}x{HeightPx}"
			+ $" work={Work.X},{Work.Y},{Work.Right},{Work.Bottom}"
			+ $" flip={(Placement.Flip ? 1 : 0)} rev={(Reversed ? 1 : 0)}"
			+ $" pos=({Placement.X},{Placement.Y})");
		return Scaled;
	}

	[Doc($$"""
	把版面單位（DIP）換成物理像素。
	#Prm[{{nameof(Dip)}}][版面單位。]
	#Prm[{{nameof(Scale)}}][螢幕縮放。]
	#Rtn[物理像素；無條件進位，寧可多留 1px 也不要少算而壓到字。]
	""")]
	static i32 Px(f64 Dip, f64 Scale){
		return (i32)Math.Ceiling(Dip * Scale);
	}

	[Doc($$"""
	插入點所在螢幕的縮放比例。
	*為何不信 {{nameof(RenderScaling)}}*：它要在平台窗口建立之後才有值，而我們在第一次 Show 之前就要算位置。
	`screens` 的 `Screen.Scaling` 對每個螢幕都成立，故優先取它；兩者都拿不到才回 1.0。
	#Prm[{{nameof(Point)}}][插入點（螢幕物理像素）。]
	#Prm[{{nameof(Scale)}}][取得的縮放；取不到時為 1.0。]
	#Rtn[true 表示真的取到了。]
	""")]
	bool TryScalingAt(PixelPoint Point, out f64 Scale){
		Scale = 1.0;
		try{
			var Screens = this.Screens;
			var Screen = Screens?.ScreenFromPoint(Point) ?? Screens?.Primary;
			if(Screen is not null && Screen.Scaling > 0){
				Scale = Screen.Scaling;
				return true;
			}
		}catch(Exception){
			// 平台還沒就緒時這裡可能拋；下方的兜底比在這裡中斷顯示要好。
		}
		try{
			if(RenderScaling > 0){
				Scale = RenderScaling;
				return true;
			}
		}catch(Exception){
			// 同上：拿不到就回 1.0，並由 ShowAt 在 Show 之後補算一次。
		}
		return false;
	}

	[Doc($$"""
	插入點所在螢幕的工作區（物理像素）。
	取不到時退回 {{nameof(FallbackWork)}}，並記一則 Warn——寧可位置不完美，也不要讓浮窗消失。
	#Prm[{{nameof(Point)}}][插入點（螢幕物理像素）。]
	#Rtn[工作區。]
	""")]
	PixelRect WorkAreaAt(PixelPoint Point){
		try{
			var Screens = this.Screens;
			var Screen = Screens?.ScreenFromPoint(Point) ?? Screens?.Primary;
			if(Screen is not null){
				return Screen.WorkingArea;
			}
		}catch(Exception){
			// 見 TryScalingAt。
		}
		AppLog.Warn("[Place] 取不到螢幕工作區，暫用 1920x1080 兜底");
		return FallbackWork;
	}

	[Doc("取不到工作區時的兜底；只影響貼邊與翻轉判斷，不影響「貼着插入點」這件事。")]
	static readonly PixelRect FallbackWork = new(0, 0, 1920, 1080);

	[Doc("量一次內容的尺寸：`Window.Measure` 受視窗系統影響，直接量子控件最可靠。")]
	void MeasureContent(){
		var Root = Content as Layoutable;
		if(Root is not null){
			Root.Measure(Size.Infinity);
		}
	}

	[Doc("內容的期望尺寸；沒有內容時退回視窗自己的 `DesiredSize`。")]
	Size ContentSize{
		get{
			var Root = Content as Layoutable;
			return Root is not null ? Root.DesiredSize : DesiredSize;
		}
	}
}
