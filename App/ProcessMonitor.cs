// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RobloxRAMManager;

/// <summary>
/// Real-time process monitor. Polls NativeEngine (or managed fallback) every N seconds.
/// </summary>
public sealed class ProcessMonitor
{
    public ObservableCollection<RobloxInstanceVM> Instances { get; } = new();
    public SystemStatsVM Stats { get; } = new();

    public DateTime ManagerStartTime { get; } = DateTime.Now;
    public TimeSpan ManagerUptime => DateTime.Now - ManagerStartTime;
    public string ManagerUptimeDisplay
    {
        get
        {
            var u = ManagerUptime;
            if (u.TotalHours >= 24)
                return $"{(int)u.TotalDays}d {u.Hours:D2}h {u.Minutes:D2}m";
            if (u.TotalHours >= 1)
                return $"{(int)u.TotalHours:D2}h {u.Minutes:D2}m {u.Seconds:D2}s";
            return $"{u.Minutes:D2}m {u.Seconds:D2}s";
        }
    }

    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private int _pollIntervalMs = 2000;

    public int PollIntervalMs
    {
        get => _pollIntervalMs;
        set { _pollIntervalMs = Math.Max(500, value); _timer.Interval = TimeSpan.FromMilliseconds(_pollIntervalMs); }
    }

    public event Action<RobloxInstanceVM>? OnInstanceCrashed;

    // Track known PIDs to detect crashes
    private readonly HashSet<uint> _knownPids = new();
    private readonly Dictionary<uint, System.Diagnostics.Process> _procs = new();

    public ProcessMonitor()
    {
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_pollIntervalMs)
        };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    /// <summary>Raised once after each poll finished (UI thread).</summary>
    public event Action? PollCompleted;
    private bool _polling;

    public void Poll() => _ = PollAsync();

    private async Task PollAsync()
    {
        if (_polling) return;
        _polling = true;
        try
        {
            // Native calls (incl. "is the window hung?" checks) run off the UI thread so the app never freezes.
            var (sys, procs) = await Task.Run(() => (NativeInterop.GetSystemResources(), NativeInterop.EnumRobloxProcesses()));
            Apply(sys, procs);
        }
        catch { }
        finally { _polling = false; }
        try { PollCompleted?.Invoke(); } catch { }
    }

    private void Apply(NativeInterop.NativeSystemInfo sys, NativeInterop.NativeProcessInfo[] procs)
    {
        try
        {
            // System stats
            Stats.TotalRamBytes = sys.TotalPhysicalBytes;
            Stats.AvailableRamBytes = sys.AvailablePhysicalBytes;
            Stats.MemoryLoadPercent = sys.MemoryLoadPercent;
            Stats.CpuPercent = sys.TotalCpuPercent;
            Stats.ProcessorCount = sys.ProcessorCount;

            // Roblox instances
            var currentPids = new HashSet<uint>();

            foreach (var p in procs)
            {
                currentPids.Add(p.ProcessId);

                var existing = FindByPid(p.ProcessId);
                if (existing != null)
                {
                    existing.Update(p);
                    existing.RefreshUptime();
                }
                else
                {
                    var vm = new RobloxInstanceVM();
                    try
                    {
                        var proc = System.Diagnostics.Process.GetProcessById((int)p.ProcessId);
                        vm.StartTime = proc.StartTime;
                        try { proc.EnableRaisingEvents = true; } catch { } // giữ handle để đọc exit code khi game tắt
                        _procs[p.ProcessId] = proc;
                    }
                    catch
                    {
                        vm.StartTime = DateTime.Now;
                    }
                    vm.Update(p);
                    vm.Index = Instances.Count + 1;
                    Instances.Add(vm);
                }
            }

            // Detect crashed instances (was known, now gone)
            foreach (var pid in _knownPids)
            {
                if (!currentPids.Contains(pid))
                {
                    var crashed = FindByPid(pid);
                    NativeInterop.ReleaseJob(pid);
                    if (_procs.Remove(pid, out var pr))
                    {
                        try { if (crashed != null && pr.HasExited) crashed.ExitCode = pr.ExitCode; } catch { }
                        try { pr.Dispose(); } catch { }
                    }
                    if (crashed != null)
                    {
                        OnInstanceCrashed?.Invoke(crashed);
                        Instances.Remove(crashed);
                    }
                }
            }

            // Remove instances whose PID is no longer active
            for (int i = Instances.Count - 1; i >= 0; i--)
            {
                if (!currentPids.Contains(Instances[i].Pid))
                    Instances.RemoveAt(i);
            }

            // Re-index
            for (int i = 0; i < Instances.Count; i++)
                Instances[i].Index = i + 1;

            _knownPids.Clear();
            foreach (var pid in currentPids)
                _knownPids.Add(pid);

            Stats.TotalAccounts = Instances.Count;
            Stats.ActiveAccounts = Instances.Count(x => x.IsResponding);
        }
        catch
        {
            // ponytail: swallow polling errors, don't crash the manager
        }
    }

    private RobloxInstanceVM? FindByPid(uint pid)
        => Instances.FirstOrDefault(x => x.Pid == pid);
}

/* ── View Models ── */

public class RobloxInstanceVM : INotifyPropertyChanged
{
    private int _index;
    private uint _pid;
    private string _windowTitle = "";
    private double _ramMB;
    private double _peakRamMB;
    private double _privateMB;
    private double _cpuPercent;
    private string _priority = "Normal";
    private uint _priorityClass;
    private bool _isResponding = true;
    private long _placeId;
    private DateTime _startTime = DateTime.Now;

