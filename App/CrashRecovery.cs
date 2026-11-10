// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Diagnostics;
using System.Management;
using System.Text.RegularExpressions;

namespace RobloxRAMManager;

/// <summary>
/// Tracks Roblox instances, detects crashes, and auto-relaunches via roblox:// protocol.
/// </summary>
public sealed class CrashRecovery
{
    public bool Enabled { get; set; } = true;

    // PlaceId cache: PID → placeId (extracted from command line on first detection)
    private readonly Dictionary<uint, long> _placeIdCache = new();
    private readonly object _cacheLock = new();

    // Crash log
    public List<CrashEvent> CrashLog { get; } = new();

    public int TotalCrashes => CrashLog.Count;
    public int TotalRecoveries => CrashLog.Count(c => c.Recovered);

    /// <summary>
    /// Called when ProcessMonitor detects a process has disappeared.
    /// </summary>
    public void HandleCrash(RobloxInstanceVM instance, bool accountHandled = false, bool accountRecovered = false)
    {
        var crash = new CrashEvent
        {
            Pid = instance.Pid,
            WindowTitle = instance.WindowTitle,
            RamMB = instance.RamMB,
            CpuPercent = instance.CpuPercent,
            CrashTime = DateTime.Now,
            PlaceId = instance.PlaceId > 0 ? instance.PlaceId : GetCachedPlaceId(instance.Pid)
        };

        CrashLog.Add(crash);
        if (CrashLog.Count > 200) CrashLog.RemoveAt(0);

        if (accountHandled)
        {
            // Acc có lưu trong Quản lý tài khoản: MainWindow đã tự đăng nhập & mở lại
            crash.Recovered = accountRecovered;
            if (accountRecovered) crash.RecoveryTime = DateTime.Now;
        }
        else if (Enabled && crash.PlaceId > 0)
        {
            try
            {
                // Relaunch via Roblox protocol URL
                var url = $"roblox://placeId={crash.PlaceId}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                crash.Recovered = true;
                crash.RecoveryTime = DateTime.Now;
            }
            catch
            {
                crash.Recovered = false;
            }
        }

        // Clean up cache
        lock (_cacheLock) _placeIdCache.Remove(instance.Pid);
    }

    /// <summary>
    /// Extract PlaceId from a Roblox process command line and cache it.
    /// Call this when a new Roblox instance is first detected.
    /// </summary>
    public long CacheInstancePlaceId(uint pid)
    {
        lock (_cacheLock)
            if (_placeIdCache.TryGetValue(pid, out var cached))
                return cached;

        // WMI chậm → chạy ngoài khoá (hàm này được gọi từ thread nền)
        var placeId = ExtractPlaceIdFromCommandLine(pid);
        lock (_cacheLock)
        {
            // Nếu trong lúc chờ WMI đã có PlaceId chính xác hơn (đọc từ log) thì giữ cái đó
            if (_placeIdCache.TryGetValue(pid, out var already) && already > 0) return already;
            _placeIdCache[pid] = Math.Max(placeId, 0); // cache cả kết quả "không có" để khỏi gọi WMI lại liên tục
        }
        return placeId;
    }

    public void RememberPlace(uint pid, long placeId) { if (placeId > 0) lock (_cacheLock) _placeIdCache[pid] = placeId; }

    public long GetCachedPlaceId(uint pid)
    {
        lock (_cacheLock) return _placeIdCache.TryGetValue(pid, out var id) ? id : 0;
    }

    private static long ExtractPlaceIdFromCommandLine(uint pid)
    {
        try
        {
            // WMI query for process command line
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                using (obj)
                {
                    var cmdLine = obj["CommandLine"]?.ToString();
                    if (string.IsNullOrEmpty(cmdLine)) continue;

                    // Look for placeId in command line args or URL
                    // Formats: -placeId 123456  or  placeId=123456
                    var match = Regex.Match(cmdLine, @"placeId(?:=|%3D|\s)+(\d+)", RegexOptions.IgnoreCase);
                    if (match.Success && long.TryParse(match.Groups[1].Value, out var id))
                        return id;
                }
            }
        }
        catch
        {
            // ponytail: WMI might fail if process already exited, that's OK
        }
        return 0;
    }
}

public class CrashEvent
{
    public uint Pid { get; set; }
    public string WindowTitle { get; set; } = "";
    public double RamMB { get; set; }
    public double CpuPercent { get; set; }
    public DateTime CrashTime { get; set; }
    public long PlaceId { get; set; }
    public bool Recovered { get; set; }
    public DateTime? RecoveryTime { get; set; }

    public string Summary => $"[{CrashTime:HH:mm:ss}] {WindowTitle} (PID {Pid}) - " +
                             (Recovered ? "✓ Recovered" : "✗ Manual recovery needed");
}
