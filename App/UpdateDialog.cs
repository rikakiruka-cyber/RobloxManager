// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RobloxRAMManager;

/// <summary>Hộp thoại "Có bản cập nhật": hiện số phiên bản + ghi chú "có gì mới", nút Cập nhật ngay.</summary>
public sealed class UpdateDialog : Window
{
    private readonly UpdateInfo _info;
    private readonly ProgressBar _bar = new() { Height = 8, Minimum = 0, Maximum = 100, Margin = new Thickness(0, 14, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _status = new() { FontSize = 11, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly Button _update;
    private readonly Button _later;
    private CancellationTokenSource? _cts;
    private bool _busy;

    public UpdateDialog(UpdateInfo info)
    {
        _info = info;
        Title = Loc.Tr("Có bản cập nhật mới", "Update available");
        Width = 520;
        MaxHeight = 640;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(10, 22, 18));
        SetResourceReference(ForegroundProperty, "TextBrush");

        var root = new StackPanel { Margin = new Thickness(22) };
        var head = new TextBlock { Text = $"🆕 Roblox RAM Manager v{info.Version.ToString(3)}", FontSize = 17, FontWeight = FontWeights.Bold };
        head.SetResourceReference(TextBlock.ForegroundProperty, "AccentPrimaryBrush");
        root.Children.Add(head);

        var cur = new TextBlock
        {
            Text = Loc.Tr($"Bạn đang dùng v{AppInfo.VersionText}", $"You are on v{AppInfo.VersionText}"),
            FontSize = 11.5, Margin = new Thickness(0, 4, 0, 14)
        };
        cur.SetResourceReference(TextBlock.ForegroundProperty, "TextDimBrush");
        root.Children.Add(cur);

        root.Children.Add(new TextBlock
        {
            Text = Loc.Tr("Có gì mới:", "What's new:"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6)
        });

        string notes = Loc.IsEn ? info.NotesEn : info.NotesVi;
        if (string.IsNullOrWhiteSpace(notes)) notes = Loc.Tr("(Không có ghi chú)", "(No notes)");
        root.Children.Add(new ScrollViewer
        {
            MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock { Text = notes, TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 19 }
        });

        root.Children.Add(_bar);
        root.Children.Add(_status);

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        _later = new Button { Content = Loc.Tr("Để sau", "Later"), Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 8, 0) };
        _update = new Button { Content = Loc.Tr("⬇ Cập nhật ngay", "⬇ Update now"), Padding = new Thickness(16, 8, 16, 8) };
        if (Application.Current.TryFindResource("AccentButton") is Style st) _update.Style = st;
        _later.Click += (_, _) => { if (_busy) _cts?.Cancel(); else Close(); };
        _update.Click += async (_, _) => await StartAsync();
        row.Children.Add(_later);
        row.Children.Add(_update);
        root.Children.Add(row);

        Content = root;
        Closing += (_, _) => _cts?.Cancel();
    }

    private async Task StartAsync()
    {
        _busy = true;
        _update.IsEnabled = false;
        _later.Content = Loc.Tr("Huỷ", "Cancel");
        _bar.Visibility = Visibility.Visible;
        _bar.Value = 0;
        _status.Text = Loc.Tr("Đang tải bản mới...", "Downloading the new version...");
        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<double>(v =>
            {
                _bar.Value = v;
                _status.Text = Loc.Tr($"Đang tải... {v:0}%", $"Downloading... {v:0}%");
            });
            string file = await Updater.DownloadAsync(_info, progress, _cts.Token);
            _status.Text = Loc.Tr("Tải xong. Đang cài đặt và khởi động lại...", "Downloaded. Installing and restarting...");
            await Task.Delay(500);
            Updater.ApplyAndRestart(file);
        }
        catch (OperationCanceledException)
        {
            _status.Text = Loc.Tr("Đã huỷ.", "Cancelled.");
            _bar.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            _status.Text = "⚠ " + ex.Message;
            _bar.Visibility = Visibility.Collapsed;
            _update.Content = Loc.Tr("⬇ Thử lại", "⬇ Retry");
        }
        finally
        {
            _busy = false;
            _update.IsEnabled = true;
            _later.Content = Loc.Tr("Để sau", "Later");
        }
    }
}
