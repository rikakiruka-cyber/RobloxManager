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
    // Số lần quét liên tiếp mà PID đọc không ra thông tin thật (WorkingSet = 0) → nghi process ma
    private readonly Dictionary<uint, int> _ghostPolls = new();
    private const int GhostPollsToDrop = 3;
    // Roblox thật luôn >> 2 MB. Process ma (văng nhưng còn sót PID) thường chỉ còn vài trăm KB, KHÔNG nhất thiết đúng 0 byte
    private const ulong GhostMaxWsBytes = 2UL * 1024 * 1024;
    private static readonly Dictionary<uint, DateTime> _procStartTimeCache = new();

    /// <summary>Hỏi thẳng Windows: PID này còn sống thật không? (đã thoát / không còn tồn tại / PID bị tái sử dụng bởi process khác)</summary>
    private static bool IsProcessDead(uint pid, DateTime? knownStart, System.Diagnostics.Process? heldProc = null)
    {
        try
        {
            if (heldProc != null)
            {
                try { if (heldProc.HasExited) return true; } catch { return true; }
            }

            if (knownStart.HasValue)
            {
                // Dùng cache StartTime nếu có, tránh tạo Process object liên tục trên luồng UI
                if (!_procStartTimeCache.TryGetValue(pid, out var st))
                {
                    using var pr = System.Diagnostics.Process.GetProcessById((int)pid);
                    if (pr.HasExited) return true;
                    st = pr.StartTime;
                    _procStartTimeCache[pid] = st;
                }
                if (Math.Abs((st - knownStart.Value).TotalSeconds) > 5) return true;
                return false;
            }

            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.HasExited;
        }
        catch (ArgumentException) { return true; }   // không còn process nào mang PID này
        catch { return false; }                      // lỗi quyền... → không kết luận bừa
    }

    public ProcessMonitor()
    {
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_pollIntervalMs)
        };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start() => _timer.Start();
    public void Stop()
    {
        _timer.Stop();
        foreach (var p in _procs.Values)
        {
            try { p.Dispose(); } catch { }
        }
        _procs.Clear();
        _procStartTimeCache.Clear();
    }

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
                // PROCESS MA: app giữ handle tới process (Process.EnableRaisingEvents / Job giới hạn CPU) nên
                // Windows vẫn liệt kê PID đó trong snapshot dù process đã thoát hẳn → Task Manager không thấy,
                // nhưng app hiện thành 1 acc "Running" 0 MB mãi mãi. Hỏi thẳng handle đang giữ: đã thoát thì
                // coi như đã mất (không đưa vào currentPids → phần phát hiện văng bên dưới sẽ xử lý + nhả handle).
                if (_procs.TryGetValue(p.ProcessId, out var held))
                {
                    bool exited = false;
                    try { exited = held.HasExited; } catch { }
                    if (exited) continue;
                }

                var existing = FindByPid(p.ProcessId);

                // Native layer không đọc được info thật (OpenProcess lỗi — thường do process đang
                // dở quá trình thoát/crash, hoặc còn sót PID "ma" trong snapshot của Windows) sẽ trả
                // WorkingSetBytes = 0. Một RobloxPlayerBeta.exe thật đang sống không bao giờ có working
                // set = 0 byte, nên đây là dấu hiệu đáng tin để lọc "process ma" — trước đây app vẫn
                // thêm nó vào danh sách với 0 MB/0% và để nguyên mãi (Task Manager không thấy vì bản
                // thân Windows cũng gần như đã dọn xong, chỉ còn sót entry trong snapshot).
                // Mở rộng: RAM < 2 MB + KHÔNG có cửa sổ + CPU ~0 cũng là process ma (hay gặp: Roblox vừa văng,
                // còn sót PID với vài trăm KB nên "0 MB" nhưng không phải đúng 0 byte → bản cũ bỏ lọt).
                var procInfo = p;
                bool noWindow = string.IsNullOrWhiteSpace(procInfo.WindowTitle);
                bool looksGhost = procInfo.WorkingSetBytes == 0
                                  || (noWindow && procInfo.WorkingSetBytes < GhostMaxWsBytes && procInfo.CpuPercent < 0.5);

                if (existing == null)
                {
                    if (looksGhost)
                    {
                        // Kiểm tra xem process có thật sự sống không trước khi coi là ma (phòng trường hợp native engine bị từ chối quyền do anti-cheat)
                        if (!IsProcessDead(procInfo.ProcessId, null))
                        {
                            try
                            {
                                using var proc = System.Diagnostics.Process.GetProcessById((int)procInfo.ProcessId);
                                if (!proc.HasExited && proc.WorkingSet64 > 0)
                                {
                                    procInfo.WorkingSetBytes = (ulong)proc.WorkingSet64;
                                    procInfo.PeakWorkingSetBytes = (ulong)proc.PeakWorkingSet64;
                                    procInfo.PrivateBytes = (ulong)proc.PrivateMemorySize64;
                                    looksGhost = false;
                                }
                            }
                            catch { }
                        }
                    }
                    if (looksGhost) continue; // chưa từng thấy & không đọc được info thật → bỏ qua hẳn
                    currentPids.Add(procInfo.ProcessId);
                    var vm = new RobloxInstanceVM();
                    try
                    {
                        var proc = System.Diagnostics.Process.GetProcessById((int)procInfo.ProcessId);
                        vm.StartTime = proc.StartTime;
                        _procStartTimeCache[procInfo.ProcessId] = vm.StartTime;
                        try { proc.EnableRaisingEvents = true; } catch { } // giữ handle để đọc exit code khi game tắt
                        _procs[procInfo.ProcessId] = proc;
                    }
                    catch
                    {
                        vm.StartTime = DateTime.Now;
                        _procStartTimeCache[procInfo.ProcessId] = vm.StartTime;
                    }
                    vm.Update(procInfo);
                    vm.Index = Instances.Count + 1;
                    Instances.Add(vm);
                }
                else
                {
                    _procs.TryGetValue(procInfo.ProcessId, out var heldProc);
                    if (looksGhost)
                    {
                        // Ghost liên tục nhiều lần quét → thật sự đã chết (không phải nhiễu thoáng qua) → bỏ khỏi danh sách.
                        int gp = _ghostPolls.TryGetValue(procInfo.ProcessId, out var g0) ? g0 + 1 : 1;
                        _ghostPolls[procInfo.ProcessId] = gp;
                        bool dead = IsProcessDead(procInfo.ProcessId, existing.StartTime, heldProc);
                        if (dead || gp >= GhostPollsToDrop)
                        {
                            AppLog.Write($"[ProcessMonitor] Bỏ PID {procInfo.ProcessId} (process ma): RAM={procInfo.WorkingSetBytes / 1024.0:F0} KB, title=\"{procInfo.WindowTitle}\", OS xác nhận đã chết={dead}, lần quét ma={gp}");
                            continue;
                        }
                    }
                    else
                    {
                        _ghostPolls.Remove(procInfo.ProcessId);
                        // PID cũ bị cấp lại cho Roblox mới → coi process cũ đã mất (để báo văng + mở lại), lần quét sau sẽ nhận process mới
                        if (IsProcessDead(procInfo.ProcessId, existing.StartTime, heldProc)) continue;
                    }
                    currentPids.Add(procInfo.ProcessId);
                    if (looksGhost)
                    {
                        // Đã từng đọc info thật trước đó, lần này lỗi → có thể chỉ là nhiễu tạm thời lúc
                        // process đang thoát. Không ghi đè RAM/CPU về 0 (tránh nhấp nháy sai); vẫn coi là
                        // "đang có" để không báo văng nhầm — nếu process thật sự đã mất hẳn, lần quét sau
                        // nó sẽ không còn trong snapshot nữa và cơ chế phát hiện văng bên dưới sẽ tự bắt được.
                        existing.RefreshUptime();
                    }
                    else
                    {
                        existing.Update(procInfo);
                        existing.RefreshUptime();
                    }
                }
            }

            // Detect crashed instances (was known, now gone)
            foreach (var pid in _knownPids)
            {
                if (!currentPids.Contains(pid))
                {
                    var crashed = FindByPid(pid);
                    NativeInterop.ReleaseJob(pid);
                    _procStartTimeCache.Remove(pid);
                    if (_procs.Remove(pid, out var pr))
                    {
                        try { if (crashed != null && pr.HasExited) crashed.ExitCode = pr.ExitCode; } catch { }
                        try { pr.Dispose(); } catch { }
                    }
                    if (crashed != null)
                    {
                        // Handler lỗi thì bỏ qua: nếu để ném ra ngoài, _knownPids không được cập nhật →
                        // vòng quét sau lại báo "văng" lần nữa → acc bị mở lại nhiều lần.
                        try { OnInstanceCrashed?.Invoke(crashed); } catch (Exception ex) { AppLog.Write("[ProcessMonitor] OnInstanceCrashed lỗi: " + ex.Message); }
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
            foreach (var gk in _ghostPolls.Keys.Where(k => !currentPids.Contains(k) && !procs.Any(x => x.ProcessId == k)).ToList())
                _ghostPolls.Remove(gk);

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
