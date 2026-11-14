namespace Ngan.Ime.Windows.Tsf;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ngan.Ime.Core.Ime;
using Ngan.Ime.Core.Keys;
using System;
using System.Collections.Generic;

/// <summary>
/// 仿 Weasel 的直排候選窗：無邊框、不搶焦點、貼着游標顯示。
/// 第一行是 preedit，之後每個候選一行；高亮項用主色底。
/// 黑底白字，寬高都由內容決定，文字多長就佔多寬。
/// </summary>
public sealed class TsfCandidateWindow : Window {
    /// <summary>純黑底。</summary>
    static readonly IBrush BackBrush = Brushes.Black;

    /// <summary>純白字。</summary>
    static readonly IBrush ForeBrush = Brushes.White;

    /// <summary>高亮行的底色。</summary>
    static readonly IBrush HighlightBrush = new SolidColorBrush(Color.Parse("#2F6FEB"));

    readonly StackPanel Root = new() { Spacing = 0 };
    readonly TextBlock PreeditBlock = new() { FontSize = 16, Foreground = ForeBrush };
    readonly Border Frame;

    public TsfCandidateWindow() {
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        // 寬高都貼內容：候選多寬、preedit 多長，視窗就多大。
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = BackBrush;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Frame = new Border {
            Background = BackBrush,
            Padding = new Thickness(2, 1),
            Child = Root,
        };
        Content = Frame;
        Root.Children.Add(PreeditBlock);
    }

    /// <summary>以最新狀態刷新內容並移到游標左下角；沒有內容就隱藏。</summary>
    public void Update(string Preedit, CandidatePage? Page, int X, int Y) {
        var Items = Page?.Data;
        var HasContent = !string.IsNullOrEmpty(Preedit) || (Items is not null && Items.Count > 0);
        if (!HasContent) {
            Hide();
            return;
        }
        PreeditBlock.Text = Preedit;
        PreeditBlock.IsVisible = !string.IsNullOrEmpty(Preedit);
        while (Root.Children.Count > 1) {
            Root.Children.RemoveAt(1);
        }
        if (Items is not null) {
            var Highlight = Page?.HighlightedIndex ?? 0;
            for (var I = 0; I < Items.Count; I++) {
                var Cand = Items[I];
                var Row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                Row.Children.Add(new TextBlock {
                    Text = (I + 1).ToString() + ".",
                    FontSize = 14,
                    Foreground = ForeBrush,
                    Width = 18,
                });
                Row.Children.Add(new TextBlock { Text = Cand.Text, FontSize = 16, Foreground = ForeBrush });
                if (!string.IsNullOrEmpty(Cand.Comment)) {
                    Row.Children.Add(new TextBlock {
                        Text = Cand.Comment,
                        FontSize = 12,
                        Foreground = ForeBrush,
                    });
                }
                if (I == Highlight) {
                    Row.Background = HighlightBrush;
                }
                Root.Children.Add(Row);
            }
        }
        if (!IsVisible) {
            Show();
        }
        Position = new PixelPoint(X, Y);
    }

    /// <summary>結束輸入或失焦時收起。</summary>
    public void HideIfVisible() {
        if (IsVisible) {
            Hide();
        }
    }
}
