// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace RobloxRAMManager;

public static class DiscordNotifier
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<(bool success, string message)> SendStatusAsync(
        ProcessMonitor monitor, CrashRecovery recovery, string? alertMessage = null)
    {
        var url = AppConfig.DiscordWebhookUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url) || (!url.StartsWith("http://") && !url.StartsWith("https://")))
            return (false, Loc.Tr("Chưa điền Webhook URL hợp lệ", "No valid Webhook URL entered"));

        try
        {
            var s = monitor.Stats;
            var isAlert = !string.IsNullOrEmpty(alertMessage);
            int embedColor = isAlert ? 0xFF0040 : (s.CrashedAccounts > 0 ? 0xF59E0B : 0x00FF88);

            // Build accounts list summary
            var accSummary = new StringBuilder();
            if (monitor.Instances.Count == 0)
            {
                accSummary.AppendLine(Loc.Tr("_(Chưa phát hiện tài khoản Roblox nào đang chạy)_", "_(No running Roblox account detected)_"));
            }
            else
            {
                foreach (var inst in monitor.Instances)
                {
                    var statusDot = inst.IsResponding ? "🟢" : "🔴";
                    accSummary.AppendLine($"{statusDot} **#{inst.Index} {inst.WindowTitle}** (PID {inst.Pid})");
                    accSummary.AppendLine($"　└ 💾 **{inst.RamDisplay}** | ⚡ **{inst.CpuDisplay}** | ⏱️ {Loc.Tr("Treo", "Uptime")}: **{inst.UptimeDisplay}** | Pri: `{inst.Priority}`");
                }
            }

            var embed = new Dictionary<string, object>
            {
                ["title"] = isAlert ? $"🚨 [{Loc.Tr("CẢNH BÁO", "ALERT")}] {alertMessage}" : Loc.Tr("🎮 Roblox RAM Manager — Báo cáo trạng thái", "🎮 Roblox RAM Manager — Status report"),
                ["color"] = embedColor,
                ["description"] = isAlert
                    ? Loc.Tr($"⚠️ Đã phát hiện sự cố lúc **{DateTime.Now:HH:mm:ss dd/MM/yyyy}**", $"⚠️ Issue detected at **{DateTime.Now:HH:mm:ss dd/MM/yyyy}**")
                    : Loc.Tr($"Báo cáo tự động cập nhật lúc **{DateTime.Now:HH:mm:ss dd/MM/yyyy}**", $"Automatic report updated at **{DateTime.Now:HH:mm:ss dd/MM/yyyy}**"),
                ["fields"] = new object[]
                {
                    new { name = "⏱️ Tool Uptime", value = monitor.ManagerUptimeDisplay, inline = true },
                    new { name = Loc.Tr("🎮 Số Acc Treo", "🎮 Idle Accounts"), value = $"{s.ActiveAccounts} Active / {s.TotalAccounts} Total", inline = true },
                    new { name = Loc.Tr("💥 Tổng Crash", "💥 Total Crashes"), value = recovery.TotalCrashes.ToString(), inline = true },
                    new { name = Loc.Tr("💾 RAM Hệ Thống", "💾 System RAM"), value = $"{s.RamDisplay} ({s.MemoryLoadPercent}%)", inline = true },
                    new { name = Loc.Tr("⚡ CPU Hệ Thống", "⚡ System CPU"), value = $"{s.CpuPercent:F1}%", inline = true },
                    new { name = Loc.Tr("🛡️ Chế độ", "🛡️ Mode"), value = $"Cap: {AppConfig.CpuCapPercent}% | Trim: {AppConfig.TrimIntervalSec}s", inline = true },
                    new { name = Loc.Tr("📋 Chi tiết từng tài khoản Roblox", "📋 Per-account details"), value = accSummary.ToString(), inline = false }
                },
                ["footer"] = new { text = $"Roblox RAM Manager v{AppInfo.VersionText} • by {AppInfo.Author}" },
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            var payload = new
            {
                username = "Roblox RAM Manager",
                avatar_url = "https://raw.githubusercontent.com/twitter/twemoji/master/assets/72x72/26a1.png",
                embeds = new[] { embed }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await _http.PostAsync(url, content);

            if (res.IsSuccessStatusCode)
                return (true, Loc.Tr("Gửi thông số lên Discord thành công!", "Stats sent to Discord successfully!"));

            return (false, Loc.Tr($"Discord trả về mã lỗi: {(int)res.StatusCode} {res.ReasonPhrase}", $"Discord returned error: {(int)res.StatusCode} {res.ReasonPhrase}"));
        }
        catch (Exception ex)
        {
            return (false, Loc.Tr($"Lỗi kết nối: {ex.Message}", $"Connection error: {ex.Message}"));
        }
    }

    /// <summary>Gửi cảnh báo quan trọng; ping=true thì @người dùng theo Discord ID đã cấu hình.</summary>
    public static async Task<bool> SendAlertAsync(string title, string detail, bool ping)
    {
        var url = AppConfig.DiscordWebhookUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url) || (!url.StartsWith("http://") && !url.StartsWith("https://"))) return false;
        try
        {
            var id = new string((AppConfig.DiscordUserId ?? "").Where(char.IsDigit).ToArray());
            bool doPing = ping && id.Length >= 15;
            var payload = new Dictionary<string, object>
            {
                ["username"] = "Roblox RAM Manager",
                ["content"] = doPing ? $"<@{id}>" : "",
                ["allowed_mentions"] = doPing ? (object)new { users = new[] { id } } : new { parse = Array.Empty<string>() },
                ["embeds"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["title"] = "🚨 " + title,
                        ["description"] = detail + $"\n\n🕒 {DateTime.Now:HH:mm:ss dd/MM/yyyy}",
                        ["color"] = 0xFF0040
                    }
                }
            };
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync(url, content);
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
