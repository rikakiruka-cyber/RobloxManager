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
    private const int SW_RESTORE = 9;
    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(nint hProcess);

    // Job Object for CPU rate limiting
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateJobObjectW(nint securityAttributes, string? name);
    [DllImport("kernel32.dll")]
    private static extern bool SetInformationJobObject(nint hJob, int infoClass, ref JOBOBJECT_CPU_RATE_CONTROL_INFORMATION info, int cbSize);
    [DllImport("kernel32.dll")]
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
            var r = new NativeProcessInfo[c]; Array.Copy(buf, r, c); return r;
        }
        return EnumRobloxManaged();
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

    /// <summary>Aggressive RAM trim: EmptyWorkingSet + SetProcessWorkingSetSize(-1,-1)</summary>
    public static bool TrimMemory(uint pid)
    {
        if (_dllAvailable) return NE_TrimProcessMemory(pid);
        var h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION, false, pid);
        if (h == 0) return false;
        EmptyWorkingSet(h);
        SetProcessWorkingSetSize(h, (nint)(-1), (nint)(-1));
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

    /// <summary>Full optimization: trim + below normal priority + limit to N cores</summary>
    public static (bool trim, bool priority, bool affinity) OptimizeProcess(uint pid, uint priClass, int maxCores)
    {
        var h = OpenProcess(PROCESS_ALL_NEEDED, false, pid);
        if (h == 0) return (false, false, false);

        bool t = EmptyWorkingSet(h);
        SetProcessWorkingSetSize(h, (nint)(-1), (nint)(-1));
        bool p = SetPriorityClass(h, priClass);

        // Affinity: N cores, rải đều theo PID (không dồn mọi acc vào core 0-1)
        GetSystemInfo(out var si);
        int total = (int)Math.Min(si.dwNumberOfProcessors, 64u);
        int cores = Math.Clamp(maxCores, 1, total);
        int start = (int)((pid / 4) % (uint)total);
        long m = 0;
        for (int k = 0; k < cores; k++) m |= 1L << ((start + k) % total);
        bool a = SetProcessAffinityMask(h, (nint)m);

        CloseHandle(h);
        return (t, p, a);
    }

    /// <summary>Trim the tool's own memory to stay lightweight</summary>
    public static void TrimSelf()
    {
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        var h = System.Diagnostics.Process.GetCurrentProcess().Handle;
        EmptyWorkingSet(h);
        SetProcessWorkingSetSize(h, (nint)(-1), (nint)(-1));
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
            ControlFlags = JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP,
            CpuRate = Math.Clamp(percent, 1, 100) * 100 // hundredths of a percent
        };
        return SetInformationJobObject(hJob, JobObjectCpuRateControlInformation, ref info,
            Marshal.SizeOf<JOBOBJECT_CPU_RATE_CONTROL_INFORMATION>());
    }

    /// <summary>Hard-cap CPU usage via Job Object. cpuPercent: 1-100. Đổi mức giới hạn dùng lại Job cũ.</summary>
    public static bool CpuCapProcess(uint pid, uint cpuPercent)
    {
        if (_jobHandles.TryGetValue(pid, out var existing))
            return SetJobRate(existing, cpuPercent);

        var hJob = CreateJobObjectW(0, null);
        if (hJob == 0) return false;
        if (!SetJobRate(hJob, cpuPercent)) { CloseHandle(hJob); return false; }

        var hProcess = OpenProcess(PROCESS_SET_QUOTA | PROCESS_TERMINATE, false, pid);
        if (hProcess == 0) { CloseHandle(hJob); return false; }

        bool ok = AssignProcessToJobObject(hJob, hProcess);
        CloseHandle(hProcess);

        if (ok) _jobHandles[pid] = hJob;
        else CloseHandle(hJob);
        return ok;
    }

    /// <summary>Gỡ giới hạn CPU thật sự (đưa về 100%). Trước đây chỉ đóng handle nên giới hạn vẫn còn.</summary>
    public static void RemoveCpuCap(uint pid)
    {
        if (_jobHandles.TryGetValue(pid, out var h)) SetJobRate(h, 100);
    }

    /// <summary>Gỡ giới hạn và giải phóng Job của một PID (khi tiến trình đã tắt).</summary>
    public static void ReleaseJob(uint pid)
    {
        if (_jobHandles.Remove(pid, out var h)) { SetJobRate(h, 100); CloseHandle(h); }
    }

    /// <summary>Gỡ mọi giới hạn CPU (khi thoát app).</summary>
    public static void ReleaseAllCaps()
    {
        foreach (var h in _jobHandles.Values) { SetJobRate(h, 100); CloseHandle(h); }
        _jobHandles.Clear();
    }

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
        var alive = new HashSet<int>(procsNow.Select(p => p.Id));
        foreach (var dead in _procCpu.Keys.Where(k => !alive.Contains(k)).ToList()) _procCpu.Remove(dead);
        foreach (var proc in procsNow)
        {
            try
            {
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
                list.Add(info);
            }
            catch { }
            finally { proc.Dispose(); }
        }
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
