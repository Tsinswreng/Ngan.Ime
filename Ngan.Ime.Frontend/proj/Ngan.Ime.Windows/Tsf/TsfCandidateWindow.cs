namespace Ngan.Ime.Windows.Tsf;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ngan.Ime.Core.Keys;
using System;
using System.Collections.Generic;

/// <summary>
/// 仿 Weasel 的直排候選窗：無邊框、不搶焦點、貼着游標顯示。
/// 第一行是 preedit，之後每個候選一行；高亮項用主色底。
/// </summary>
public sealed class TsfCandidateWindow : Window {
    readonly StackPanel Root = new() { Spacing = 2 };
    readonly TextBlock PreeditBlock = new() { FontSize = 16, Foreground = Brushes.White };

    public TsfCandidateWindow() {
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        Background = new SolidColorBrush(Color.Parse("#252A31"));
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Content = new Border {
            BorderBrush = new SolidColorBrush(Color.Parse("#6C7785")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6),
            Child = Root,
        };
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
                var Row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                Row.Children.Add(new TextBlock {
                    Text = (I + 1).ToString() + ".",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.Parse("#9DA9B8")),
                    Width = 20,
                });
                Row.Children.Add(new TextBlock { Text = Cand.Text, FontSize = 16, Foreground = Brushes.White });
                if (!string.IsNullOrEmpty(Cand.Comment)) {
                    Row.Children.Add(new TextBlock {
                        Text = Cand.Comment,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.Parse("#9DA9B8")),
                    });
                }
                if (I == Highlight) {
                    Row.Background = new SolidColorBrush(Color.Parse("#2F6FEB"));
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
