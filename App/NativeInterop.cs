// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.IO;
using System.Runtime.InteropServices;

namespace RobloxRAMManager;

public static class NativeInterop
{
    /* ── Structs matching C++ ── */

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct NativeProcessInfo
    {
        public uint ProcessId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string WindowTitle;
        public ulong WorkingSetBytes;
        public ulong PeakWorkingSetBytes;
        public ulong PrivateBytes;
        public double CpuPercent;
        public uint PriorityClass;
        [MarshalAs(UnmanagedType.Bool)]
        public bool Responding;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NativeSystemInfo
    {
        public ulong TotalPhysicalBytes;
        public ulong AvailablePhysicalBytes;
        public uint MemoryLoadPercent;
        public double TotalCpuPercent;
        public uint ProcessorCount;
    }

    /* ── NativeEngine.dll imports ── */
    private const string DLL = "NativeEngine.dll";
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern void NE_GetSystemResources(ref NativeSystemInfo info);
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NE_EnumRobloxProcesses([Out] NativeProcessInfo[] buffer, int maxCount);
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool NE_SetProcessPriority(uint pid, uint priorityClass);
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool NE_TrimProcessMemory(uint pid);

    /* ── Direct Win32 P/Invoke ── */

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength; public uint dwMemoryLoad;
        public ulong ullTotalPhys; public ulong ullAvailPhys;
        public ulong ullTotalPageFile; public ulong ullAvailPageFile;
        public ulong ullTotalVirtual; public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    [DllImport("kernel32.dll")]
    private static extern void GetSystemInfo(out SYSTEM_INFO si);
    [DllImport("kernel32.dll")]
    private static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint h);
    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(nint hProcess, nint min, nint max);
    [DllImport("kernel32.dll")]
    private static extern bool SetPriorityClass(nint hProcess, uint cls);
    [DllImport("kernel32.dll")]
    private static extern bool SetProcessAffinityMask(nint hProcess, nint mask);
    [DllImport("kernel32.dll")]
    private static extern bool GetProcessAffinityMask(nint hProcess, out nint procMask, out nint sysMask);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);
    private const int SW_RESTORE = 9;
    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(nint hProcess);

    // Soft (non-destructive) throttling: memory priority + EcoQoS. Thay cho việc ép trim/Idle/cap cứng.
    [DllImport("kernel32.dll", EntryPoint = "SetProcessInformation")]
    private static extern bool SetProcessInformationUInt(nint hProcess, int infoClass, ref uint info, int size);
    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE { public uint Version; public uint ControlMask; public uint StateMask; }
    [DllImport("kernel32.dll", EntryPoint = "SetProcessInformation")]
    private static extern bool SetProcessInformationPower(nint hProcess, int infoClass, ref PROCESS_POWER_THROTTLING_STATE info, int size);
    private const int ProcessMemoryPriority = 0, ProcessPowerThrottling = 4;
    private const uint MEMORY_PRIORITY_LOW = 2, MEMORY_PRIORITY_NORMAL = 5;
    private const uint POWER_THROTTLING_EXECUTION_SPEED = 0x1;

    /// <summary>Sàn của slider CPU. Dưới ~30% game có thể khựng/rớt mạng — đó là lựa chọn của người dùng, app chỉ chặn mức vô lý (<10%).</summary>
    public const uint MIN_CPU_CAP_PERCENT = 10;

    private static void SetBackgroundHints(nint h, bool background)
    {
        uint mp = background ? MEMORY_PRIORITY_LOW : MEMORY_PRIORITY_NORMAL;
        SetProcessInformationUInt(h, ProcessMemoryPriority, ref mp, sizeof(uint));
        var st = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = 1,
            ControlMask = POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = background ? POWER_THROTTLING_EXECUTION_SPEED : 0
        };
        SetProcessInformationPower(h, ProcessPowerThrottling, ref st, Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
    }

    // Giới hạn RAM CỨNG bằng hard working-set max: Windows tự đẩy trang vượt mức ra standby/pagefile NGAY và liên tục,
    // game không thể "phình" lại như sau khi trim 1 lần. KHÔNG làm malloc thất bại nên không gây crash (khác Job memory limit).
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSizeEx(nint hProcess, nint min, nint max, uint flags);
    private const uint QUOTA_LIMITS_HARDWS_MIN_DISABLE = 0x2, QUOTA_LIMITS_HARDWS_MAX_ENABLE = 0x4, QUOTA_LIMITS_HARDWS_MAX_DISABLE = 0x8;
    /// <summary>Sàn RAM cứng 200 MB (cho phép đặt lẻ như 200/500 MB). Dưới ~1 GB Roblox có thể thrash page-fault → UI cảnh báo, người dùng tự quyết.</summary>
    public const int MIN_RAM_CAP_MB = 200;

    // Job Object for CPU rate limiting
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateJobObjectW(nint securityAttributes, string? name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint OpenJobObjectW(uint desiredAccess, bool inherit, string name);
    private const uint JOB_OBJECT_SET_ATTRIBUTES = 0x0002;
    // Job đặt tên theo PID để lần mở app sau (sau crash cứng) mở lại được và gỡ cap — Job vô danh thì mất tay cầm vĩnh viễn.
    private static string JobName(uint pid) => $"Local\\RobloxRAMManager_Cap_{pid}";
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(nint hJob, int infoClass, ref JOBOBJECT_CPU_RATE_CONTROL_INFORMATION info, int cbSize);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(nint hJob, nint hProcess);

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
    {
        public uint ControlFlags;
        public uint CpuRate; // hundredths of a percent, e.g. 3000 = 30%
    }

    private const int JobObjectCpuRateControlInformation = 15;
    private const uint JOB_OBJECT_CPU_RATE_CONTROL_ENABLE = 0x1;
    private const uint JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP = 0x4;
    private const uint PROCESS_TERMINATE = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_INFO
    {
        public ushort wProcessorArchitecture; public ushort wReserved;
        public uint dwPageSize; public nint lpMin; public nint lpMax;
        public nint dwActiveProcessorMask; public uint dwNumberOfProcessors;
        public uint dwProcessorType; public uint dwAllocationGranularity;
        public ushort wProcessorLevel; public ushort wProcessorRevision;
    }

    // Process access rights
    private const uint PROCESS_SET_QUOTA = 0x0100;
    private const uint PROCESS_SET_INFORMATION = 0x0200;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_QUERY_LIMITED = 0x1000;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_ALL_NEEDED = PROCESS_SET_QUOTA | PROCESS_SET_INFORMATION | PROCESS_QUERY_INFORMATION;

    /// <summary>Log chẩn đoán từ tầng theo dõi tiến trình (vd lọc "process ma"). Ghi file qua AppLog
    /// luôn luôn; MainWindow có thể subscribe thêm để hiện ngay trong khung Log của app.</summary>
    public static event Action<string>? Diagnostic;
    private static void Diag(string msg) { AppLog.Write(msg); try { Diagnostic?.Invoke(msg); } catch { } }

    // Giới hạn tần suất log "process ma" theo từng PID (phòng trường hợp một PID cứ bị liệt kê ma
    // mãi do thứ gì khác ngoài app này giữ handle — không để log bị spam mỗi lần quét 2 giây/lần).
    private static readonly Dictionary<int, DateTime> _zombieLastLogged = new();
    private static void DiagZombieOnce(int pid, string msg)
    {
        if (_zombieLastLogged.Count > 200) _zombieLastLogged.Clear();
        if (_zombieLastLogged.TryGetValue(pid, out var last) && (DateTime.UtcNow - last) < TimeSpan.FromMinutes(5))
            return;
        _zombieLastLogged[pid] = DateTime.UtcNow;
        Diag(msg);
    }

    /* ── DLL availability ── */
    private static readonly bool _dllAvailable;
    public static bool IsNativeAvailable => _dllAvailable;
    private static long _prevIdle, _prevKernel, _prevUser;
    private static readonly Dictionary<int, (TimeSpan cpu, DateTime wall)> _procCpu = new();
    // Track Job Object handles per PID for CPU rate limiting
    private static readonly Dictionary<uint, nint> _jobHandles = new();

    static NativeInterop()
    {
        try
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            _dllAvailable = File.Exists(Path.Combine(dir, DLL));
            if (_dllAvailable) { var i = new NativeSystemInfo(); NE_GetSystemResources(ref i); }
        }
        catch { _dllAvailable = false; }
    }

    /* ── Public API ── */

    public static NativeSystemInfo GetSystemResources()
    {
        if (_dllAvailable) { var i = new NativeSystemInfo(); NE_GetSystemResources(ref i); return i; }
        return GetSystemResourcesManaged();
    }

    public static NativeProcessInfo[] EnumRobloxProcesses()
    {
        if (_dllAvailable)
        {
            var buf = new NativeProcessInfo[64];
            int c = NE_EnumRobloxProcesses(buf, 64);
            var r = new NativeProcessInfo[c]; Array.Copy(buf, r, c);
            // Nếu native engine mở process thất bại (WorkingSetBytes == 0), chuyển sang fallback managed thay vì coi là process ma
            for (int i = 0; i < c; i++)
            {
                if (r[i].WorkingSetBytes == 0)
                    TryPopulateFromManaged(ref r[i]);
            }
            return r;
        }
        return EnumRobloxManaged();
    }

    private static void TryPopulateFromManaged(ref NativeProcessInfo info)
    {
        try
        {
            using var proc = System.Diagnostics.Process.GetProcessById((int)info.ProcessId);
            if (!proc.HasExited)
            {
                info.WorkingSetBytes = (ulong)proc.WorkingSet64;
                info.PeakWorkingSetBytes = (ulong)proc.PeakWorkingSet64;
                info.PrivateBytes = (ulong)proc.PrivateMemorySize64;
                if (string.IsNullOrEmpty(info.WindowTitle) && !string.IsNullOrEmpty(proc.MainWindowTitle))
                    info.WindowTitle = proc.MainWindowTitle;
                info.PriorityClass = PriorityToWin32(proc.PriorityClass);
            }
        }
        catch { }
    }

    public static bool SetPriority(uint pid, uint priorityClass)
    {
        if (_dllAvailable) return NE_SetProcessPriority(pid, priorityClass);
        var h = OpenProcess(PROCESS_SET_INFORMATION, false, pid);
        if (h == 0) return false;
        var ok = SetPriorityClass(h, priorityClass);
        CloseHandle(h);
        return ok;
    }

    private static readonly Dictionary<uint, DateTime> _lastTrim = new();
    private const int TRIM_COOLDOWN_SEC = 60;

    public static bool CanTrim(uint pid)
    {
        lock (_lastTrim)
        {
            if (_lastTrim.TryGetValue(pid, out var t))
                return (DateTime.UtcNow - t).TotalSeconds >= TRIM_COOLDOWN_SEC;
            return true;
        }
    }

    /// <summary>Trim working set. Trim ép Windows nạp lại trang bằng hard page fault ngay khi game chạm vào → giật/văng nếu
    /// làm liên tục. Nên: mỗi PID tối đa 1 lần/60s (trừ khi force = người dùng bấm tay).</summary>
    public static bool TrimMemory(uint pid, bool force = false)
    {
        lock (_lastTrim)
        {
            var now = DateTime.UtcNow;
            if (!force && _lastTrim.TryGetValue(pid, out var t) && (now - t).TotalSeconds < TRIM_COOLDOWN_SEC)
                return true; // vừa trim xong, bỏ qua (không tính là lỗi)
            _lastTrim[pid] = now;
            if (_lastTrim.Count > 128)
            {
                foreach (var k in _lastTrim.Keys.Where(k => (now - _lastTrim[k]).TotalSeconds > TRIM_COOLDOWN_SEC * 2).ToList())
                    _lastTrim.Remove(k);
            }
        }
        if (_dllAvailable) return NE_TrimProcessMemory(pid);
        var h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED, false, pid);
        if (h == 0) h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION, false, pid);
        if (h == 0) return false;
        SetProcessWorkingSetSize(h, (nint)(-1), (nint)(-1));   // 1 lệnh là đủ, EmptyWorkingSet trùng lặp
        CloseHandle(h);
        return true;
    }

    /// <summary>Set CPU affinity mask. E.g. 0b0011 = core 0+1 only.</summary>
    public static bool SetAffinity(uint pid, nint mask)
    {
        var h = OpenProcess(PROCESS_SET_INFORMATION, false, pid);
        if (h == 0) return false;
        var ok = SetProcessAffinityMask(h, mask);
        CloseHandle(h);
        return ok;
    }

    /// <summary>Tối ưu nhẹ cho acc chạy NỀN: BelowNormal (không bao giờ Idle) + memory priority thấp + EcoQoS + affinity
    /// ≥ nửa số core. KHÔNG trim ở đây (trim đi riêng, có cooldown) → không treo UI, không gây hard page fault hàng loạt.</summary>
    public static (bool trim, bool priority, bool affinity) OptimizeProcess(uint pid, uint priClass, int maxCores)
    {
        var h = OpenProcess(PROCESS_SET_INFORMATION, false, pid);
        if (h == 0) h = OpenProcess(PROCESS_ALL_NEEDED, false, pid);
        if (h == 0) return (false, false, false);

        if (priClass == IDLE_PRIORITY_CLASS) priClass = BELOW_NORMAL_PRIORITY_CLASS; // Idle = bị bỏ đói CPU → rớt mạng → văng
        bool p = SetPriorityClass(h, priClass);
        SetBackgroundHints(h, true);

        GetSystemInfo(out var si);
        int total = (int)Math.Min(si.dwNumberOfProcessors, 64u);
        int cores = Math.Clamp(maxCores, Math.Max(2, total / 2), total);
        int start = (int)((pid / 4) % (uint)total);
        long m = 0;
        for (int k = 0; k < cores; k++) m |= 1L << ((start + k) % total);
        bool a = total <= 2 || SetProcessAffinityMask(h, (nint)m);   // máy ≤2 luồng: đừng đụng affinity

        CloseHandle(h);
        return (true, p, a);
    }

    /// <summary>Trả acc về trạng thái bình thường (priority hints, EcoQoS).</summary>
    public static void RestoreProcess(uint pid)
    {
        var h = OpenProcess(PROCESS_SET_INFORMATION, false, pid);
        if (h == 0) return;
        SetBackgroundHints(h, false);
        CloseHandle(h);
    }

    /// <summary>Dọn sạch các giới hạn (EcoQoS, memory priority, hard RAM cap) còn sót lại trên Roblox đang chạy từ phiên trước (nếu app bị crash cứng).</summary>
    public static void CleanupExistingProcesses()
    {
        try
        {
            var procs = System.Diagnostics.Process.GetProcessesByName("RobloxPlayerBeta");
            foreach (var p in procs)
            {
                try
                {
                    uint pid = (uint)p.Id;
                    RestoreProcess(pid);
                    ClearRamLimit(pid);
                    // Gỡ CPU hard cap còn sót từ phiên trước (Job đặt tên theo PID)
                    var hj = OpenJobObjectW(JOB_OBJECT_SET_ATTRIBUTES, false, JobName(pid));
                    if (hj != 0) { SetJobRate(hj, 100); CloseHandle(hj); }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }
    }

    /// <summary>Dọn rác bộ nhớ của app nhẹ nhàng bằng GC, KHÔNG gọi EmptyWorkingSet hay ép giảm working set
    /// để tránh gây hard page fault cho chính UI của app.</summary>
    public static void TrimSelf()
    {
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    /// <summary>PID của cửa sổ Roblox đang được focus (đang thật sự chơi), 0 nếu không có/không xác định.
    /// Dùng để auto-mode KHÔNG đụng RAM/CPU của acc đang chơi — chỉ dọn/ép các acc chạy nền phía sau.</summary>
    public static uint GetForegroundRobloxPid()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == 0) return 0;
            GetWindowThreadProcessId(hwnd, out uint pid);
            return pid;
        }
        catch { return 0; }
    }

    /// <summary>Get system-wide affinity mask (all logical processors)</summary>
    public static nint GetSystemAffinityMask()
    {
        GetSystemInfo(out var si);
        int n = (int)Math.Min(si.dwNumberOfProcessors, 63u);
        return (nint)((1L << n) - 1);
    }

    private static bool SetJobRate(nint hJob, uint percent)
    {
        var info = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
        {
            ControlFlags = percent >= 100 ? 0 : (JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP),
            CpuRate = percent >= 100 ? 0 : Math.Clamp(percent, 1, 100) * 100 // hundredths of a percent
        };
        return SetInformationJobObject(hJob, JobObjectCpuRateControlInformation, ref info,
            Marshal.SizeOf<JOBOBJECT_CPU_RATE_CONTROL_INFORMATION>());
    }

    private static readonly object _jobLock = new();
    private static readonly Dictionary<uint, uint> _jobRate = new();   // PID → % đang áp (tránh gọi lại vô ích)
    private static readonly Dictionary<uint, int> _ramLimited = new(); // PID → MB đang áp

    /// <summary>Hard-cap CPU qua Job Object. percent: 10-100 (100 = không giới hạn). % tính trên TỔNG CPU máy (như Task Manager).
    /// Lỗi (không gán được vào Job...) được ghi log kèm mã lỗi Win32 thay vì im lặng như trước.</summary>
    public static bool CpuCapProcess(uint pid, uint cpuPercent)
    {
        cpuPercent = Math.Clamp(cpuPercent, MIN_CPU_CAP_PERCENT, 100);
        lock (_jobLock)
        {
            if (_jobHandles.TryGetValue(pid, out var existing))
            {
                if (_jobRate.TryGetValue(pid, out var cur) && cur == cpuPercent) return true;
                bool r = SetJobRate(existing, cpuPercent);
                if (r) _jobRate[pid] = cpuPercent;
                return r;
            }
            if (cpuPercent >= 100) return true; // chưa có Job và không cần giới hạn → khỏi tạo

            var hJob = CreateJobObjectW(0, JobName(pid));
            bool jobExisted = hJob != 0 && Marshal.GetLastWin32Error() == 183; // ERROR_ALREADY_EXISTS: Job sót từ phiên trước, process vẫn nằm trong đó
            if (hJob == 0) { Diag(Loc.Tr($"[CPU CAP] CreateJobObject lỗi (Win32 {Marshal.GetLastWin32Error()}) PID {pid}", $"[CPU CAP] CreateJobObject failed (Win32 {Marshal.GetLastWin32Error()}) PID {pid}")); return false; }
            if (!SetJobRate(hJob, cpuPercent)) { Diag(Loc.Tr($"[CPU CAP] SetInformationJobObject lỗi (Win32 {Marshal.GetLastWin32Error()}) PID {pid}", $"[CPU CAP] SetInformationJobObject failed (Win32 {Marshal.GetLastWin32Error()}) PID {pid}")); CloseHandle(hJob); return false; }

            var hProcess = OpenProcess(PROCESS_SET_QUOTA | PROCESS_TERMINATE, false, pid);
            if (hProcess == 0) { Diag(Loc.Tr($"[CPU CAP] OpenProcess lỗi (Win32 {Marshal.GetLastWin32Error()}) PID {pid}", $"[CPU CAP] OpenProcess failed (Win32 {Marshal.GetLastWin32Error()}) PID {pid}")); CloseHandle(hJob); return false; }

            bool ok = AssignProcessToJobObject(hJob, hProcess);
            int err = ok ? 0 : Marshal.GetLastWin32Error();
            CloseHandle(hProcess);
            if (!ok && jobExisted) ok = true; // process đã ở trong chính Job này rồi → dùng lại để điều khiển

            if (ok) { _jobHandles[pid] = hJob; _jobRate[pid] = cpuPercent; }
            else { Diag(Loc.Tr($"[CPU CAP] Không gán được PID {pid} vào Job (Win32 {err}) — process có thể đang nằm trong Job không cho lồng nhau", $"[CPU CAP] Could not assign PID {pid} to the Job (Win32 {err}) — the process may already be in a non-nestable Job")); CloseHandle(hJob); }
            return ok;
        }
    }

    /// <summary>PID này đã bị cap ở mức bao nhiêu % (null = chưa cap).</summary>
    public static uint? GetCpuCap(uint pid) { lock (_jobLock) return _jobRate.TryGetValue(pid, out var r) ? r : null; }

    /// <summary>Gỡ giới hạn CPU thật sự (đưa về 100%).</summary>
    public static void RemoveCpuCap(uint pid)
    {
        lock (_jobLock)
            if (_jobHandles.TryGetValue(pid, out var h)) { SetJobRate(h, 100); _jobRate[pid] = 100; }
    }

    /// <summary>Gỡ giới hạn và giải phóng Job của một PID (khi tiến trình đã tắt).</summary>
    public static void ReleaseJob(uint pid)
    {
        lock (_jobLock)
        {
            _jobRate.Remove(pid);
            if (_jobHandles.Remove(pid, out var h)) { SetJobRate(h, 100); CloseHandle(h); }
        }
        lock (_ramLimited) _ramLimited.Remove(pid);
    }

    /// <summary>Gỡ mọi giới hạn CPU + RAM (khi thoát app) để Roblox không bị kẹt ở mức đã giới hạn.</summary>
    public static void ReleaseAllCaps()
    {
        lock (_jobLock)
        {
            foreach (var h in _jobHandles.Values) { SetJobRate(h, 100); CloseHandle(h); }
            _jobHandles.Clear(); _jobRate.Clear();
        }
        uint[] pids; lock (_ramLimited) { pids = _ramLimited.Keys.ToArray(); _ramLimited.Clear(); }
        foreach (var pid in pids) ClearRamLimit(pid);
    }

    /// <summary>Đặt giới hạn RAM CỨNG (working set tối đa) cho 1 PID. Đã áp đúng mức đó rồi thì bỏ qua.</summary>
    public static bool SetRamLimit(uint pid, int mb)
    {
        mb = Math.Max(mb, MIN_RAM_CAP_MB);
        lock (_ramLimited) if (_ramLimited.TryGetValue(pid, out var cur) && cur == mb) return true;
        var h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED, false, pid);
        if (h == 0) h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION, false, pid);
        if (h == 0) { Diag(Loc.Tr($"[RAM CAP] OpenProcess lỗi (Win32 {Marshal.GetLastWin32Error()}) PID {pid}", $"[RAM CAP] OpenProcess failed (Win32 {Marshal.GetLastWin32Error()}) PID {pid}")); return false; }
        bool ok = SetProcessWorkingSetSizeEx(h, 204800, (nint)((long)mb * 1024 * 1024), QUOTA_LIMITS_HARDWS_MIN_DISABLE | QUOTA_LIMITS_HARDWS_MAX_ENABLE);
        int err = ok ? 0 : Marshal.GetLastWin32Error();
        CloseHandle(h);
        if (ok) lock (_ramLimited) _ramLimited[pid] = mb;
        else Diag(Loc.Tr($"[RAM CAP] SetProcessWorkingSetSizeEx lỗi (Win32 {err}) PID {pid}", $"[RAM CAP] SetProcessWorkingSetSizeEx failed (Win32 {err}) PID {pid}"));
        return ok;
    }

    /// <summary>Gỡ giới hạn RAM cứng của 1 PID.</summary>
    public static void ClearRamLimit(uint pid)
    {
        var h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED, false, pid);
        if (h == 0) h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION, false, pid);
        if (h == 0) return;
        var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        long max = GlobalMemoryStatusEx(ref ms) ? (long)ms.ullTotalPhys : 64L * 1024 * 1024 * 1024;
        SetProcessWorkingSetSizeEx(h, 204800, (nint)max, QUOTA_LIMITS_HARDWS_MIN_DISABLE | QUOTA_LIMITS_HARDWS_MAX_DISABLE);
        CloseHandle(h);
        lock (_ramLimited) _ramLimited.Remove(pid);
    }

    /// <summary>PID này đang bị giới hạn RAM cứng ở mức nào (MB), null = không.</summary>
    public static int? GetRamLimit(uint pid) { lock (_ramLimited) return _ramLimited.TryGetValue(pid, out var r) ? r : null; }

    /// <summary>Bring Roblox window to front</summary>
    public static bool BringToFront(uint pid)
    {
        try
        {
            var p = System.Diagnostics.Process.GetProcessById((int)pid);
            if (p.MainWindowHandle != 0)
            {
                ShowWindow(p.MainWindowHandle, SW_RESTORE);
                return SetForegroundWindow(p.MainWindowHandle);
            }
        }
        catch { }
        return false;
    }

    /// <summary>Terminate a specific process</summary>
    public static bool KillProcess(uint pid)
    {
        try
        {
            var p = System.Diagnostics.Process.GetProcessById((int)pid);
            p.Kill();
            return true;
        }
        catch { return false; }
    }

    /* ── Managed fallback ── */

    private static NativeSystemInfo GetSystemResourcesManaged()
    {
        var info = new NativeSystemInfo();
        var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref ms))
        {
            info.TotalPhysicalBytes = ms.ullTotalPhys;
            info.AvailablePhysicalBytes = ms.ullAvailPhys;
            info.MemoryLoadPercent = ms.dwMemoryLoad;
        }
        GetSystemInfo(out var si);
        info.ProcessorCount = si.dwNumberOfProcessors;
        if (GetSystemTimes(out long idle, out long kernel, out long user))
        {
            if (_prevKernel != 0)
            {
                long dI = idle - _prevIdle, dK = kernel - _prevKernel, dU = user - _prevUser;
                long dT = dK + dU;
                if (dT > 0) info.TotalCpuPercent = (double)(dT - dI) * 100.0 / dT;
            }
            _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
        }
        return info;
    }

    private static NativeProcessInfo[] EnumRobloxManaged()
    {
        var list = new List<NativeProcessInfo>();
        var now = DateTime.UtcNow;
        var procsNow = System.Diagnostics.Process.GetProcessesByName("RobloxPlayerBeta");
        var alive = new HashSet<int>();
        foreach (var proc in procsNow)
        {
            try
            {
                // "Process ma": Windows vẫn liệt kê một PID trong GetProcessesByName dù tiến trình
                // đã thoát hẳn, MIỄN LÀ còn ít nhất 1 handle nào đó (kể cả của chính app này, ví dụ
                // Process.EnableRaisingEvents = true trong ProcessMonitor) còn tham chiếu tới nó.
                // Task Manager lọc dựa theo exit code thật nên không hiện, còn GetProcessesByName thì
                // không tự lọc — nếu không chặn ở đây, app sẽ hiện nhầm acc "đang chạy" (RAM/CPU = 0,
                // không bao giờ bị phát hiện là văng → auto mở lại cũng không chạy). Lọc ngay tại đây.
                if (proc.HasExited)
                {
                    DiagZombieOnce(proc.Id, Loc.Tr($"[ProcessMonitor] Bỏ qua PID {proc.Id} — vẫn bị GetProcessesByName liệt kê dù HasExited=true (process ma/zombie, thường do handle bị giữ lại). Đây là PID ĐÃ THOÁT, không phải Roblox đang chạy thật.", $"[ProcessMonitor] Skipping PID {proc.Id} — still listed by GetProcessesByName although HasExited=true (ghost/zombie process, usually a leaked handle). This PID has already exited and is not a live Roblox."));
                    continue;
                }

                var info = new NativeProcessInfo
                {
                    ProcessId = (uint)proc.Id,
                    WindowTitle = string.IsNullOrEmpty(proc.MainWindowTitle) ? $"Roblox ({proc.Id})" : proc.MainWindowTitle,
                    WorkingSetBytes = (ulong)proc.WorkingSet64,
                    PeakWorkingSetBytes = (ulong)proc.PeakWorkingSet64,
                    PrivateBytes = (ulong)proc.PrivateMemorySize64,
                    Responding = proc.Responding,
                    PriorityClass = PriorityToWin32(proc.PriorityClass)
                };
                var cpuNow = proc.TotalProcessorTime;
                if (_procCpu.TryGetValue(proc.Id, out var prev))
                {
                    var dC = (cpuNow - prev.cpu).TotalMilliseconds;
                    var dW = (now - prev.wall).TotalMilliseconds;
                    // Normalize by core count (matches Task Manager); otherwise a process
                    // pegging a single core reads up to (100 * ProcessorCount)%.
                    if (dW > 0) info.CpuPercent = Math.Clamp(dC / dW * 100.0 / Math.Max(1, Environment.ProcessorCount), 0, 100);
                }
                _procCpu[proc.Id] = (cpuNow, now);
                alive.Add(proc.Id);
                list.Add(info);
            }
            catch (Exception ex)
            {
                // Không đọc được thông tin tiến trình (quyền truy cập / đã thoát giữa chừng) →
                // bỏ qua PID này, KHÔNG thêm vào danh sách (tránh hiện entry rỗng/sai số liệu).
                Diag(Loc.Tr($"[ProcessMonitor] Bỏ qua PID {proc.Id} — lỗi khi đọc thông tin tiến trình: {ex.GetType().Name}: {ex.Message}", $"[ProcessMonitor] Skipping PID {proc.Id} — failed to read process info: {ex.GetType().Name}: {ex.Message}"));
            }
            finally { proc.Dispose(); }
        }
        // Dọn cache CPU của các PID không còn thật sự sống (kể cả process ma vừa lọc ở trên) —
        // trước đây tính "alive" từ danh sách thô nên cache của process ma không bao giờ bị dọn.
        foreach (var dead in _procCpu.Keys.Where(k => !alive.Contains(k)).ToList()) _procCpu.Remove(dead);
        return list.ToArray();
    }

    /* ── Priority helpers ── */

    public const uint IDLE_PRIORITY_CLASS = 0x40;
    public const uint BELOW_NORMAL_PRIORITY_CLASS = 0x4000;
    public const uint NORMAL_PRIORITY_CLASS = 0x20;
    public const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x8000;
    public const uint HIGH_PRIORITY_CLASS = 0x80;
    public const uint REALTIME_PRIORITY_CLASS = 0x100;

    public static uint PriorityToWin32(System.Diagnostics.ProcessPriorityClass pc) => pc switch
    {
        System.Diagnostics.ProcessPriorityClass.Idle => IDLE_PRIORITY_CLASS,
        System.Diagnostics.ProcessPriorityClass.BelowNormal => BELOW_NORMAL_PRIORITY_CLASS,
        System.Diagnostics.ProcessPriorityClass.Normal => NORMAL_PRIORITY_CLASS,
        System.Diagnostics.ProcessPriorityClass.AboveNormal => ABOVE_NORMAL_PRIORITY_CLASS,
        System.Diagnostics.ProcessPriorityClass.High => HIGH_PRIORITY_CLASS,
        System.Diagnostics.ProcessPriorityClass.RealTime => REALTIME_PRIORITY_CLASS,
        _ => NORMAL_PRIORITY_CLASS
    };

    public static System.Diagnostics.ProcessPriorityClass Win32ToPriority(uint cls) => cls switch
    {
        IDLE_PRIORITY_CLASS => System.Diagnostics.ProcessPriorityClass.Idle,
        BELOW_NORMAL_PRIORITY_CLASS => System.Diagnostics.ProcessPriorityClass.BelowNormal,
        ABOVE_NORMAL_PRIORITY_CLASS => System.Diagnostics.ProcessPriorityClass.AboveNormal,
        HIGH_PRIORITY_CLASS => System.Diagnostics.ProcessPriorityClass.High,
        REALTIME_PRIORITY_CLASS => System.Diagnostics.ProcessPriorityClass.RealTime,
        _ => System.Diagnostics.ProcessPriorityClass.Normal
    };

    public static string PriorityName(uint cls) => cls switch
    {
        IDLE_PRIORITY_CLASS => "Idle",
        BELOW_NORMAL_PRIORITY_CLASS => "Below Normal",
        NORMAL_PRIORITY_CLASS => "Normal",
        ABOVE_NORMAL_PRIORITY_CLASS => "Above Normal",
        HIGH_PRIORITY_CLASS => "High",
        REALTIME_PRIORITY_CLASS => "Realtime",
        _ => $"0x{cls:X}"
    };
}
