// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RobloxRAMManager;

/// <summary>Thư mục dữ liệu chung (config, log, tài khoản mã hoá) do người dùng chọn ổ/thư mục.</summary>
public static class DataStore
{
    // File "con trỏ" nhỏ trong %AppData% để lần sau biết dữ liệu nằm ở đâu.
    private static string Pointer => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RobloxManager", "location.txt");

    public static string? Dir { get; private set; }
    public static string LogsDir => Path.Combine(Dir ?? AppDomain.CurrentDomain.BaseDirectory, "logs");
    public static string FilePath(string name) => Path.Combine(Dir ?? AppDomain.CurrentDomain.BaseDirectory, name);

    public static bool TryLoad()
    {
        try
        {
            if (!File.Exists(Pointer)) return false;
            var d = File.ReadAllText(Pointer).Trim();
            if (string.IsNullOrWhiteSpace(d) || !Directory.Exists(d)) return false;
            Dir = d;
            Directory.CreateDirectory(LogsDir);
            return true;
        }
        catch { return false; }
    }

    public static void Set(string dir)
    {
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "logs"));
        Directory.CreateDirectory(Path.GetDirectoryName(Pointer)!);
        File.WriteAllText(Pointer, dir);
        Dir = dir;
    }

    /// <summary>Chép config.json cũ (cạnh file exe) sang thư mục dữ liệu mới nếu có.</summary>
    public static void MigrateLegacy()
    {
        try
        {
            var old = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            var dst = FilePath("config.json");
            if (File.Exists(old) && !File.Exists(dst)) File.Copy(old, dst);
        }
        catch { }
    }

    /// <summary>Chép toàn bộ dữ liệu hiện tại sang thư mục mới (không xoá bản cũ).</summary>
    public static void CopyAllTo(string newDir)
    {
        if (Dir == null) return;
        Directory.CreateDirectory(Path.Combine(newDir, "logs"));
        foreach (var f in new[] { "config.json", "accounts.dat", "accounts.dat.bak", "vault.key", "games.json" })
        {
            var src = Path.Combine(Dir, f);
            if (File.Exists(src)) File.Copy(src, Path.Combine(newDir, f), true);
        }
        var logs = Path.Combine(Dir, "logs");
        if (Directory.Exists(logs))
            foreach (var f in Directory.GetFiles(logs))
                File.Copy(f, Path.Combine(newDir, "logs", Path.GetFileName(f)), true);
    }

    public static void PurgeOldLogs(int keepDays = 14)
    {
        try
        {
            foreach (var f in Directory.GetFiles(LogsDir, "*.log"))
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-keepDays)) File.Delete(f);
        }
        catch { }
    }
}

/// <summary>Hộp thoại chọn ổ đĩa / thư mục lưu dữ liệu.</summary>
public sealed class DataFolderDialog : Window
{
    private readonly ComboBox _drives = new() { Margin = new Thickness(0, 0, 0, 10), Height = 32, FontSize = 12 };
    private readonly TextBox _path = new() { Height = 32, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) };
    public string SelectedPath => _path.Text.Trim();

    public DataFolderDialog(string? current = null)
    {
        Title = Loc.Tr("Chọn nơi lưu dữ liệu", "Choose data location");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        this.ApplyThemeWindow();

        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(new TextBlock
        {
            Text = Loc.Tr("Chọn ổ đĩa để lưu config, log và tài khoản (đã mã hoá):",
                          "Choose a drive to store config, logs and accounts (encrypted):"),
            TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12)
        });

        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                if (!d.IsReady || d.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                string label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "" : $"[{d.VolumeLabel}] ";
                double freeGb = d.AvailableFreeSpace / 1073741824.0;
                _drives.Items.Add(new ComboBoxItem
                {
                    Content = $"{d.Name}   {label}({freeGb:0.#} GB {Loc.Tr("trống", "free")})",
                    Tag = d.Name
                });
            }
        }
        catch { }

        _drives.SelectionChanged += (_, _) =>
        {
            if (_drives.SelectedItem is ComboBoxItem ci && ci.Tag is string r)
                _path.Text = Path.Combine(r, "RobloxManager_Data");
        };
        root.Children.Add(_drives);

        root.Children.Add(new TextBlock
        {
            Text = Loc.Tr("Thư mục sẽ tạo:", "Folder to be created:"), FontSize = 11, Margin = new Thickness(0, 0, 0, 4)
        });
        root.Children.Add(_path);

        if (_drives.Items.Count > 0)
        {
            string sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            int idx = 0;
            for (int i = 0; i < _drives.Items.Count; i++)
                if (_drives.Items[i] is ComboBoxItem ci && ci.Tag is string r &&
                    !r.Equals(sysRoot, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
            _drives.SelectedIndex = idx;
        }
        if (!string.IsNullOrWhiteSpace(current)) _path.Text = current;

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var browse = new Button { Content = Loc.Tr("📁 Chọn thư mục khác...", "📁 Other folder..."), Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
        browse.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = Loc.Tr("Chọn thư mục lưu dữ liệu", "Choose data folder") };
            if (dlg.ShowDialog(this) == true) _path.Text = Path.Combine(dlg.FolderName, "RobloxManager_Data");
        };
        var cancel = new Button
        {
            Content = string.IsNullOrWhiteSpace(current) ? Loc.Tr("Thoát", "Exit") : Loc.Tr("Huỷ", "Cancel"),
            Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0)
        };
        cancel.Click += (_, _) => DialogResult = false;
        var ok = new Button
        {
            Content = Loc.Tr("✔ Dùng thư mục này", "✔ Use this folder"),
            Padding = new Thickness(14, 8, 14, 8), FontWeight = FontWeights.Bold
        };
        if (Application.Current.TryFindResource("AccentButton") is Style st) ok.Style = st;
        ok.Click += (_, _) =>
        {
            var p = SelectedPath;
            if (string.IsNullOrEmpty(p)) return;
            try
            {
                Directory.CreateDirectory(p);
                var t = Path.Combine(p, ".write_test");
                File.WriteAllText(t, "x");
                File.Delete(t);
            }
            catch (Exception ex)
            {
                Dlg.Show(Loc.Tr("Không ghi được vào thư mục này:\n", "Cannot write to this folder:\n") + ex.Message,
                                Title, MessageBoxButton.OK, MessageBoxImage.Warning, this);
                return;
            }
            DialogResult = true;
        };
        row.Children.Add(browse);
        row.Children.Add(cancel);
        row.Children.Add(ok);
        root.Children.Add(row);
        Content = root;
    }
}
