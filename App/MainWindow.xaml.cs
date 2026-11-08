// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Shapes = System.Windows.Shapes;

namespace RobloxRAMManager;

public partial class MainWindow : Window
{
    private readonly ProcessMonitor _monitor = new();
    private readonly CrashRecovery _crashRecovery = new();
    private bool _autoMode;
    private bool _loaded;
    private bool _isSettingsOpen;
    private bool _isHelpOpen;
    private RobloxInstanceVM? _selectedInstance;
    private List<ClientInfo> _clients = new();
    private readonly Action<string> _onNativeDiagnostic;

    private readonly System.Windows.Threading.DispatcherTimer _clockTimer;
    private readonly System.Windows.Threading.DispatcherTimer _selfTrimTimer;
    private readonly System.Windows.Threading.DispatcherTimer _autoTrimTimer;
    private readonly System.Windows.Threading.DispatcherTimer _discordTimer;
    private DateTime _lastDiscordSent = DateTime.MinValue;

    private const uint RAM_HIGH = 80, RAM_CRITICAL = 90, RAM_NORMAL = 60;

    public MainWindow()
    {
        InitializeComponent();
        InstanceGrid.ItemsSource = _monitor.Instances;
        _monitor.OnInstanceCrashed += OnCrashed;
        // Log chẩn đoán từ tầng theo dõi tiến trình (vd lọc "process ma") chạy ở thread nền → phải
        // marshal về UI thread trước khi đụng vào LogText, nếu không WPF sẽ ném lỗi cross-thread.
        _onNativeDiagnostic = msg => Dispatcher.BeginInvoke(() => Log(msg));
        NativeInterop.Diagnostic += _onNativeDiagnostic;

        // Clock & Uptime update every second
        _clockTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) =>
        {
            ClockLabel.Text = DateTime.Now.ToString("HH:mm:ss");
            ManagerUptimeLabel.Text = $"⏱️ {_monitor.ManagerUptimeDisplay}";
            if (_selectedInstance != null && AccountDetailView.Visibility == Visibility.Visible)
            {
                AccDetailUptime.Text = _selectedInstance.UptimeDisplay;
            }
        };
        _clockTimer.Start();

        // Tự dọn RAM của chính app mỗi 5 phút để luôn nhẹ
        _selfTrimTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _selfTrimTimer.Tick += (_, _) => NativeInterop.TrimSelf();
        _selfTrimTimer.Start();

