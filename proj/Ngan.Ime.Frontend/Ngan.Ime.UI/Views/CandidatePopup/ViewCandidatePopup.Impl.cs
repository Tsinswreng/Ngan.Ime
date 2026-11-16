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
		// 寬高貼內容：候選多寬、組字多長，視窗就多大。
		SizeToContent = SizeToContent.WidthAndHeight;
		Background = UiCfg.Inst.PopupBgColor;
		TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
		MkContent();
		// 只訂閱「一份快照套完」這一個事件，不逐屬性訂閱：Apply 會改四個屬性，
		// 逐屬性通知等於一顆鍵刷新四次（見 VmCandidatePopup.Applied 的註釋）。
		Vm.Applied += Refresh;
	}

	[Doc("建立「組字列 ＋ 候選行容器」的控件樹。")]
	void MkContent(){
		var Root = new StackPanel{
			Spacing = UiCfg.Inst.PopupRowSpacing,
		};
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
	顯示並定位；順序照 Weasel：先把內容排好、算好位置，視窗最後才第一次出現。
	#Prm[{{nameof(Anchor)}}][插入點下緣。]
	#Prm[{{nameof(Top)}}][插入點矩形上緣。]
	""")]
	public partial void ShowAt(PixelPoint Anchor, i32 Top){
		ApplyPlacement(Anchor, Top);
		if(!IsVisible){
			Show();
		}
	}

	[Doc("只換位置（內容沒變時，例如光標移動）；不負責顯示。")]
	public partial void MoveTo(PixelPoint Anchor, i32 Top){
		ApplyPlacement(Anchor, Top);
	}

	[Doc($$"""
	算位置並寫進 {{nameof(Position)}}；不顯示。

	*照 Weasel `WeaselPanel::MoveTo` ＋ `_RepositionWindow`（`WeaselUI/WeaselPanel.cpp:1121-1223`）*：
	+ 插入點下緣往下讓 6px（`OffsetRect(0, 6)`）；
	+ x 夾在插入點所在螢幕的工作區內；
	+ 下方放不下就翻到插入點**上緣之上**：`y = Top - 高度`
		（Weasel：`m_inputPos.top - height - 6`，`m_inputPos.top` 是上緣 + 6）；
	+ 翻上去時行序倒排（{{nameof(Reversed)}}，照 `m_istorepos`），第一候選貼着光標；
	+ 翻過一次就固定上方（`m_sticky`）；y 不得高於工作區上緣。

	*尺寸一定用 `DesiredSize`*：`Bounds` 是上一輪內容的尺寸，候選行數一變就是錯的。
	""")]
	void ApplyPlacement(PixelPoint Anchor, i32 Top){
		MeasureContent();
		var Width = (i32)Math.Ceiling(ContentSize.Width);
		var Height = (i32)Math.Ceiling(ContentSize.Height);
		var Screens = this.Screens;
		var Screen = Screens?.ScreenFromPoint(Anchor) ?? Screens?.Primary;
		var Work = Screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
		var Below = Anchor.Y + AnchorGapPx;
		var Flip = Below > Work.Bottom - Height || Sticky;
		if(Flip != Reversed){
			// 行序一換高度就變：先換、重量，再用新高度算 y。
			Reversed = Flip;
			Refresh();
			MeasureContent();
			Width = (i32)Math.Ceiling(ContentSize.Width);
			Height = (i32)Math.Ceiling(ContentSize.Height);
		}
		if(Flip){
			Sticky = true;
		}
		var X = Anchor.X;
		var Y = Flip ? Top - Height : Anchor.Y + AnchorGapPx;
		var MaxX = Work.Right - Width;
		if(X > MaxX){
			X = MaxX;
		}
		if(X < Work.X){
			X = Work.X;
		}
		if(Y < Work.Y){
			Y = Work.Y;
		}
		Position = new PixelPoint(X, Y);
		AppLog.Info($"[Place] anchor=({Anchor.X},{Anchor.Y}) top={Top} w={Width} h={Height} work={Work.X},{Work.Y},{Work.Right},{Work.Bottom} flip={(Flip ? 1 : 0)} rev={(Reversed ? 1 : 0)} pos=({X},{Y})");
	}

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
