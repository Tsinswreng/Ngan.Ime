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
			var Cand = Vm.Candidates[I];
			var Row = Rows[I];
			// 序號由界面自己產生：引擎只給文字與註釋，序號是顯示層的事。
			Row.Index.Text = (I + 1).ToString() + ".";
			Row.Text.Text = Cand.Text ?? "";
			Row.Comment.Text = Cand.Comment ?? "";
			Row.Comment.IsVisible = !string.IsNullOrEmpty(Cand.Comment);
			Row.Frame.Background = I == Vm.Highlighted
				? UiCfg.Inst.PopupHighlightColor
				: Brushes.Transparent;
		}
		if(!IsVisible){
			Show();
		}
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
			FontSize = UiCfg.Inst.PopupIndexFontSize,
			Foreground = UiCfg.Inst.PopupForeColor,
		};
		var Text = new TextBlock{
			FontSize = UiCfg.Inst.PopupCandidateFontSize,
			Foreground = UiCfg.Inst.PopupForeColor,
		};
		var Comment = new TextBlock{
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

	[Doc("把視窗移到插入點左下角；碰到螢幕邊界就往內收。")]
	public partial void MoveTo(PixelPoint Anchor){
		var X = Anchor.X;
		var Y = Anchor.Y;
		// 依插入點所在的螢幕收邊界：多螢幕時用 Primary 會把副螢幕上的浮窗拉回主螢幕。
		// `Screens` 在視窗還沒有 platform impl 時可以是 null，那時直接照原座標放，不要拋
		// （拋了會被 CandidatePopup.Show 的 catch 接住，使用者看到的是「浮窗閃一下就不見」）。
		var Screens = this.Screens;
		var Screen = Screens?.ScreenFromPoint(Anchor) ?? Screens?.Primary;
		if(Screen is not null){
			var Area = Screen.WorkingArea;
			// 先量一次自身尺寸，才知道收進來多少；未顯示時 Bounds 可能為 0，故有下限保護。
			var W = (i32)Math.Ceiling(Bounds.Width);
			var H = (i32)Math.Ceiling(Bounds.Height);
			if(X + W > Area.Right){
				X = Area.Right - W;
			}
			if(Y + H > Area.Bottom){
				// 下方放不下就翻到插入點上方：再減一個字級當作間隔。
				Y = Anchor.Y - H - (i32)Math.Ceiling(UiCfg.Inst.PopupCandidateFontSize);
			}
			X = Math.Max(X, Area.X);
			Y = Math.Max(Y, Area.Y);
		}
		Position = new PixelPoint(X, Y);
	}
}
