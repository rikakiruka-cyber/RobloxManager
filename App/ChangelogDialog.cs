// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RobloxRAMManager;

/// <summary>Hộp thoại "Lịch sử cập nhật": mọi phiên bản (mới nhất trước) theo ngôn ngữ app.</summary>
public sealed class ChangelogDialog : Window
{
    public ChangelogDialog()
    {
        Title = Loc.Tr("Lịch sử cập nhật", "Update history");
        Width = 520;
        Height = 560;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(10, 22, 18));
        SetResourceReference(ForegroundProperty, "TextBrush");

        var grid = new Grid { Margin = new Thickness(22) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var head = new TextBlock { Text = "📜 " + Loc.Tr("Lịch sử cập nhật", "Update history"), FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) };
        head.SetResourceReference(TextBlock.ForegroundProperty, "AccentPrimaryBrush");
        Grid.SetRow(head, 0);
        grid.Children.Add(head);

        var list = new StackPanel();
        var entries = Changelog.Load();
        if (entries.Count == 0)
            list.Children.Add(new TextBlock { Text = Loc.Tr("(Chưa có dữ liệu)", "(No data)"), FontSize = 12 });

        foreach (var en in entries)
        {
            bool current = en.Version == AppInfo.Current;
            string title = $"v{en.Version.ToString(3)}";
            if (!string.IsNullOrEmpty(en.Date)) title += "  ·  " + en.Date;
            if (current) title += "  ·  " + Loc.Tr("đang dùng", "current");

            var h = new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            h.SetResourceReference(TextBlock.ForegroundProperty, current ? "AccentPrimaryBrush" : "TextBrush");
            list.Children.Add(h);

            var notes = Loc.IsEn ? en.En : en.Vi;
            if (notes.Length == 0) notes = Loc.IsEn ? en.Vi : en.En;
            string text = notes.Length == 0
                ? Loc.Tr("(Không có ghi chú)", "(No notes)")
                : string.Join("\n", notes.Select(n => "• " + n));
            var body = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 19, Margin = new Thickness(8, 0, 0, 16) };
            if (notes.Length == 0) body.SetResourceReference(TextBlock.ForegroundProperty, "TextDimBrush");
            list.Children.Add(body);
        }

        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list };
        Grid.SetRow(sv, 1);
        grid.Children.Add(sv);

        var close = new Button { Content = Loc.Tr("Đóng", "Close"), Padding = new Thickness(18, 8, 18, 8), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        if (Application.Current.TryFindResource("AccentButton") is Style st) close.Style = st;
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 2);
        grid.Children.Add(close);

        Content = grid;
    }
}
