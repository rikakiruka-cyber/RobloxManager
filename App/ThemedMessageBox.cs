// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RobloxRAMManager;

/// <summary>Thay MessageBox gốc của Windows (luôn nền trắng) bằng hộp thoại cùng theme với app.
/// Lỗi bất kỳ khi dựng hộp thoại → tự lùi về MessageBox gốc để không bao giờ mất thông báo.</summary>
public static class Dlg
{
    public static MessageBoxResult Show(string text, string title, MessageBoxButton buttons = MessageBoxButton.OK,
                                        MessageBoxImage icon = MessageBoxImage.None, Window? owner = null)
    {
        try
        {
            if (Application.Current == null) throw new InvalidOperationException();
            owner ??= Application.Current.MainWindow;
            if (owner is { IsLoaded: false } || owner is { IsVisible: false }) owner = null;
            var w = new ThemedMessageWindow(text, title, buttons, icon);
            if (owner != null && !ReferenceEquals(owner, w)) { w.Owner = owner; w.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
            else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            w.ShowDialog();
            return w.Result;
        }
        catch
        {
            return MessageBox.Show(text, title, buttons, icon);
        }
    }
}

internal sealed class ThemedMessageWindow : Window
{
    public MessageBoxResult Result { get; private set; }
    private readonly MessageBoxResult _cancelResult;

    public ThemedMessageWindow(string text, string title, MessageBoxButton buttons, MessageBoxImage icon)
    {
        Title = title;
        MinWidth = 340; MaxWidth = 640; MaxHeight = 640;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        this.ApplyThemeWindow();

        string glyph = icon switch
        {
            MessageBoxImage.Error => "⛔",
            MessageBoxImage.Warning => "⚠",
            MessageBoxImage.Question => "❓",
            MessageBoxImage.Information => "ℹ",
            _ => ""
        };
        string iconKey = icon switch
        {
            MessageBoxImage.Error => "AccentDangerBrush",
            MessageBoxImage.Warning => "AccentWarningBrush",
            _ => "AccentPrimaryBrush"
        };

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var body = new DockPanel { LastChildFill = true };
        if (glyph.Length > 0)
        {
            var ic = new TextBlock { Text = glyph, FontSize = 26, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top };
            ic.Fg(iconKey);
            DockPanel.SetDock(ic, Dock.Left);
            body.Children.Add(ic);
        }
        var msg = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 20 };
        body.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 460,
            Content = msg
        });
        Grid.SetRow(body, 0);
        root.Children.Add(body);

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        Grid.SetRow(row, 1);

        void Add(string label, MessageBoxResult r, bool accent, bool isDefault, bool isCancel)
        {
            var b = new Button { Content = label, MinWidth = 84, Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8, 0, 0, 0), IsDefault = isDefault, IsCancel = isCancel };
            if (accent && Application.Current.TryFindResource("AccentButton") is Style st) b.Style = st;
            b.Click += (_, _) => { Result = r; Close(); };
            row.Children.Add(b);
        }

        switch (buttons)
        {
            case MessageBoxButton.YesNo:
                Add(Loc.Tr("Không", "No"), MessageBoxResult.No, false, false, true);
                Add(Loc.Tr("Có", "Yes"), MessageBoxResult.Yes, true, true, false);
                _cancelResult = MessageBoxResult.No;
                break;
            case MessageBoxButton.OKCancel:
                Add(Loc.Tr("Huỷ", "Cancel"), MessageBoxResult.Cancel, false, false, true);
                Add("OK", MessageBoxResult.OK, true, true, false);
                _cancelResult = MessageBoxResult.Cancel;
                break;
            default:
                Add("OK", MessageBoxResult.OK, true, true, true);
                _cancelResult = MessageBoxResult.OK;
                break;
        }
        Result = _cancelResult; // đóng bằng nút X / Alt+F4

        root.Children.Add(row);
        Content = root;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Result = _cancelResult; Close(); } };
    }
}