    public int Index          { get => _index;       set => Set(ref _index, value); }
    public uint Pid           { get => _pid;         set => Set(ref _pid, value); }
    public string WindowTitle { get => _windowTitle; set => Set(ref _windowTitle, value); }
    public double RamMB       { get => _ramMB;       set { Set(ref _ramMB, value); Notify(nameof(RamDisplay)); } }
    public double PeakRamMB   { get => _peakRamMB;  set => Set(ref _peakRamMB, value); }
    public double PrivateMB   { get => _privateMB;  set => Set(ref _privateMB, value); }
    public double CpuPercent  { get => _cpuPercent;  set { Set(ref _cpuPercent, value); Notify(nameof(CpuDisplay)); } }
    public string Priority    { get => _priority;    set => Set(ref _priority, value); }
    public uint PriorityClass { get => _priorityClass; set => Set(ref _priorityClass, value); }
    public bool IsResponding  { get => _isResponding; set { Set(ref _isResponding, value); Notify(nameof(StatusDisplay)); Notify(nameof(StatusColor)); } }
    public long PlaceId       { get => _placeId;     set => Set(ref _placeId, value); }
    /// <summary>Exit code của tiến trình khi tắt (null = không đọc được).</summary>
    public int? ExitCode { get; set; }
    /// <summary>0 = đóng bình thường (nút X), 1 = End Task. Mã khác (vd 0xC0000005) = văng/crash.</summary>
    public bool LooksManualClose => ExitCode is 0 or 1;
    public DateTime StartTime { get => _startTime;   set { Set(ref _startTime, value); Notify(nameof(UptimeDisplay)); } }

    public TimeSpan Uptime => DateTime.Now - StartTime;
    public string UptimeDisplay
    {
        get
        {
            var u = Uptime;
            if (u.TotalHours >= 24)
                return $"{(int)u.TotalDays}d {u.Hours:D2}h {u.Minutes:D2}m";
            if (u.TotalHours >= 1)
                return $"{(int)u.TotalHours:D2}h {u.Minutes:D2}m {u.Seconds:D2}s";
            return $"{u.Minutes:D2}m {u.Seconds:D2}s";
        }
    }

    public void RefreshUptime() => Notify(nameof(UptimeDisplay));

    public string RamDisplay => RamMB >= 1024 ? $"{RamMB / 1024:F1} GB" : $"{RamMB:F0} MB";
    public string CpuDisplay => $"{CpuPercent:F1}%";
    public string StatusDisplay => IsResponding ? "● Running" : "● Not Responding";
    public string StatusColor => IsResponding ? "#00FF88" : "#FF0040";

    public void Update(NativeInterop.NativeProcessInfo p)
    {
        Pid = p.ProcessId;
        WindowTitle = string.IsNullOrWhiteSpace(p.WindowTitle) ? $"Roblox ({p.ProcessId})" : p.WindowTitle;
        RamMB = p.WorkingSetBytes / (1024.0 * 1024.0);
        PeakRamMB = p.PeakWorkingSetBytes / (1024.0 * 1024.0);
        PrivateMB = p.PrivateBytes / (1024.0 * 1024.0);
        CpuPercent = Math.Round(p.CpuPercent, 1);
        PriorityClass = p.PriorityClass;
        Priority = NativeInterop.PriorityName(p.PriorityClass);
        IsResponding = p.Responding;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value)) { field = value; Notify(n); }
    }
}

public class SystemStatsVM : INotifyPropertyChanged
{
    private ulong _totalRamBytes;
    private ulong _availableRamBytes;
    private uint _memoryLoadPercent;
    private double _cpuPercent;
    private uint _processorCount;
    private int _totalAccounts;
    private int _activeAccounts;

    public ulong TotalRamBytes { get => _totalRamBytes; set { Set(ref _totalRamBytes, value); Notify(nameof(RamDisplay)); Notify(nameof(UsedRamGB)); Notify(nameof(TotalRamGB)); } }
    public ulong AvailableRamBytes { get => _availableRamBytes; set { Set(ref _availableRamBytes, value); Notify(nameof(RamDisplay)); Notify(nameof(UsedRamGB)); } }
    public uint MemoryLoadPercent { get => _memoryLoadPercent; set { Set(ref _memoryLoadPercent, value); Notify(nameof(IsHighRam)); } }
    public double CpuPercent { get => _cpuPercent; set { Set(ref _cpuPercent, value); Notify(nameof(CpuDisplay)); Notify(nameof(IsHighCpu)); } }
    public uint ProcessorCount { get => _processorCount; set => Set(ref _processorCount, value); }
    public int TotalAccounts { get => _totalAccounts; set { Set(ref _totalAccounts, value); Notify(nameof(CrashedAccounts)); } }
    public int ActiveAccounts { get => _activeAccounts; set { Set(ref _activeAccounts, value); Notify(nameof(CrashedAccounts)); } }

    public double UsedRamGB => (TotalRamBytes > AvailableRamBytes ? TotalRamBytes - AvailableRamBytes : 0UL) / (1024.0 * 1024 * 1024);
    public double TotalRamGB => TotalRamBytes / (1024.0 * 1024 * 1024);
    public string RamDisplay => $"{UsedRamGB:F1} / {TotalRamGB:F1} GB";
    public string CpuDisplay => $"{CpuPercent:F1}%";
    public int CrashedAccounts => Math.Max(0, TotalAccounts - ActiveAccounts);
    public bool IsHighRam => MemoryLoadPercent > 80;
    public bool IsHighCpu => CpuPercent > 80;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value)) { field = value; Notify(n); }
    }
}
