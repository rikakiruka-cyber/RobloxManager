// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Diagnostics;
using System.IO;

namespace RobloxRAMManager;

public enum ClientKind { Roblox, Bloxstrap, Fishstrap, Voidstrap }

/// <summary>Một client/bootstrapper Roblox đã tìm thấy trên máy.</summary>
public sealed class ClientInfo
{
    public ClientKind Kind { get; init; }
    /// <summary>Id ổn định để lưu vào tài khoản (không đổi giữa các lần quét).</summary>
    public string Id => Kind.ToString();
    public string DisplayName { get; init; } = "";
    public string ExePath { get; init; } = "";
    public string? Version { get; init; }

    public override string ToString() => Version != null ? $"{DisplayName}  ({Version})" : DisplayName;
}

/// <summary>
/// Quét máy tìm các client Roblox đã cài (Roblox gốc, Bloxstrap, Fishstrap, Voidstrap) và
/// kiểm tra nhanh trước khi mở xem client đó có đang ở trạng thái mở Roblox lên được bình thường không.
/// </summary>
public static class ClientManager
{
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>Quét các vị trí cài đặt thường gặp. Không tìm thấy client nào cũng không lỗi — trả về danh sách rỗng.</summary>
    public static List<ClientInfo> Detect()
    {
        var found = new List<ClientInfo>();

        // Roblox gốc: %LOCALAPPDATA%\Roblox\Versions\version-xxxx\RobloxPlayerBeta.exe (lấy bản mới nhất nếu có nhiều bản còn sót lại)
        try
        {
            var versionsDir = Path.Combine(LocalAppData, "Roblox", "Versions");
            if (Directory.Exists(versionsDir))
            {
                var best = Directory.GetDirectories(versionsDir)
                    .Select(d => Path.Combine(d, "RobloxPlayerBeta.exe"))
                    .Where(File.Exists)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (best != null)
                    found.Add(new ClientInfo { Kind = ClientKind.Roblox, DisplayName = "Roblox", ExePath = best, Version = ReadVersion(best) });
            }
        }
        catch { /* không chặn quét các client khác nếu Roblox gốc lỗi */ }

        AddIfExists(found, ClientKind.Bloxstrap, "Bloxstrap", Path.Combine(LocalAppData, "Bloxstrap", "Bloxstrap.exe"));
        AddIfExists(found, ClientKind.Fishstrap, "Fishstrap", Path.Combine(LocalAppData, "Fishstrap", "Fishstrap.exe"));
        AddIfExists(found, ClientKind.Voidstrap, "Voidstrap", Path.Combine(LocalAppData, "Voidstrap", "Voidstrap.exe"));

        return found;
    }

    private static void AddIfExists(List<ClientInfo> list, ClientKind kind, string name, string exe)
    {
        try
        {
            if (File.Exists(exe))
                list.Add(new ClientInfo { Kind = kind, DisplayName = name, ExePath = exe, Version = ReadVersion(exe) });
        }
        catch { }
    }

    private static string? ReadVersion(string exe)
    {
        try { return FileVersionInfo.GetVersionInfo(exe).FileVersion; } catch { return null; }
    }

    /// <summary>
    /// Kiểm tra nhanh TRƯỚC khi mở (không mở game thật): file exe còn tồn tại và đọc được
    /// (chưa bị gỡ cài đặt / chưa bị antivirus khoá / chưa bị hỏng). Trả về lỗi cụ thể nếu có.
    /// </summary>
    public static (bool ok, string message) PreflightCheck(ClientInfo client)
    {
        if (!File.Exists(client.ExePath))
            return (false, Loc.Tr(
                $"Không tìm thấy {client.DisplayName} ở đường dẫn đã lưu ({client.ExePath}) — có thể đã gỡ cài đặt hoặc cập nhật lại đường dẫn. Hãy bấm Quét lại.",
                $"{client.DisplayName} was not found at its saved path ({client.ExePath}) — it may have been uninstalled or moved. Try rescanning."));

        try
        {
            using var fs = File.Open(client.ExePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (fs.Length == 0)
                return (false, Loc.Tr($"File {client.DisplayName} bị rỗng/hỏng (0 byte).", $"The {client.DisplayName} file is empty/corrupted (0 bytes)."));
        }
        catch (Exception ex)
        {
            return (false, Loc.Tr(
                $"Không đọc được file {client.DisplayName} (có thể đang bị antivirus chặn hoặc bị hỏng): {ex.Message}",
                $"Could not read the {client.DisplayName} file (may be blocked by antivirus or corrupted): {ex.Message}"));
        }
        return (true, "");
    }

    /// <summary>
    /// Mở thẳng vào đúng client đã chọn bằng URI gameinfo (không qua trình xử lý URI mặc định của
    /// Windows, nên chọn client nào là chắc chắn client đó mở, không bị client khác "giành" mất),
    /// rồi theo dõi vài giây đầu xem tiến trình có văng ngay với lỗi không — nếu có, báo lỗi luôn
    /// thay vì để người dùng chờ hết 90 giây mới biết Roblox không lên được.
    /// </summary>
    public static async Task<(bool ok, string message)> LaunchAndVerifyAsync(ClientInfo client, string uri)
    {
        var pre = PreflightCheck(client);
        if (!pre.ok) return pre;

        Process? proc;
        try
        {
            proc = Process.Start(new ProcessStartInfo(client.ExePath, uri) { UseShellExecute = true });
            if (proc == null)
                return (true, Loc.Tr($"Đã gửi lệnh mở bằng {client.DisplayName} (không theo dõi được tiến trình).",
                                      $"Launch sent via {client.DisplayName} (could not track the process)."));
        }
        catch (Exception ex)
        {
            return (false, Loc.Tr($"Không khởi động được {client.DisplayName}: {ex.Message}",
                                   $"Could not start {client.DisplayName}: {ex.Message}"));
        }

        // Một số bootstrapper tự thoát tiến trình "launcher" ngay sau khi đã bàn giao cho
        // RobloxPlayerBeta.exe (bình thường, exit code 0) — nên chỉ coi là lỗi khi nó văng
        // RẤT sớm kèm exit code báo lỗi (ví dụ thiếu file, crash khi khởi tạo).
        try
        {
            bool exitedFast = await Task.Run(() => proc.WaitForExit(1500));
            if (exitedFast && proc.ExitCode != 0)
            {
                return (false, Loc.Tr(
                    $"{client.DisplayName} thoát ngay với lỗi (mã {proc.ExitCode}) — không mở được Roblox. Hãy thử mở {client.DisplayName} thủ công để xem chi tiết lỗi, hoặc chọn client khác.",
                    $"{client.DisplayName} exited immediately with an error (code {proc.ExitCode}) — Roblox did not open. Try opening {client.DisplayName} manually to see the error, or pick a different client."));
            }
        }
        catch { /* không đọc được exit code thì bỏ qua, coi như đã gửi lệnh mở thành công */ }

        return (true, Loc.Tr($"Đã gửi lệnh mở bằng {client.DisplayName}.", $"Launch sent via {client.DisplayName}."));
    }
}