        // Auto-trim Roblox instances
        _autoTrimTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(AppConfig.TrimIntervalSec) };
        _autoTrimTimer.Tick += (_, _) =>
        {
            foreach (var i in _monitor.Instances) NativeInterop.TrimMemory(i.Pid);
        };

        // Discord periodic webhook timer (checks every 60s)
        _discordTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _discordTimer.Tick += DiscordTimer_Tick;
        _discordTimer.Start();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        AuthorLabel.Text = $"by {AppInfo.Author}";
        EngineLabel.Text = NativeInterop.IsNativeAvailable ? "Engine: C++ Native" : "Engine: Managed (.NET)";

        // Load config values into UI
        CrashRecoveryChk.IsChecked = AppConfig.CrashRecovery;
        CpuCapSlider.Value = AppConfig.CpuCapPercent;
        CpuCapLabel.Text = $"{AppConfig.CpuCapPercent}%";
        RamCapSlider.Value = AppConfig.RamCapMB;
        RamCapLabel.Text = RamCapText(AppConfig.RamCapMB);
        ThemeLabel.Text = ThemeManager.Current;

        // Discord inputs
        DiscordUrlBox.Text = AppConfig.DiscordWebhookUrl;
        DiscordCrashAlertChk.IsChecked = AppConfig.DiscordAlertOnCrash;
        DiscordUserIdBox.Text = AppConfig.DiscordUserId;
        AutoUpdateChk.IsChecked = AppConfig.AutoUpdateCheck;
        AccManualCloseChk.IsChecked = AppConfig.IgnoreManualClose;

        int discordIdx = AppConfig.DiscordWebhookIntervalMin switch
        {
            5 => 1,
            15 => 2,
            30 => 3,
            60 => 4,
            120 => 5,
            _ => 0
        };
        DiscordIntervalCombo.SelectedIndex = discordIdx;

        // Combo selections
        int pollIdx = AppConfig.PollIntervalMs switch { 1000 => 0, 2000 => 1, 5000 => 2, _ => 1 };
        IntervalCombo.SelectedIndex = pollIdx;
        PollCombo.SelectedIndex = pollIdx;

        TrimIntervalCombo.SelectedIndex = AppConfig.TrimIntervalSec switch
        {
            5 => 0,
            10 => 1,
            20 => 2,
            30 => 3,
            0 => 4,
            _ => 1
        };

        PriorityCombo.SelectedIndex = 2; // Normal

        RescanClients();

        // Aurora background
        AuroraChk.IsChecked = AppConfig.AuroraAnimated;
        SetAurora(AppConfig.AuroraAnimated);

        // Build themes list
        BuildThemeButtons();

        // Ngôn ngữ
        if (Loc.IsEn) ApplyLanguage(); else { RefreshAbout(); RefreshLangButtons(); }

        // Dữ liệu + tài khoản
        DataPathLabel.Text = DataStore.Dir ?? "";
        DataStore.PurgeOldLogs();
        if (!AccountVault.Load())
            SetAccStatus(Loc.Tr("⚠ Không giải mã được file tài khoản (khác user/máy Windows).", "⚠ Cannot decrypt the account file (different Windows user/PC)."), true);
        RefreshAccountList();

        _monitor.PollCompleted += UpdateUI;
        _monitor.Start();
        _monitor.Poll();

        _loaded = true;
        Log(Loc.Tr($"Roblox RAM Manager v{AppInfo.VersionText} by {AppInfo.Author} khởi động thành công.", $"Roblox RAM Manager v{AppInfo.VersionText} by {AppInfo.Author} started successfully."));
        StartupUpdateCheck();

    }

    // ═══════════════════════════════════════════
    //  Navigation & Tab Switcher
    // ═══════════════════════════════════════════

    private void NavHome_Click(object sender, RoutedEventArgs e)
    {
        _selectedInstance = null;
        bool wasVisible = OverviewView.Visibility == Visibility.Visible;
        OverviewView.Visibility = Visibility.Visible;
        AccountDetailView.Visibility = Visibility.Collapsed;
        DiscordView.Visibility = Visibility.Collapsed;
        AccountsView.Visibility = Visibility.Collapsed;
        if (!wasVisible) FadeIn(OverviewView);

        SetNav(home: true, discord: false);
        HighlightActiveTab(null);
    }

    private void NavAccount_Click(object sender, RoutedEventArgs e)
    {
        _selectedInstance = null;
        bool wasVisible = AccountsView.Visibility == Visibility.Visible;
        OverviewView.Visibility = Visibility.Collapsed;
        AccountDetailView.Visibility = Visibility.Collapsed;
        DiscordView.Visibility = Visibility.Collapsed;
        AccountsView.Visibility = Visibility.Visible;
        if (!wasVisible) FadeIn(AccountsView);

        SetNav(home: false, discord: false, account: true);
        HighlightActiveTab(null);
    }

    private void NavDiscord_Click(object sender, RoutedEventArgs e)
    {
        _selectedInstance = null;
        bool wasVisible = DiscordView.Visibility == Visibility.Visible;
        OverviewView.Visibility = Visibility.Collapsed;
        AccountDetailView.Visibility = Visibility.Collapsed;
        DiscordView.Visibility = Visibility.Visible;
        AccountsView.Visibility = Visibility.Collapsed;
        if (!wasVisible) FadeIn(DiscordView);

        SetNav(home: false, discord: true);
        HighlightActiveTab(null);
    }

    private void ShowAccountDetail(RobloxInstanceVM inst)
    {
        _selectedInstance = inst;
        bool wasVisible = AccountDetailView.Visibility == Visibility.Visible;
        OverviewView.Visibility = Visibility.Collapsed;
        AccountDetailView.Visibility = Visibility.Visible;
        DiscordView.Visibility = Visibility.Collapsed;
        AccountsView.Visibility = Visibility.Collapsed;
        if (!wasVisible) FadeIn(AccountDetailView);

        SetNav(home: false, discord: false);

        AccDetailTitle.Text = $"Acc #{inst.Index}: {inst.WindowTitle}";
        AccDetailSubtitle.Text = $"PID: {inst.Pid} · PlaceId: {(inst.PlaceId > 0 ? inst.PlaceId.ToString() : "N/A")}";
        AccDetailStatus.Text = inst.IsResponding ? Loc.Tr("● Đang hoạt động", "● Running") : Loc.Tr("● Không phản hồi", "● Not responding");
        AccDetailStatus.Fg(inst.IsResponding ? "AccentPrimaryBrush" : "AccentDangerBrush");

        AccDetailUptime.Text = inst.UptimeDisplay;
        AccDetailStartTime.Text = Loc.Tr($"Bắt đầu chạy lúc: {inst.StartTime:HH:mm:ss dd/MM/yyyy}", $"Started at: {inst.StartTime:HH:mm:ss dd/MM/yyyy}");
        AccDetailRam.Text = inst.RamDisplay;
        AccDetailPeakRam.Text = Loc.Tr($"Đỉnh: {inst.PeakRamMB:F0} MB · Bộ nhớ riêng: {inst.PrivateMB:F0} MB", $"Peak: {inst.PeakRamMB:F0} MB · Private: {inst.PrivateMB:F0} MB");
        AccDetailCpu.Text = inst.CpuDisplay;
        AccDetailPri.Text = Loc.Tr($"Độ ưu tiên: {inst.Priority} · Giới hạn cứng CPU: {AppConfig.CpuCapPercent}%", $"Priority: {inst.Priority} · CPU hard cap: {AppConfig.CpuCapPercent}%");

        HighlightActiveTab(inst.Pid);
    }

    private void InstanceGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (InstanceGrid.SelectedItem is RobloxInstanceVM sel)
            ShowAccountDetail(sel);
    }

    private void SynchronizeTabs()
    {
        // TabBarPanel: Child 0 is always TabOverviewBtn
        // Retain TabOverviewBtn, sync instances tabs
        var existingButtons = TabBarPanel.Children.OfType<Button>().Where(b => b != TabOverviewBtn).ToList();
        var currentPids = _monitor.Instances.Select(x => x.Pid).ToHashSet();

        // Remove tabs of closed instances
        foreach (var btn in existingButtons)
        {
            if (btn.Tag is uint pid && !currentPids.Contains(pid))
            {
                TabBarPanel.Children.Remove(btn);
                if (_selectedInstance?.Pid == pid) NavHome_Click(this, new RoutedEventArgs());
            }
        }

        // Add or update tabs
        foreach (var inst in _monitor.Instances)
        {
            var btn = TabBarPanel.Children.OfType<Button>().FirstOrDefault(b => b.Tag is uint pid && pid == inst.Pid);
            string tabTitle = $"🎮 #{inst.Index} ({inst.UptimeDisplay})";

            if (btn == null)
            {
                var newBtn = new Button
                {
                    Tag = inst.Pid,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 7, 12, 7),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand,
                    Content = tabTitle,
                    FontSize = 11
                };
                newBtn.Fg("TextBrush");
                newBtn.Border("PanelBorderBrush");
                newBtn.Click += (_, _) => ShowAccountDetail(inst);
                TabBarPanel.Children.Add(newBtn);
            }
            else
            {
                newBtnContent(btn, tabTitle);
            }
        }

        HighlightActiveTab(_selectedInstance?.Pid);
    }

    private static void newBtnContent(Button btn, string title)
    {
        if (btn.Content as string != title)
            btn.Content = title;
    }

    private void HighlightActiveTab(uint? activePid)
    {
        bool overviewActive = activePid == null && OverviewView.Visibility == Visibility.Visible;
        TabOverviewBtn.Border(overviewActive ? "AccentPrimaryBrush" : "PanelBorderBrush");
        TabOverviewBtn.BgOrClear(overviewActive, "GlassActiveBrush");

        foreach (var b in TabBarPanel.Children.OfType<Button>().Where(x => x != TabOverviewBtn))
        {
            bool isMe = activePid != null && b.Tag is uint pid && pid == activePid;
            b.Border(isMe ? "AccentPrimaryBrush" : "PanelBorderBrush");
            b.BgOrClear(isMe, "GlassActiveBrush");
            b.FontWeight = isMe ? FontWeights.Bold : FontWeights.Normal;
        }
    }

    // ═══════════════════════════════════════════
    //  Per-Account Actions (Detail View)
    // ═══════════════════════════════════════════

    private void AccDetailOptimize_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedInstance == null) return;
        var cap = (uint)AppConfig.CpuCapPercent;
        NativeInterop.OptimizeProcess(_selectedInstance.Pid, NativeInterop.BELOW_NORMAL_PRIORITY_CLASS, 2);
        NativeInterop.CpuCapProcess(_selectedInstance.Pid, cap);
        NativeInterop.TrimSelf();
        Log(Loc.Tr($"[TỐI ƯU ACC] {_selectedInstance.WindowTitle} → Trim + BelowNormal + CPU≤{cap}%", $"[OPTIMIZE ACC] {_selectedInstance.WindowTitle} → Trim + BelowNormal + CPU≤{cap}%"));
        ShowAccountDetail(_selectedInstance);
    }

    private void AccDetailTrim_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedInstance == null) return;
        NativeInterop.TrimMemory(_selectedInstance.Pid);
        Log(Loc.Tr($"[DỌN RAM] Đã trim bộ nhớ {_selectedInstance.WindowTitle}", $"[TRIM RAM] Trimmed memory of {_selectedInstance.WindowTitle}"));
        ShowAccountDetail(_selectedInstance);
    }

    private void AccDetailBringFront_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedInstance == null) return;
        NativeInterop.BringToFront(_selectedInstance.Pid);
        Log(Loc.Tr($"[MÀN HÌNH] Đưa cửa sổ {_selectedInstance.WindowTitle} lên trước", $"[WINDOW] Brought {_selectedInstance.WindowTitle} to front"));
    }

    private void AccDetailKill_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedInstance == null) return;
        var title = _selectedInstance.WindowTitle;
        var pid = _selectedInstance.Pid;
        NativeInterop.KillProcess(pid);
        Log(Loc.Tr($"[TẮT ACC] Đã tắt tiến trình {title} (PID {pid})", $"[KILL ACC] Terminated {title} (PID {pid})"));
        NavHome_Click(sender, e);
    }

    // ═══════════════════════════════════════════
    //  Discord Webhook Integration
    // ═══════════════════════════════════════════

    private async void SendDiscordWebhook_Click(object sender, RoutedEventArgs e)
    {
        SaveDiscordConfig();
        DiscordStatusLabel.Text = Loc.Tr("⏳ Đang gửi thông số lên Discord...", "⏳ Sending stats to Discord...");
        bool ok; string msg;
        try { (ok, msg) = await DiscordNotifier.SendStatusAsync(_monitor, _crashRecovery); }
        catch (Exception ex) { ok = false; msg = ex.Message; }
        DiscordStatusLabel.Text = (ok ? "✅ " : "❌ ") + msg;
        DiscordStatusLabel.Fg(ok ? "AccentPrimaryBrush" : "AccentDangerBrush");
        Log($"[DISCORD] {msg}");
        if (ok) _lastDiscordSent = DateTime.Now;
    }

    private void SaveDiscordConfig_Click(object sender, RoutedEventArgs e)
    {
        SaveDiscordConfig();
        DiscordStatusLabel.Text = Loc.Tr("✅ Đã lưu cấu hình Discord thành công!", "✅ Discord settings saved!");
        DiscordStatusLabel.Fg("AccentPrimaryBrush");
    }

    private void SaveDiscordConfig()
    {
        AppConfig.DiscordWebhookUrl = DiscordUrlBox.Text.Trim();
        if (DiscordIntervalCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var mins))
            AppConfig.DiscordWebhookIntervalMin = mins;
        AppConfig.DiscordAlertOnCrash = DiscordCrashAlertChk.IsChecked == true;
        AppConfig.DiscordUserId = new string(DiscordUserIdBox.Text.Where(char.IsDigit).ToArray());
        AppConfig.Save();
    }

    private void AccManualClose_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        AppConfig.IgnoreManualClose = AccManualCloseChk.IsChecked == true;
        AppConfig.Save();
    }

    private async void DiscordTimer_Tick(object? sender, EventArgs e)
    {
        if (AppConfig.DiscordWebhookIntervalMin <= 0) return;
        if (string.IsNullOrWhiteSpace(AppConfig.DiscordWebhookUrl)) return;

        if (DateTime.Now - _lastDiscordSent >= TimeSpan.FromMinutes(AppConfig.DiscordWebhookIntervalMin))
        {
            _lastDiscordSent = DateTime.Now;
            try
            {
                var (ok, msg) = await DiscordNotifier.SendStatusAsync(_monitor, _crashRecovery);
                Log($"[DISCORD AUTO] {msg}");
            }
            catch (Exception ex) { Log($"[DISCORD AUTO] {ex.Message}"); }
        }
    }

    // ═══════════════════════════════════════════
    //  UI Update with Animations
    // ═══════════════════════════════════════════

    private void UpdateUI()
    {
        var s = _monitor.Stats;
        double barMax = Math.Max(80, RamBarFill.Parent is Grid g ? g.ActualWidth - 2 : 200);

        // RAM bar (animated)
        double ramTarget = barMax * Math.Min(s.MemoryLoadPercent, 100) / 100.0;
        AnimateWidth(RamBarFill, ramTarget);
        RamLabel.Text = s.RamDisplay;
        RamPercent.Text = $"{s.MemoryLoadPercent}%";
        RamPercent.Fg(s.IsHighRam ? "AccentDangerBrush" : "AccentPrimaryBrush");

        // CPU bar (animated)
        double cpuTarget = barMax * Math.Min(s.CpuPercent, 100) / 100.0;
        AnimateWidth(CpuBarFill, cpuTarget);
        CpuLabel.Text = $"Processors: {s.ProcessorCount}";
        CpuPercent.Text = s.CpuDisplay;
        CpuPercent.Fg(s.IsHighCpu ? "AccentDangerBrush" : "AccentTertiaryBrush");

        // Accounts
        ActiveAccLabel.Text = s.ActiveAccounts.ToString();
        CrashedAccLabel.Text = _crashRecovery.TotalCrashes.ToString();

        foreach (var inst in _monitor.Instances.ToList())
        {
            if (inst.PlaceId == 0) inst.PlaceId = _crashRecovery.CacheInstancePlaceId(inst.Pid);
            TrackPlace(inst);
        }

        StatusText.Text = s.TotalAccounts > 0
            ? Loc.Tr($"Đang theo dõi {s.TotalAccounts} tài khoản | RAM: {s.MemoryLoadPercent}% | CPU: {s.CpuPercent:F0}%", $"Monitoring {s.TotalAccounts} accounts | RAM: {s.MemoryLoadPercent}% | CPU: {s.CpuPercent:F0}%")
            : Loc.Tr("Chưa phát hiện Roblox nào. Đang chờ...", "No Roblox detected. Waiting...");

        // Refresh detail view if currently open
        if (_selectedInstance != null && AccountDetailView.Visibility == Visibility.Visible)
        {
            var live = _monitor.Instances.FirstOrDefault(x => x.Pid == _selectedInstance.Pid);
            if (live != null)
            {
                AccDetailRam.Text = live.RamDisplay;
                AccDetailCpu.Text = live.CpuDisplay;
                AccDetailPeakRam.Text = Loc.Tr($"Đỉnh: {live.PeakRamMB:F0} MB · Bộ nhớ riêng: {live.PrivateMB:F0} MB", $"Peak: {live.PeakRamMB:F0} MB · Private: {live.PrivateMB:F0} MB");
            }
        }

        EnforceRamCap();
        PrunePidCaches();

        // Sync tabs
        SynchronizeTabs();

        if (_autoMode) RunAutoMode();
    }

    private void AnimateWidth(FrameworkElement el, double target)
    {
        if (!_loaded) { el.Width = target; return; }
        var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        el.BeginAnimation(WidthProperty, anim);
    }

    // ═══════════════════════════════════════════
    //  Auto Mode
    // ═══════════════════════════════════════════

    private void RunAutoMode()
    {
        var load = _monitor.Stats.MemoryLoadPercent;
        var cap = (uint)AppConfig.CpuCapPercent;

        if (!_autoTrimTimer.IsEnabled && AppConfig.TrimIntervalSec > 0) _autoTrimTimer.Start();

        if (load >= RAM_CRITICAL)
        {
            foreach (var i in _monitor.Instances)
                if (i.PriorityClass != NativeInterop.IDLE_PRIORITY_CLASS)
                {
                    NativeInterop.OptimizeProcess(i.Pid, NativeInterop.IDLE_PRIORITY_CLASS, 1);
                    NativeInterop.CpuCapProcess(i.Pid, Math.Min(cap, 25));
                    Log($"[AUTO] RAM {load}% → {i.WindowTitle} → Idle + 1-core + CPU≤{Math.Min(cap, 25)}%");
                }
        }
        else if (load >= RAM_HIGH)
        {
            foreach (var i in _monitor.Instances.OrderByDescending(x => x.RamMB))
                if (i.PriorityClass != NativeInterop.BELOW_NORMAL_PRIORITY_CLASS && i.PriorityClass != NativeInterop.IDLE_PRIORITY_CLASS)
                {
                    NativeInterop.OptimizeProcess(i.Pid, NativeInterop.BELOW_NORMAL_PRIORITY_CLASS, 2);
                    NativeInterop.CpuCapProcess(i.Pid, cap);
                    Log($"[AUTO] RAM {load}% → {i.WindowTitle} → BelowNormal + 2-core + CPU≤{cap}%");
                }
        }
        else if (load <= RAM_NORMAL)
        {
            foreach (var i in _monitor.Instances)
                if (i.PriorityClass != NativeInterop.NORMAL_PRIORITY_CLASS)
                {
                    NativeInterop.SetPriority(i.Pid, NativeInterop.NORMAL_PRIORITY_CLASS);
                    NativeInterop.SetAffinity(i.Pid, NativeInterop.GetSystemAffinityMask());
                    NativeInterop.RemoveCpuCap(i.Pid);
                    Log(Loc.Tr($"[AUTO] RAM {load}% → {i.WindowTitle} → Normal (khôi phục)", $"[AUTO] RAM {load}% → {i.WindowTitle} → Normal (restored)"));
                }
        }
    }

    // ═══════════════════════════════════════════
    //  Crash Handler
    // ═══════════════════════════════════════════

    private readonly Dictionary<uint, DateTime> _lastPlaceCheck = new();

    /// <summary>Đọc log Roblox để biết acc đang ở game nào, lưu lại để lần sau/lúc bị văng vào lại đúng game đó.</summary>
    private async void TrackPlace(RobloxInstanceVM inst)
    {
        try
        {
            if (_lastPlaceCheck.TryGetValue(inst.Pid, out var t) && (DateTime.Now - t).TotalSeconds < 12) return;
            _lastPlaceCheck[inst.Pid] = DateTime.Now;
            var start = inst.StartTime;
            long place = await Task.Run(() => RobloxLogReader.FindPlaceId(start));
            if (place <= 0) return;
            inst.PlaceId = place;
            _crashRecovery.RememberPlace(inst.Pid, place);
            if (_pidAccount.TryGetValue(inst.Pid, out var accId))
            {
                var acc = AccountVault.Accounts.FirstOrDefault(a => a.Id == accId);
                if (acc != null && acc.LastPlaceId != place) { acc.LastPlaceId = place; AccountVault.Save(); }
            }
        }
        catch { }
    }

    /// <summary>Dọn dữ liệu theo PID của các tiến trình đã tắt (tránh phình bộ nhớ khi chạy lâu ngày).</summary>
    private void PrunePidCaches()
    {
        var alive = _monitor.Instances.Select(i => i.Pid).ToHashSet();
        foreach (var k in _lastPlaceCheck.Keys.Where(k => !alive.Contains(k)).ToList()) _lastPlaceCheck.Remove(k);
        foreach (var k in _lastRamTrim.Keys.Where(k => !alive.Contains(k)).ToList()) _lastRamTrim.Remove(k);
    }

    private static long ResolvePlace(AccountEntry acc) => acc.PlaceId > 0 ? acc.PlaceId : acc.LastPlaceId;

    private async void OnCrashed(RobloxInstanceVM crashed)
    {
        try { await OnCrashedCoreAsync(crashed); }
        catch (Exception ex) { try { Log($"[CRASH-HANDLER] {ex.GetType().Name}: {ex.Message}"); } catch { } }
    }

    private async Task OnCrashedCoreAsync(RobloxInstanceVM crashed)
    {
        Log($"[EXIT] PID {crashed.Pid} exit code: {(crashed.ExitCode.HasValue ? "0x" + crashed.ExitCode.Value.ToString("X") + $" ({crashed.ExitCode.Value})" : "?")}");

        // Bạn tự tay đóng game (nút X / End Task) → không làm gì, không mở lại, không báo Discord
        if (AppConfig.IgnoreManualClose && crashed.LooksManualClose)
        {
            _pidAccount.Remove(crashed.Pid);
            Log(Loc.Tr($"[ĐÓNG TAY] PID {crashed.Pid} do bạn tự đóng → không mở lại.", $"[MANUAL CLOSE] PID {crashed.Pid} closed by you → not reopening."));
            return;
        }

        bool accMapped = false, accOk = false;
        AccountEntry? recAcc = null;
        if (_crashRecovery.Enabled && _pidAccount.TryGetValue(crashed.Pid, out var accId))
        {
            _pidAccount.Remove(crashed.Pid);
            var acc = AccountVault.Accounts.FirstOrDefault(a => a.Id == accId);
            if (acc != null && acc.AutoReopen)
            {
                accMapped = true;
                recAcc = acc;
                // Ưu tiên game đang chơi lúc bị văng → game gần nhất đã lưu → Place ID cố định
                long place = crashed.PlaceId > 0 ? crashed.PlaceId : _crashRecovery.GetCachedPlaceId(crashed.Pid);
                if (place <= 0) place = acc.LastPlaceId;
                if (place <= 0) place = acc.PlaceId;
                accOk = await LaunchAccountAsync(acc, place);
            }
        }
        _crashRecovery.HandleCrash(crashed, accMapped, accOk);
        var last = _crashRecovery.CrashLog.LastOrDefault();
        if (last != null) Log($"[CRASH] {last.Summary}");

        bool hasWebhook = !string.IsNullOrWhiteSpace(AppConfig.DiscordWebhookUrl);
        if (hasWebhook && accMapped && !accOk && recAcc != null)
        {
            // Việc quan trọng: không mở lại được acc / link VIP sai → ping người dùng
            string title = _lastLaunchVipInvalid
                ? Loc.Tr("Link server VIP bị sai — chưa mở lại acc", "VIP server link is wrong — account not reopened")
                : Loc.Tr("Acc bị văng và KHÔNG mở lại được", "Account crashed and could NOT be reopened");
            string detail = Loc.Tr($"Tài khoản: **{recAcc.Username}** (PID {crashed.Pid})\nLý do: {_lastLaunchError}",
                                   $"Account: **{recAcc.Username}** (PID {crashed.Pid})\nReason: {_lastLaunchError}");
            await DiscordNotifier.SendAlertAsync(title, detail, ping: true);
        }
        else if (hasWebhook && AppConfig.DiscordAlertOnCrash)
        {
            await DiscordNotifier.SendStatusAsync(_monitor, _crashRecovery,
                Loc.Tr($"Tài khoản {crashed.WindowTitle} (PID {crashed.Pid}) bị văng/crash!", $"Account {crashed.WindowTitle} (PID {crashed.Pid}) crashed!"));
        }
    }

    // ═══════════════════════════════════════════
    //  Toolbar Actions
    // ═══════════════════════════════════════════

    private void AutoMode_Changed(object sender, RoutedEventArgs e)
    {
        if (ModeLabel == null) return;
        _autoMode = AutoModeChk.IsChecked == true;
        ModeLabel.Text = _autoMode ? "Auto" : "Manual";
        ModeLabel.Fg(_autoMode ? "AccentPrimaryBrush" : "AccentWarningBrush");
        AutoLabel.Fg(_autoMode ? "AccentPrimaryBrush" : "TextDimBrush");
        if (_autoMode && AppConfig.TrimIntervalSec > 0) _autoTrimTimer.Start();
        Log(_autoMode ? Loc.Tr("[MODE] Auto Bật", "[MODE] Auto On") : "[MODE] Manual");
    }

    private void OptimizeAll_Click(object sender, RoutedEventArgs e)
    {
        int ok = 0;
        var cap = (uint)AppConfig.CpuCapPercent;
        foreach (var i in _monitor.Instances)
        {
            var r = NativeInterop.OptimizeProcess(i.Pid, NativeInterop.BELOW_NORMAL_PRIORITY_CLASS, 2);
            NativeInterop.CpuCapProcess(i.Pid, cap);
            if (r.trim || r.priority) ok++;
        }
        NativeInterop.TrimSelf();
        if (AppConfig.TrimIntervalSec > 0) _autoTrimTimer.Start();
        Log(Loc.Tr($"[TỐI ƯU TOÀN BỘ] {ok}/{_monitor.Instances.Count} acc → BelowNormal + Trim + 2-core + CPU≤{cap}%", $"[OPTIMIZE ALL] {ok}/{_monitor.Instances.Count} accs → BelowNormal + Trim + 2-core + CPU≤{cap}%"));
    }

    private void TrimAll_Click(object sender, RoutedEventArgs e)
    {
        int ok = 0, fail = 0;
        foreach (var i in _monitor.Instances) if (NativeInterop.TrimMemory(i.Pid)) ok++; else fail++;
        Log(Loc.Tr($"[DỌN RAM TOÀN BỘ] Thành công:{ok} Thất bại:{fail}", $"[TRIM ALL] Success:{ok} Failed:{fail}"));
    }

    private void SetPriority_Click(object sender, RoutedEventArgs e)
    {
        if (InstanceGrid.SelectedItem is not RobloxInstanceVM sel) { Log(Loc.Tr("[CẢNH BÁO] Hãy chọn một tài khoản từ danh sách", "[WARNING] Select an account from the list")); return; }
        if (PriorityCombo.SelectedItem is not ComboBoxItem item) return;
        var pri = uint.Parse(item.Tag.ToString()!);
        Log(NativeInterop.SetPriority(sel.Pid, pri) ? $"[OK] {sel.WindowTitle} → {item.Content}" : $"[FAIL] PID {sel.Pid}");
    }

    private void RefreshBtn_Click(object sender, RoutedEventArgs e) { _monitor.Poll(); Log(Loc.Tr("[QUÉT TIẾN TRÌNH]", "[PROCESS SCAN]")); }

    private void CrashRecovery_Changed(object sender, RoutedEventArgs e)
    {
        if (LogText == null) return;
        _crashRecovery.Enabled = CrashRecoveryChk.IsChecked == true;
        AppConfig.CrashRecovery = _crashRecovery.Enabled;
        if (_loaded) AppConfig.Save();
        Log(_crashRecovery.Enabled ? Loc.Tr("[CRASH RECOVERY] Bật", "[CRASH RECOVERY] On") : Loc.Tr("[CRASH RECOVERY] Tắt", "[CRASH RECOVERY] Off"));
    }

    private void IntervalCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (IntervalCombo?.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var ms))
            _monitor.PollIntervalMs = ms;
    }

    // ═══════════════════════════════════════════
    //  Settings Panel with 360° Gear Animation
    // ═══════════════════════════════════════════

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isSettingsOpen) CloseSettings();
        else OpenSettings();
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e) => CloseSettings();
    private void SettingsBackdrop_Click(object sender, MouseButtonEventArgs e) => CloseSettings();

    private void OpenSettings()
    {
        _isSettingsOpen = true;
        SettingsOverlay.Visibility = Visibility.Visible;
        var anim = new DoubleAnimation(310, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        SettingsTranslate.BeginAnimation(TranslateTransform.XProperty, anim);

        // Gear rotation 360°
        var gearAnim = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        SettingsGearRotate.BeginAnimation(RotateTransform.AngleProperty, gearAnim);
        SettingsGearPath.FillRes("AccentPrimaryBrush");
    }

    private void CloseSettings()
    {
        _isSettingsOpen = false;
        var anim = new DoubleAnimation(0, 310, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        anim.Completed += (_, _) => SettingsOverlay.Visibility = Visibility.Collapsed;
        SettingsTranslate.BeginAnimation(TranslateTransform.XProperty, anim);

        // Gear rotation reverse 360 -> 0
        var gearAnim = new DoubleAnimation(360, 0, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        SettingsGearRotate.BeginAnimation(RotateTransform.AngleProperty, gearAnim);
        SettingsGearPath.FillRes("TextDimBrush");
    }

    // ═══════════════════════════════════════════
    //  Help Panel (slides in from the left) + EN/VI toggle
    // ═══════════════════════════════════════════

    private void HelpBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isHelpOpen) CloseHelp();
        else OpenHelp();
    }

    private void CloseHelp_Click(object sender, RoutedEventArgs e) => CloseHelp();
    private void HelpBackdrop_Click(object sender, MouseButtonEventArgs e) => CloseHelp();

    private void OpenHelp()
    {
        _isHelpOpen = true;
        HelpOverlay.Visibility = Visibility.Visible;
        var anim = new DoubleAnimation(-340, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        HelpTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
        HelpIcon.FillRes("AccentPrimaryBrush");
    }

    private void CloseHelp()
    {
        _isHelpOpen = false;
        var anim = new DoubleAnimation(0, -340, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        anim.Completed += (_, _) => HelpOverlay.Visibility = Visibility.Collapsed;
        HelpTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
        HelpIcon.FillRes("TextDimBrush");
    }

    private void LangVi_Click(object sender, RoutedEventArgs e) => SetLanguage("vi");
    private void LangEn_Click(object sender, RoutedEventArgs e) => SetLanguage("en");
    private void SetLangVi_Click(object sender, RoutedEventArgs e) => SetLanguage("vi");
    private void SetLangEn_Click(object sender, RoutedEventArgs e) => SetLanguage("en");

    // ═══════════════════════════════════════════
    //  Ngôn ngữ VI / EN
    // ═══════════════════════════════════════════

    private void SetLanguage(string lang)
    {
        if (Loc.Lang == lang && _loaded) { RefreshLangButtons(); return; }
        Loc.Lang = lang;
        AppConfig.Language = lang;
        if (_loaded) AppConfig.Save();
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        bool was = _loaded;
        _loaded = false; // tránh handler SelectionChanged lưu cấu hình khi ta làm mới ComboBox
        try
        {
            Loc.Apply(WindowRoot);

            // ComboBox hiển thị bản sao nội dung item đang chọn -> chọn lại để làm mới
            foreach (var cb in new[] { TrimIntervalCombo, PollCombo, DiscordIntervalCombo, IntervalCombo, PriorityCombo })
            {
                int i = cb.SelectedIndex;
                cb.SelectedIndex = -1;
                cb.SelectedIndex = i;
            }

            // Help panel
            HelpContentVi.Visibility = Loc.IsEn ? Visibility.Collapsed : Visibility.Visible;
            HelpContentEn.Visibility = Loc.IsEn ? Visibility.Visible : Visibility.Collapsed;
            HelpTitleLabel.Text = Loc.Tr("TRỢ GIÚP", "HELP");

            RamCapLabel.Text = RamCapText(AppConfig.RamCapMB);
            RefreshAbout();
            RefreshLangButtons();

            // Chi tiết acc đang mở
            if (_selectedInstance != null)
            {
                var inst = _selectedInstance;
                AccDetailStatus.Text = inst.IsResponding ? Loc.Tr("● Đang hoạt động", "● Running") : Loc.Tr("● Không phản hồi", "● Not responding");
                AccDetailStartTime.Text = Loc.Tr($"Bắt đầu chạy lúc: {inst.StartTime:HH:mm:ss dd/MM/yyyy}", $"Started at: {inst.StartTime:HH:mm:ss dd/MM/yyyy}");
                AccDetailPeakRam.Text = Loc.Tr($"Đỉnh: {inst.PeakRamMB:F0} MB · Bộ nhớ riêng: {inst.PrivateMB:F0} MB", $"Peak: {inst.PeakRamMB:F0} MB · Private: {inst.PrivateMB:F0} MB");
                AccDetailPri.Text = Loc.Tr($"Độ ưu tiên: {inst.Priority} · Giới hạn cứng CPU: {AppConfig.CpuCapPercent}%", $"Priority: {inst.Priority} · CPU hard cap: {AppConfig.CpuCapPercent}%");
            }
        }
        finally { _loaded = was; }
        if (_loaded) Log(Loc.Tr("[NGÔN NGỮ] Tiếng Việt", "[LANGUAGE] English"));
    }

    private void RefreshLangButtons()
    {
        LangViBtn.BgOrClear(!Loc.IsEn, "GlassActiveBrush");
        LangEnBtn.BgOrClear(Loc.IsEn, "GlassActiveBrush");
        SetLangViBtn.BgOrClear(!Loc.IsEn, "GlassActiveBrush");
        SetLangEnBtn.BgOrClear(Loc.IsEn, "GlassActiveBrush");
    }

    private static string AppVersion => AppInfo.VersionText;

    // ═══════════════════════════════════════════
    //  Tự cập nhật (GitHub Releases)
    // ═══════════════════════════════════════════

    private bool _updateBusy;

    private async void StartupUpdateCheck()
    {
        try
        {
            await Task.Delay(4000);
            if (AppConfig.AutoUpdateCheck) await CheckUpdateAsync(false);
        }
        catch { }
    }

    private void AuthorDiscord_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppInfo.AuthorDiscordUrl) { UseShellExecute = true }); }
        catch { }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        try { await CheckUpdateAsync(true); } catch { }
    }

    private void Changelog_Click(object sender, RoutedEventArgs e) => new ChangelogDialog { Owner = this }.ShowDialog();

    private void AutoUpdate_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        AppConfig.AutoUpdateCheck = AutoUpdateChk.IsChecked == true;
        AppConfig.Save();
    }

    private async Task CheckUpdateAsync(bool manual)
    {
        if (_updateBusy) return;
        _updateBusy = true;
        try
        {
            if (manual) UpdateStatusLabel.Text = Loc.Tr("⏳ Đang kiểm tra...", "⏳ Checking...");
            var (info, ok, msg) = await Updater.CheckAsync();
            if (info != null)
            {
                UpdateStatusLabel.Text = Loc.Tr($"🆕 Có bản mới v{info.Version.ToString(3)}", $"🆕 New version v{info.Version.ToString(3)} available");
                Log(Loc.Tr($"[CẬP NHẬT] Có bản mới v{info.Version.ToString(3)}", $"[UPDATE] New version v{info.Version.ToString(3)} available"));
                if (!IsLoaded || !IsVisible) return; // cửa sổ chính đã đóng/chưa hiện → bỏ qua, tránh lỗi Owner
                new UpdateDialog(info) { Owner = this }.ShowDialog();
            }
            else if (manual)
            {
                UpdateStatusLabel.Text = (ok ? "✅ " : "⚠ ") + msg;
            }
        }
        finally { _updateBusy = false; }
    }

    private void RefreshAbout()
    {
        if (!_updateBusy)
            UpdateStatusLabel.Text = Loc.Tr($"Phiên bản hiện tại: v{AppVersion}", $"Current version: v{AppVersion}");
        RefreshEffectLabel();
        AboutText.Text = $"Roblox RAM Manager v{AppVersion}\nby {AppInfo.Author} · Aurora Glass Edition\n\n" +
            Loc.Tr("Hỗ trợ tối ưu RAM, CPU, giới hạn core, phát hiện crash và thông báo qua Discord Webhook bot.",
                   "Optimizes RAM, CPU, core limits, detects crashes and notifies via Discord Webhook bot.");
    }

    // ═══════════════════════════════════════════
    //  Tài khoản Roblox (mã hoá) + tự mở lại khi văng
    // ═══════════════════════════════════════════

    private readonly Dictionary<uint, string> _pidAccount = new();
    private bool _accSuppress;

    private static string Mask(string u) =>
        string.IsNullOrEmpty(u) ? "" : (u.Length <= 2 ? "**" : u[..2] + new string('*', Math.Min(u.Length - 2, 6)));

    private static long ParsePlace(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text ?? "", @"\d{4,}");
        return m.Success && long.TryParse(m.Value, out var id) ? id : 0;
    }

    private void SetAccStatus(string text, bool error)
    {
        AccStatusLabel.Text = text;
        AccStatusLabel.Fg(error ? "AccentDangerBrush" : "AccentPrimaryBrush");
    }

    private AccountEntry? SelectedAccount()
    {
        var id = (AccountList.SelectedItem as ListBoxItem)?.Tag as string;
        return id == null ? null : AccountVault.Accounts.FirstOrDefault(a => a.Id == id);
    }

    private void RefreshAccountList(string? selectId = null)
    {
        _accSuppress = true;
        AccountList.Items.Clear();
        foreach (var a in AccountVault.Accounts)
        {
            var item = new ListBoxItem { Content = a.Username + (string.IsNullOrEmpty(a.Cookie) ? "" : "   ✓"), Tag = a.Id };
            AccountList.Items.Add(item);
            if (a.Id == selectId) AccountList.SelectedItem = item;
        }
        _accSuppress = false;
        FillAccountFields();
    }

    private void FillAccountFields()
    {
        var a = SelectedAccount();
        AccUserBox.Text = a?.Username ?? "";
        AccPlaceBox.Text = a != null && a.PlaceId > 0 ? a.PlaceId.ToString() : "";
        AccAutoChk.IsChecked = a?.AutoReopen ?? true;
        AccVipBox.Text = a?.VipLink ?? "";
        AccPassBox.Password = "";
        AccCookieBox.Password = "";
        SelectClientInCombo(a?.PreferredClientId ?? "");
    }

    /// <summary>Quét máy tìm client Roblox (Roblox gốc, Bloxstrap, Fishstrap, Voidstrap) và đổ vào ComboBox chọn client.</summary>
    private void RescanClients()
    {
        string? keepId = (AccClientCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        _clients = ClientManager.Detect();

        AccClientCombo.Items.Clear();
        AccClientCombo.Items.Add(new ComboBoxItem { Content = Loc.Tr("Mặc định hệ thống (client đang đặt mở link roblox-player://)", "System default (whichever client handles roblox-player:// links)"), Tag = "" });
        foreach (var c in _clients)
            AccClientCombo.Items.Add(new ComboBoxItem { Content = "✓ " + c.ToString(), Tag = c.Id });

        if (_clients.Count == 0)
        {
            AccClientStatusLabel.Text = Loc.Tr(
                "Không tìm thấy Roblox/Bloxstrap/Fishstrap/Voidstrap nào đã cài cho user này. Dùng \"Mặc định hệ thống\" hoặc cài rồi bấm Quét lại.",
                "Found no installed Roblox/Bloxstrap/Fishstrap/Voidstrap for this user. Use \"System default\" or install one and rescan.");
            AccClientStatusLabel.Fg("AccentWarningBrush");
        }
        else
        {
            AccClientStatusLabel.Text = Loc.Tr($"Đã tìm thấy {_clients.Count} client: {string.Join(", ", _clients.Select(c => c.DisplayName))}.",
                                                $"Found {_clients.Count} client(s): {string.Join(", ", _clients.Select(c => c.DisplayName))}.");
            AccClientStatusLabel.Fg("TextDimBrush");
        }

        SelectClientInCombo(keepId ?? "");
    }

    private void SelectClientInCombo(string clientId)
    {
        foreach (var obj in AccClientCombo.Items)
            if (obj is ComboBoxItem ci && (ci.Tag as string) == clientId)
            { AccClientCombo.SelectedItem = ci; return; }
        AccClientCombo.SelectedIndex = 0; // không thấy (vd client đã gỡ) → rơi về Mặc định hệ thống
    }

    private void AccClientRescan_Click(object sender, RoutedEventArgs e) => RescanClients();

    private void AccountList_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_accSuppress) return;
        FillAccountFields();
    }

    private void AccNew_Click(object sender, RoutedEventArgs e)
    {
        AccountList.SelectedIndex = -1;
        FillAccountFields();
        SetAccStatus("", false);
    }

    private async void AccSave_Click(object sender, RoutedEventArgs e)
    {
        if (AccountVault.LoadFailed)
        {
            SetAccStatus(Loc.Tr("Không giải mã được file tài khoản cũ (khác user/máy Windows). Không ghi đè để tránh mất dữ liệu.",
                                "Cannot decrypt the old account file (different Windows user/PC). Not overwriting to avoid data loss."), true);
            return;
        }
        var user = AccUserBox.Text.Trim();
        if (user.Length == 0) { SetAccStatus(Loc.Tr("Nhập tên đăng nhập.", "Enter a username."), true); return; }

        var vipText = AccVipBox.Text.Trim();
        if (vipText.Length > 0 && !VipServer.TryParse(vipText, out _, out var vipErr))
        { SetAccStatus(Loc.Tr("❌ Link server VIP sai: ", "❌ Invalid VIP server link: ") + vipErr, true); return; }

        var acc = SelectedAccount();
        if (acc == null)
        {
            acc = AccountVault.Accounts.FirstOrDefault(a => a.Username.Equals(user, StringComparison.OrdinalIgnoreCase));
            if (acc == null)
            {
                if (AccPassBox.Password.Length == 0 && AccCookieBox.Password.Length == 0)
                { SetAccStatus(Loc.Tr("Nhập mật khẩu cho acc mới.", "Enter a password for the new account."), true); return; }
                acc = new AccountEntry();
                AccountVault.Accounts.Add(acc);
            }
        }
        acc.Username = user;
        if (AccPassBox.Password.Length > 0) { acc.Password = AccPassBox.Password; if (AccCookieBox.Password.Length == 0) acc.Cookie = ""; }
        if (AccCookieBox.Password.Length > 0) acc.Cookie = AccCookieBox.Password.Trim();
        acc.PlaceId = ParsePlace(AccPlaceBox.Text);
        acc.AutoReopen = AccAutoChk.IsChecked == true;
        acc.VipLink = vipText;
        acc.PreferredClientId = (AccClientCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

        if (!AccountVault.Save()) { SetAccStatus(Loc.Tr("Lưu thất bại.", "Save failed."), true); return; }
        RefreshAccountList(acc.Id);
        Log(Loc.Tr($"[TÀI KHOẢN] Đã lưu {Mask(user)}", $"[ACCOUNT] Saved {Mask(user)}"));

        // Đăng nhập luôn ngay khi lưu để lần sau chỉ cần bấm Mở
        SetAccStatus(Loc.Tr("⏳ Đã lưu, đang đăng nhập...", "⏳ Saved, signing in..."), false);
        var (ok, msg) = await RobloxLauncher.EnsureLoginAsync(acc, BrowserLoginAsync);
        RefreshAccountList(acc.Id);
        if (!ok) { SetAccStatus("⚠ " + msg, true); return; }

        string vipNote = "";
        if (vipText.Length > 0 && VipServer.TryParse(vipText, out var vip, out _))
        {
            var (chk, cmsg) = await RobloxLauncher.CheckVipAsync(acc.Cookie, vip.PlaceId, vip.LinkCode);
            vipNote = chk switch
            {
                VipCheck.Valid => Loc.Tr(" Link VIP hợp lệ ✓", " VIP link OK ✓"),
                VipCheck.Invalid => Loc.Tr(" ⚠ Link VIP bị Roblox từ chối: ", " ⚠ VIP link rejected: ") + cmsg,
                _ => Loc.Tr(" (Chưa xác nhận được link VIP, sẽ kiểm tra lại khi mở)", " (VIP link not confirmed yet, will re-check on launch)")
            };
            if (chk == VipCheck.Invalid) { SetAccStatus("⚠" + vipNote, true); return; }
        }
        SetAccStatus(Loc.Tr("✅ Đã lưu & đăng nhập. Bấm Mở để vào acc.", "✅ Saved & signed in. Press Open to launch.") + vipNote, false);
    }

    /// <summary>Roblox đòi captcha/2FA → mở cửa sổ đăng nhập tự điền sẵn user/mk, lấy cookie xong tự đóng.</summary>
    private Task<bool> BrowserLoginAsync(AccountEntry acc)
    {
        SetAccStatus(Loc.Tr("Roblox yêu cầu xác minh — hãy hoàn tất trong cửa sổ vừa mở (chỉ 1 lần).",
                            "Roblox requires verification — complete it in the window that just opened (one time only)."), false);
        var win = new RobloxLoginWindow(acc.Username, acc.Password) { Owner = this };
        bool ok = win.ShowDialog() == true && !string.IsNullOrEmpty(win.Cookie);
        if (ok) { acc.Cookie = win.Cookie!; AccountVault.Save(); }
        return Task.FromResult(ok);
    }

    private void AccDelete_Click(object sender, RoutedEventArgs e)
    {
        var acc = SelectedAccount();
        if (acc == null) return;
        if (Dlg.Show(Loc.Tr($"Xoá tài khoản {acc.Username}?", $"Delete account {acc.Username}?"), "Roblox Manager",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        AccountVault.Accounts.Remove(acc);
        AccountVault.Save();
        RefreshAccountList();
        SetAccStatus(Loc.Tr("Đã xoá.", "Deleted."), false);
    }

    private async void AccLaunch_Click(object sender, RoutedEventArgs e)
    {
        var acc = SelectedAccount();
        if (acc == null) { SetAccStatus(Loc.Tr("Chọn một tài khoản đã lưu.", "Select a saved account."), true); return; }
        long place = ParsePlace(AccPlaceBox.Text);
        if (place <= 0) place = ResolvePlace(acc);
        try { await LaunchAccountAsync(acc, place); }
        catch (Exception ex) { LaunchFail(acc, ex.Message); }
    }

    private async void AccountList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var acc = SelectedAccount();
        if (acc == null) return;
        try { await LaunchAccountAsync(acc, ResolvePlace(acc)); }
        catch (Exception ex) { LaunchFail(acc, ex.Message); }
    }

    private async void AccLaunchAll_Click(object sender, RoutedEventArgs e)
    {
        var list = AccountVault.Accounts.ToList();
        if (list.Count == 0) { SetAccStatus(Loc.Tr("Chưa có tài khoản nào.", "No accounts saved."), true); return; }
        foreach (var a in list)
        {
            try { await LaunchAccountAsync(a, ResolvePlace(a)); }
            catch (Exception ex) { LaunchFail(a, ex.Message); }
            await Task.Delay(1500); // phần chờ Roblox lên thật sự nằm trong LaunchAccountAsync (cổng khởi chạy)
        }
    }

    private string _lastLaunchError = "";
    private bool _lastLaunchVipInvalid;

    private bool LaunchFail(AccountEntry acc, string msg, bool vipInvalid = false)
    {
        _lastLaunchError = msg;
        _lastLaunchVipInvalid = vipInvalid;
        SetAccStatus("❌ " + msg, true);
        Log($"[ACCOUNT] {Mask(acc.Username)}: {msg}");
        return false;
    }

    // Mỗi lần chỉ mở 1 acc: acc sau chỉ được mở khi acc trước đã thật sự lên (hoặc hết thời gian chờ).
    // Tránh nhiều lệnh mở chồng nhau (Mở tất cả, tự mở lại khi văng, bấm tay) làm bootstrapper hỏi/đóng Roblox đang chạy.
    private readonly SemaphoreSlim _launchGate = new(1, 1);

    private async Task<bool> LaunchAccountAsync(AccountEntry acc, long place)
    {
        await _launchGate.WaitAsync();
        try { return await LaunchAccountCoreAsync(acc, place); }
        finally { _launchGate.Release(); }
    }

    private async Task<bool> LaunchAccountCoreAsync(AccountEntry acc, long place)
    {
        _lastLaunchError = "";
        _lastLaunchVipInvalid = false;
        SetAccStatus(Loc.Tr($"⏳ Đang mở {Mask(acc.Username)}...", $"⏳ Opening {Mask(acc.Username)}..."), false);

        // Acc này có chọn riêng một client (Roblox/Bloxstrap/Fishstrap/Voidstrap) → kiểm tra nhanh
        // TRƯỚC khi đăng nhập/mở, để nếu client bị gỡ/hỏng thì báo lỗi ngay, không mất công chờ.
        ClientInfo? client = null;
        if (!string.IsNullOrEmpty(acc.PreferredClientId))
        {
            client = _clients.FirstOrDefault(c => c.Id == acc.PreferredClientId);
            if (client == null)
                return LaunchFail(acc, Loc.Tr(
                    $"Client đã chọn cho acc này không còn thấy trên máy (có thể đã gỡ cài đặt). Bấm \"🔄 Quét lại\" rồi chọn client khác.",
                    $"The client selected for this account is no longer found on this PC (may have been uninstalled). Click \"🔄 Rescan\" and pick another client."));

            var (preOk, preMsg) = ClientManager.PreflightCheck(client);
            if (!preOk) return LaunchFail(acc, preMsg);
        }

        // Có link server VIP → kiểm tra link trước, sai thì KHÔNG vào (tránh vào nhầm server)
        string? vipCode = null;
        if (!string.IsNullOrWhiteSpace(acc.VipLink))
        {
            if (!VipServer.TryParse(acc.VipLink, out var vip, out var perr))
                return LaunchFail(acc, Loc.Tr("Link server VIP sai: ", "Invalid VIP server link: ") + perr, vipInvalid: true);

            var (lok, lmsg) = await RobloxLauncher.EnsureLoginAsync(acc, BrowserLoginAsync);
            if (!lok) return LaunchFail(acc, lmsg);

            var (chk, cmsg) = await RobloxLauncher.CheckVipAsync(acc.Cookie, vip.PlaceId, vip.LinkCode);
            if (chk == VipCheck.Invalid)
                return LaunchFail(acc, cmsg, vipInvalid: true);
            if (chk == VipCheck.Unknown)
                Log($"[VIP] {Mask(acc.Username)}: {cmsg} " + Loc.Tr("Vẫn thử vào.", "Trying anyway."));

            place = vip.PlaceId;
            vipCode = vip.LinkCode;
        }

        var before = _monitor.Instances.Select(i => i.Pid).ToHashSet();
        var (ok, msg) = await RobloxLauncher.LaunchAsync(acc, place, BrowserLoginAsync, vipCode, client);
        RefreshAccountList(acc.Id);
        if (!ok) return LaunchFail(acc, msg);
        SetAccStatus(Loc.Tr($"⏳ Đã gửi lệnh mở {Mask(acc.Username)}, đang chờ Roblox lên...", $"⏳ Launch sent for {Mask(acc.Username)}, waiting for Roblox to start..."), false);
        Log($"[ACCOUNT] {Mask(acc.Username)}: {msg}");

        bool appeared = await MapNewPidAsync(acc.Id, before);
        if (appeared)
        {
            SetAccStatus("✅ " + Loc.Tr("Roblox đã lên.", "Roblox is up.") + (vipCode != null ? Loc.Tr(" (server VIP)", " (VIP server)") : ""), false);
            await Task.Delay(4000); // để client vừa mở ổn định rồi mới cho phép mở acc tiếp theo
        }
        else
        {
            string warn = Loc.Tr("Roblox chưa lên sau 90 giây. Nếu có hộp thoại hỏi \"Roblox is currently running...\" của Voidstrap/Bloxstrap thì hãy bật Multi-instance trong cài đặt của nó (xem hướng dẫn).",
                                 "Roblox did not appear within 90 seconds. If a Voidstrap/Bloxstrap prompt \"Roblox is currently running...\" is waiting, enable multi-instance in its settings (see the guide).");
            SetAccStatus("⚠ " + warn, true);
            Log($"[ACCOUNT] {Mask(acc.Username)}: {warn}");
        }
        return true;
    }

    /// <summary>Chờ tiến trình Roblox mới (tối đa 90 giây) và gán nó cho acc → để biết acc nào bị văng. Trả về false nếu hết giờ mà chưa thấy.</summary>
    private async Task<bool> MapNewPidAsync(string accId, HashSet<uint> before)
    {
        for (int i = 0; i < 60; i++)
        {
            await Task.Delay(1500);
            var fresh = _monitor.Instances.FirstOrDefault(x => !before.Contains(x.Pid) && !_pidAccount.ContainsKey(x.Pid));
            if (fresh != null) { _pidAccount[fresh.Pid] = accId; return true; }
        }
        return false;
    }

    // ═══════════════════════════════════════════
    //  Thư mục dữ liệu
    // ═══════════════════════════════════════════

    private void OpenDataDir_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start("explorer.exe", DataStore.Dir ?? AppDomain.CurrentDomain.BaseDirectory); } catch { }
    }

    private void ChangeDataDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DataFolderDialog(DataStore.Dir) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var target = dlg.SelectedPath;
        if (string.Equals(target.TrimEnd('\\'), DataStore.Dir?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            DataStore.CopyAllTo(target);
            DataStore.Set(target);
            AppConfig.Save();
            if (!AccountVault.LoadFailed) AccountVault.Save();
            DataPathLabel.Text = DataStore.Dir;
            Log(Loc.Tr($"[DỮ LIỆU] Đã chuyển sang {target} (bản cũ vẫn còn, bạn có thể xoá tay)", $"[DATA] Moved to {target} (old copy kept, delete manually if you want)"));
        }
        catch (Exception ex)
        {
            Dlg.Show(ex.Message, "Roblox Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string RamCapText(int mb) =>
        mb <= 0 ? Loc.Tr("Tắt", "Off") : (mb >= 1024 ? $"{mb / 1024.0:0.##} GB" : $"{mb} MB");

    private void RamCapSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RamCapLabel == null) return;
        int val = (int)RamCapSlider.Value;
        RamCapLabel.Text = RamCapText(val);
        AppConfig.RamCapMB = val;
        if (_loaded) AppConfig.Save();
    }

    private readonly Dictionary<uint, DateTime> _lastRamTrim = new();

    /// <summary>Acc nào vượt giới hạn RAM thì tự dọn (trim). Không dùng hard limit để Roblox không bị crash.</summary>
    private void EnforceRamCap()
    {
        int cap = AppConfig.RamCapMB;
        if (cap <= 0) return;
        var now = DateTime.Now;
        foreach (var i in _monitor.Instances)
        {
            if (i.RamMB <= cap) continue;
            if (_lastRamTrim.TryGetValue(i.Pid, out var t) && (now - t).TotalSeconds < 5) continue;
            _lastRamTrim[i.Pid] = now;
            NativeInterop.TrimMemory(i.Pid);
            Log(Loc.Tr($"[GIỚI HẠN RAM] {i.WindowTitle} vượt {RamCapText(cap)} ({i.RamDisplay}) → đã dọn",
                       $"[RAM LIMIT] {i.WindowTitle} exceeded {RamCapText(cap)} ({i.RamDisplay}) → trimmed"));
        }
    }

    private void CpuCapSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CpuCapLabel == null) return;
        var val = (int)CpuCapSlider.Value;
        CpuCapLabel.Text = $"{val}%";
        AppConfig.CpuCapPercent = val;
        if (_loaded) AppConfig.Save();
    }

    private void TrimInterval_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (TrimIntervalCombo?.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var sec))
        {
            AppConfig.TrimIntervalSec = sec;
            if (sec > 0)
            {
                _autoTrimTimer.Interval = TimeSpan.FromSeconds(sec);
                if (_autoMode || _autoTrimTimer.IsEnabled) _autoTrimTimer.Start();
            }
            else
            {
                _autoTrimTimer.Stop();
            }
            if (_loaded) AppConfig.Save();
        }
    }

    private void PollCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (PollCombo?.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var ms))
        {
            _monitor.PollIntervalMs = ms;
            AppConfig.PollIntervalMs = ms;
            if (_loaded) AppConfig.Save();
        }
    }

    private void BuildThemeButtons()
    {
        ThemeList.Items.Clear();
        foreach (var t in ThemeManager.All)
        {
            bool cur = t.Id == ThemeManager.Current;
            var btn = new Button
            {
                Content = t.Label,
                Tag = t.Id,
                FontSize = 12,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(1),
                FontWeight = cur ? FontWeights.Bold : FontWeights.Normal
            };
            btn.BgOrClear(cur, "GlassActiveBrush");
            btn.Fg(cur ? "AccentPrimaryBrush" : "TextBrush");
            btn.Border(cur ? "AccentPrimaryBrush" : "PanelBorderBrush");
            btn.Click += ThemeBtn_Click;
            ThemeList.Items.Add(btn);
        }
    }

    private void ThemeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            ThemeManager.Apply(id);
            AppConfig.Theme = id;
            AppConfig.Save();
            ThemeLabel.Text = id;
            BuildThemeButtons();
            RefreshEffectLabel();
            SetAurora(AppConfig.AuroraAnimated && WindowState != WindowState.Minimized);
            Log(Loc.Tr($"[THEME] Chuyển chủ đề: {id}", $"[THEME] Switched theme: {id}"));
        }
    }

    // ═══════════════════════════════════════════
    //  Window chrome (thanh tiêu đề tuỳ chỉnh)
    // ═══════════════════════════════════════════

    private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxBtn_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowRoot == null) return;

        if (WindowState == WindowState.Maximized)
        {
            // Cửa sổ không viền khi phóng to bị tràn ra mép → bù lại
            WindowRoot.Margin = SystemParameters.WindowResizeBorderThickness;
            MaxGlyph.Data = Geometry.Parse("M2,0 H10 V8 M0,2 H8 V10 H0 Z");
        }
        else
        {
            WindowRoot.Margin = new Thickness(0);
            MaxGlyph.Data = Geometry.Parse("M0,0 H10 V10 H0 Z");
        }

        // Thu nhỏ xuống taskbar → dừng hiệu ứng nền để tiết kiệm CPU
        if (_loaded)
            SetAurora(WindowState != WindowState.Minimized && AppConfig.AuroraAnimated);
    }

    // ═══════════════════════════════════════════
    //  Aurora background animation
    // ═══════════════════════════════════════════

    private void Aurora_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        AppConfig.AuroraAnimated = AuroraChk.IsChecked == true;
        AppConfig.Save();
        SetAurora(AppConfig.AuroraAnimated);
        Log(AppConfig.AuroraAnimated ? Loc.Tr("[HIỆU ỨNG] Bật hiệu ứng chuyển động", "[FX] Animation on") : Loc.Tr("[HIỆU ỨNG] Tắt hiệu ứng chuyển động (nền tĩnh)", "[FX] Animation off (static background)"));
    }

    private readonly List<UIElement> _fireflies = new();
    private bool? _auroraState;
    private string? _auroraTheme;

    /// <summary>Hiệu ứng riêng của từng chủ đề (tốc độ nền, kiểu hạt, màu hạt).</summary>
    private sealed record FxSpec(double Speed, double OpacityMul, string Mode, int Count, double MinSize, double MaxSize,
        double MinDur, double MaxDur, double Sway, double MaxOp, string[] Brushes, string Shape,
        bool Shooting = false, bool Shrink = false);

    private static FxSpec GetFx(string id) => id switch
    {
        // Cosmos: sao nhấp nháy + sao băng
        "cosmos" => new FxSpec(1.15, 1.0, "twinkle", 40, 1.5, 4.0, 2.5, 7.0, 0, 0.9,
                       new[] { "TextBrush", "AccentSecondaryBrush", "AccentTertiaryBrush" }, "dot", Shooting: true),
        // Hell: nền bập bùng như lửa + tàn lửa bay nhanh, nhỏ dần
        "hell" => new FxSpec(0.55, 0.2, "rise", 26, 2.0, 6.0, 5.0, 11.0, 22, 0.85,
                       new[] { "AccentSecondaryBrush", "AccentTertiaryBrush", "AccentPrimaryBrush" }, "dot", Shrink: true),
        // Love: trái tim bay lên, lắc nhẹ
        "love" => new FxSpec(1.0, 1.0, "rise", 14, 14, 30, 14, 26, 40, 0.6,
                       new[] { "AccentPrimaryBrush", "AccentSecondaryBrush", "AccentTertiaryBrush" }, "heart"),
        // Dark: nền chậm, bụi sáng mờ trôi lên
        "dark" => new FxSpec(1.5, 1.3, "rise", 14, 2.0, 4.0, 28, 50, 40, 0.30,
                       new[] { "AccentPrimaryBrush", "AccentTertiaryBrush" }, "dot"),
        // White: bong bóng rỗng bay lên
        "white" => new FxSpec(1.2, 1.0, "rise", 12, 14, 40, 20, 36, 30, 0.55,
                       new[] { "AccentPrimaryBrush", "AccentSecondaryBrush" }, "ring"),
        // Aurora: đom đóm xanh
        _ => new FxSpec(1.0, 1.0, "rise", 18, 3.0, 8.0, 16, 34, 28, 0.55,
                       new[] { "AccentPrimaryBrush", "AccentSecondaryBrush" }, "dot"),
    };

    /// <summary>Tên hiển thị của ô bật/tắt hiệu ứng theo chủ đề đang dùng (Animated Aurora / Hell / Love...).</summary>
    private void RefreshEffectLabel()
    {
        string id = ThemeManager.Current;
        string name = id.Length > 0 ? char.ToUpper(id[0]) + id[1..] : "Aurora";
        AuroraChk.Content = Loc.Tr($"{name} chuyển động (tắt để nhẹ máy hơn)", $"Animated {name} (turn off to be lighter)");
    }

    /// <summary>ON: nền + hạt chuyển động theo chủ đề. OFF: dừng hẳn mọi animation → đúng 1 khung hình tĩnh.</summary>
    private void SetAurora(bool on)
    {
        string theme = ThemeManager.Current;
        if (_auroraState == on && _auroraTheme == theme) return; // phóng to/thu nhỏ cửa sổ không làm nền chạy lại từ đầu
        _auroraState = on;
        _auroraTheme = theme;
        var fx = GetFx(theme);

        void Drift(IAnimatable target, DependencyProperty prop, double from, double to, double seconds, bool opacity = false, bool raw = false)
        {
            if (!on) { target.BeginAnimation(prop, null); return; }
            double sec = raw ? seconds : seconds * fx.Speed * (opacity ? fx.OpacityMul : 1);
            var anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(sec))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Timeline.SetDesiredFrameRate(anim, 30); // chuyển động chậm: 30fps nhìn như 60fps nhưng nhẹ gấp đôi
            target.BeginAnimation(prop, anim);
        }

        // Vầng lớn thứ nhất (dưới trái)
        Drift(AurA_Move, TranslateTransform.XProperty, -40, 190, 19);
        Drift(AurA_Move, TranslateTransform.YProperty, 30, -90, 23);
        Drift(AurA_Scale, ScaleTransform.ScaleXProperty, 0.92, 1.18, 17);
        Drift(AurA_Scale, ScaleTransform.ScaleYProperty, 1.1, 0.9, 21);
        Drift(AurA, OpacityProperty, 0.75, 1.0, 13, opacity: true);

        // Vầng lớn thứ hai (trên phải)
        Drift(AurB_Move, TranslateTransform.XProperty, 40, -170, 22);
        Drift(AurB_Move, TranslateTransform.YProperty, -20, 120, 18);
        Drift(AurB_Scale, ScaleTransform.ScaleXProperty, 1.15, 0.9, 20);
        Drift(AurB_Scale, ScaleTransform.ScaleYProperty, 0.9, 1.15, 25);
        Drift(AurB, OpacityProperty, 1.0, 0.7, 15, opacity: true);

        // Vầng giữa (thở nhẹ)
        Drift(AurC_Move, TranslateTransform.XProperty, -90, 100, 27);
        Drift(AurC_Move, TranslateTransform.YProperty, 60, -70, 24);
        Drift(AurC_Scale, ScaleTransform.ScaleXProperty, 0.85, 1.2, 26);
        Drift(AurC_Scale, ScaleTransform.ScaleYProperty, 1.15, 0.85, 22);
        Drift(AurC, OpacityProperty, 0.6, 1.0, 11, opacity: true);

        // Hai dải ribbon uốn lượn
        Drift(AurR1_Rot, RotateTransform.AngleProperty, -30, -12, 16);
        Drift(AurR1_Move, TranslateTransform.YProperty, -50, 70, 20);
        Drift(AurR1_Move, TranslateTransform.XProperty, -60, 60, 24);
        Drift(AurR1, OpacityProperty, 0.55, 1.0, 9, opacity: true);

        Drift(AurR2_Rot, RotateTransform.AngleProperty, 26, 10, 18);
        Drift(AurR2_Move, TranslateTransform.YProperty, 50, -60, 21);
        Drift(AurR2_Move, TranslateTransform.XProperty, 70, -70, 26);
        Drift(AurR2, OpacityProperty, 1.0, 0.5, 11, opacity: true);

        // Mascot bồng bềnh + chấm Online nhấp nháy: chỉ chuyển động khi hiệu ứng bật
        Drift(MascotFloat, TranslateTransform.YProperty, 0, -14, 3.2, raw: true);
        Drift(LiveDot, OpacityProperty, 1, 0.3, 1.2, raw: true);

        SetParticles(on ? fx : null);
    }

    /// <summary>Hạt riêng cho từng chủ đề: đom đóm / sao + sao băng / tàn lửa / trái tim / bụi sáng / bong bóng.</summary>
    private void SetParticles(FxSpec? fx)
    {
        foreach (var f in _fireflies) AuroraCanvas.Children.Remove(f);
        _fireflies.Clear();
        if (fx == null) return;

        var rnd = new Random(7);
        void Start(IAnimatable t, DependencyProperty p, AnimationTimeline a)
        {
            Timeline.SetDesiredFrameRate(a, 30);
            t.BeginAnimation(p, a);
        }
        DoubleAnimationUsingKeyFrames Keys(double dur, TimeSpan begin, params (double v, double pct)[] k)
        {
            var kf = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(dur), RepeatBehavior = RepeatBehavior.Forever, BeginTime = begin };
            foreach (var (v, pct) in k) kf.KeyFrames.Add(new LinearDoubleKeyFrame(v, KeyTime.FromPercent(pct)));
            return kf;
        }

        for (int i = 0; i < fx.Count; i++)
        {
            double size = fx.MinSize + rnd.NextDouble() * (fx.MaxSize - fx.MinSize);
            double dur = fx.MinDur + rnd.NextDouble() * (fx.MaxDur - fx.MinDur);
            string brush = fx.Brushes[i % fx.Brushes.Length];
            var begin = TimeSpan.FromSeconds(-rnd.NextDouble() * dur); // mỗi hạt một pha khác nhau

            FrameworkElement el;
            switch (fx.Shape)
            {
                case "heart":
                    var tb = new TextBlock { Text = "♥", FontSize = size, IsHitTestVisible = false };
                    tb.SetResourceReference(TextBlock.ForegroundProperty, brush);
                    el = tb;
                    break;
                case "ring":
                    var ring = new Shapes.Ellipse { Width = size, Height = size, StrokeThickness = 1.4, IsHitTestVisible = false };
                    ring.SetResourceReference(Shapes.Shape.StrokeProperty, brush);
                    el = ring;
                    break;
                default:
                    var dot = new Shapes.Ellipse { Width = size, Height = size, IsHitTestVisible = false };
                    dot.SetResourceReference(Shapes.Shape.FillProperty, brush);
                    el = dot;
                    break;
            }

            el.Opacity = 0;
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            var scale = new ScaleTransform(1, 1);
            var rot = new RotateTransform();
            var tt = new TranslateTransform();
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(rot);
            group.Children.Add(tt);
            el.RenderTransform = group;
            Canvas.SetLeft(el, rnd.NextDouble() * 960 + 20);

            if (fx.Mode == "twinkle")
            {
                Canvas.SetTop(el, rnd.NextDouble() * 680 + 10);
                Start(el, OpacityProperty, new DoubleAnimation(0.08, fx.MaxOp, TimeSpan.FromSeconds(dur))
                {
                    AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = begin,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
            }
            else
            {
                Canvas.SetTop(el, 700);
                Start(tt, TranslateTransform.YProperty, new DoubleAnimation(0, -740, TimeSpan.FromSeconds(dur))
                { RepeatBehavior = RepeatBehavior.Forever, BeginTime = begin });
                Start(tt, TranslateTransform.XProperty, new DoubleAnimation(-fx.Sway, fx.Sway, TimeSpan.FromSeconds(5 + rnd.NextDouble() * 5))
                {
                    AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = begin,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
                Start(el, OpacityProperty, Keys(dur, begin, (0, 0), (fx.MaxOp, 0.15), (fx.MaxOp, 0.85), (0, 1)));

                if (fx.Shape == "heart")
                    Start(rot, RotateTransform.AngleProperty, new DoubleAnimation(-16, 16, TimeSpan.FromSeconds(3 + rnd.NextDouble() * 3))
                    {
                        AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = begin,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    });
                if (fx.Shrink)
                {
                    var sh = new DoubleAnimation(1.0, 0.25, TimeSpan.FromSeconds(dur)) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = begin };
                    Start(scale, ScaleTransform.ScaleXProperty, sh);
                    Start(scale, ScaleTransform.ScaleYProperty, sh.Clone());
                }
            }

            AuroraCanvas.Children.Add(el);
            _fireflies.Add(el);
        }

        // Cosmos: 2 sao băng thỉnh thoảng vụt qua
        if (fx.Shooting)
        {
            var col = ((SolidColorBrush)FindResource("TextBrush")).Color;
            for (int s = 0; s < 2; s++)
            {
                double dur = 13 + s * 4;
                var begin = TimeSpan.FromSeconds(-(3 + s * 5));
                var streak = new Shapes.Rectangle
                {
                    Width = 110, Height = 2, RadiusX = 1, RadiusY = 1, Opacity = 0, IsHitTestVisible = false,
                    Fill = new LinearGradientBrush(Color.FromArgb(0, col.R, col.G, col.B), col, 0)
                };
                var tt = new TranslateTransform();
                var group = new TransformGroup();
                group.Children.Add(new RotateTransform(28));
                group.Children.Add(tt);
                streak.RenderTransformOrigin = new Point(0.5, 0.5);
                streak.RenderTransform = group;
                Canvas.SetLeft(streak, 80 + s * 380);
                Canvas.SetTop(streak, 30 + s * 60);

                Start(tt, TranslateTransform.XProperty, Keys(dur, begin, (0, 0), (480, 0.06), (480, 1)));
                Start(tt, TranslateTransform.YProperty, Keys(dur, begin, (0, 0), (255, 0.06), (255, 1)));
                Start(streak, OpacityProperty, Keys(dur, begin, (0, 0), (0.9, 0.01), (0, 0.06), (0, 1)));

                AuroraCanvas.Children.Add(streak);
                _fireflies.Add(streak);
            }
        }
    }

    private static void FadeIn(UIElement el)
    {
        var move = new TranslateTransform(0, 14);
        el.RenderTransform = move;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        el.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(340)) { EasingFunction = ease });
    }

    private void SetNav(bool home, bool discord, bool account = false)
    {
        NavHomeIcon.FillRes(home ? "AccentPrimaryBrush" : "TextDimBrush");
        NavDiscordIcon.FillRes(discord ? "AccentPrimaryBrush" : "TextDimBrush");
        NavAccountIcon.FillRes(account ? "AccentPrimaryBrush" : "TextDimBrush");
        NavHomeBtn.BgOrClear(home, "GlassActiveBrush");
        NavDiscordBtn.BgOrClear(discord, "GlassActiveBrush");
        NavAccountBtn.BgOrClear(account, "GlassActiveBrush");
    }

    private void Log(string msg)
    {
        if (LogText == null) return;
        LogText.Text = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + LogText.Text;
        if (LogText.Text.Length > 4000) LogText.Text = LogText.Text[..3000];
        try
        {
            if (DataStore.Dir != null)
                System.IO.File.AppendAllText(System.IO.Path.Combine(DataStore.LogsDir, $"app_{DateTime.Now:yyyy-MM-dd}.log"),
                    $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        }
        catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        NativeInterop.Diagnostic -= _onNativeDiagnostic;
        _monitor.PollCompleted -= UpdateUI;
        _monitor.Stop();
        _clockTimer.Stop();
        _selfTrimTimer.Stop();
        _autoTrimTimer.Stop();
        _discordTimer.Stop();
        // Hoàn tác tối ưu khi thoát: trả ưu tiên/core/giới hạn CPU về bình thường để Roblox không bị kẹt ở mức đã giới hạn.
        try
        {
            foreach (var i in _monitor.Instances)
            {
                NativeInterop.SetPriority(i.Pid, NativeInterop.NORMAL_PRIORITY_CLASS);
                NativeInterop.SetAffinity(i.Pid, NativeInterop.GetSystemAffinityMask());
            }
            NativeInterop.ReleaseAllCaps();
        }
        catch { }
        base.OnClosed(e);
    }
}
