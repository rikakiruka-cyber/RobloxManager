// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.IO;

namespace RobloxRAMManager;

/// <summary>
/// Logger file dùng chung, không phụ thuộc UI — để các lớp không truy cập được MainWindow
/// (ví dụ NativeInterop, ProcessMonitor chạy nền) vẫn ghi được log chẩn đoán ra
/// logs/app_YYYY-MM-DD.log, cùng file với log chính của MainWindow.
/// </summary>
public static class AppLog
{
    private static readonly object _lock = new();

    public static void Write(string msg)
    {
        try
        {
            if (DataStore.Dir == null) return;
            lock (_lock)
            {
                File.AppendAllText(Path.Combine(DataStore.LogsDir, $"app_{DateTime.Now:yyyy-MM-dd}.log"),
                    $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
        }
        catch { /* ghi log lỗi thì bỏ qua, không được làm crash app */ }
    }
}
