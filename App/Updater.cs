// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace RobloxRAMManager;

public static class AppInfo
{
    /// <summary>Repo GitHub chứa các bản phát hành (Releases). Phải để Public để app của người khác đọc được.
    /// GitHub Releases repo; must be Public so other people's apps can read it.</summary>
    public const string UpdateRepo = "rikakiruka-cyber/RobloxManager";

    /// <summary>Tác giả / Author — hiển thị trong app, báo cáo Discord và thuộc tính file exe.</summary>
    public const string Author = "RikaSoora";
    public const string AuthorDiscordId = "1374334753242873908";
    public static string AuthorDiscordUrl => $"https://discord.com/users/{AuthorDiscordId}";

    public static Version Current
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? new Version(0, 0, 0) : new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }
    public static string VersionText => Current.ToString(3);
}

public sealed record UpdateInfo(Version Version, string Tag, string NotesVi, string NotesEn,
                                string AssetUrl, long AssetSize, string? Sha256);

/// <summary>Tự cập nhật qua GitHub Releases: kiểm tra bản mới → tải → thay exe → khởi động lại.</summary>
public static class Updater
{
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "RobloxManager_update");

    private static HttpClient NewClient(TimeSpan timeout)
    {
        var c = new HttpClient { Timeout = timeout };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("RobloxManager-Updater/" + AppInfo.VersionText);
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    /// <returns>info != null: có bản mới. info == null &amp;&amp; ok: đang là bản mới nhất. ok == false: lỗi (msg).</returns>
    public static async Task<(UpdateInfo? info, bool ok, string msg)> CheckAsync()
    {
        try
        {
            try { if (Directory.Exists(TempDir)) Directory.Delete(TempDir, true); } catch { }

            using var http = NewClient(TimeSpan.FromSeconds(15));
            using var res = await http.GetAsync($"https://api.github.com/repos/{AppInfo.UpdateRepo}/releases/latest");
            if (res.StatusCode == HttpStatusCode.NotFound)
                return (null, true, Loc.Tr("Bạn đang dùng bản mới nhất.", "You are on the latest version."));
            if (!res.IsSuccessStatusCode)
                return (null, false, Loc.Tr($"Máy chủ cập nhật trả về lỗi {(int)res.StatusCode}.", $"Update server returned error {(int)res.StatusCode}."));

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            string tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var ver))
                return (null, false, Loc.Tr($"Tên phiên bản lạ: {tag}", $"Unrecognized version tag: {tag}"));
            if (ver <= AppInfo.Current)
                return (null, true, Loc.Tr("Bạn đang dùng bản mới nhất.", "You are on the latest version."));

            string? url = null, sha = null;
            long size = 0;
            foreach (var a in root.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (url != null && !name.Contains("x64", StringComparison.OrdinalIgnoreCase)) continue;
                url = a.GetProperty("browser_download_url").GetString();
                size = a.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                sha = a.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String
                      ? dg.GetString() : null;
            }
            if (url == null)
                return (null, false, Loc.Tr("Bản cập nhật chưa có file cài đặt.", "The update has no installer file."));

            string body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var (vi, en) = SplitNotes(body);
            (vi, en) = await MergeSkippedAsync(http, ver, vi, en);
            return (new UpdateInfo(ver, tag, vi, en, url, size, sha), true, "");
        }
        catch (Exception ex)
        {
            return (null, false, Loc.Tr("Không kiểm tra được cập nhật: ", "Could not check for updates: ") + ex.Message);
        }
    }

    /// <summary>Toàn bộ lịch sử các bản phát hành (ghi chú VI/EN) lấy từ Releases — dùng bổ sung cho CHANGELOG.json nhúng trong exe.</summary>
    public static async Task<List<ChangelogEntry>> FetchHistoryAsync()
    {
        var list = new List<ChangelogEntry>();
        try
        {
            using var http = NewClient(TimeSpan.FromSeconds(15));
            using var res = await http.GetAsync($"https://api.github.com/repos/{AppInfo.UpdateRepo}/releases?per_page=100");
            if (!res.IsSuccessStatusCode) return list;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            foreach (var r in doc.RootElement.EnumerateArray())
            {
                if (r.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;
                if (r.TryGetProperty("prerelease", out var p) && p.GetBoolean()) continue;
                string t = r.GetProperty("tag_name").GetString() ?? "";
                if (!Version.TryParse(t.TrimStart('v', 'V'), out var v)) continue;
                string date = r.TryGetProperty("published_at", out var pa) && pa.ValueKind == JsonValueKind.String
                    ? (pa.GetString() ?? "") : "";
                if (date.Length >= 10) date = date[..10]; else date = "";
                string bd = r.TryGetProperty("body", out var bb) ? bb.GetString() ?? "" : "";
                var (vi, en) = SplitNotes(bd);
                static string[] Lines(string s) => s.Split('\n')
                    .Select(l => Regex.Replace(l.Trim(), @"^[-*\u2022]\s*", ""))
                    .Where(l => l.Length > 0).ToArray();
                list.Add(new ChangelogEntry(new Version(v.Major, v.Minor, Math.Max(v.Build, 0)), date, Lines(vi), Lines(en)));
            }
        }
        catch { }
        return list;
    }

    /// <summary>Nếu người dùng bỏ qua nhiều bản, gộp ghi chú của tất cả bản từ sau bản đang dùng đến bản mới nhất.</summary>
    private static async Task<(string vi, string en)> MergeSkippedAsync(HttpClient http, Version latest, string latestVi, string latestEn)
    {
        try
        {
            using var res = await http.GetAsync($"https://api.github.com/repos/{AppInfo.UpdateRepo}/releases?per_page=30");
            if (!res.IsSuccessStatusCode) return (latestVi, latestEn);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

            var items = new List<(Version v, string vi, string en)>();
            foreach (var r in doc.RootElement.EnumerateArray())
            {
                if (r.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;
                if (r.TryGetProperty("prerelease", out var p) && p.GetBoolean()) continue;
                string t = r.GetProperty("tag_name").GetString() ?? "";
                if (!Version.TryParse(t.TrimStart('v', 'V'), out var v)) continue;
                if (v <= AppInfo.Current || v > latest) continue;
                string bd = r.TryGetProperty("body", out var bb) ? bb.GetString() ?? "" : "";
                var (a, c) = SplitNotes(bd);
                items.Add((v, a, c));
            }
            if (items.Count <= 1) return (latestVi, latestEn);

            items = items.OrderByDescending(x => x.v).ToList();
            string Join(Func<(Version v, string vi, string en), string> pick) =>
                string.Join("\n\n", items.Select(x => $"v{x.v.ToString(3)}\n{pick(x)}"));
            return (Join(x => x.vi), Join(x => x.en));
        }
        catch { return (latestVi, latestEn); }
    }

    /// <summary>Ghi chú phát hành có 2 phần &lt;!--vi--&gt; và &lt;!--en--&gt;; chọn theo ngôn ngữ app.</summary>
    internal static (string vi, string en) SplitNotes(string body)
    {
        string Clean(string t)
        {
            t = t.Trim();
            var lines = t.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            if (lines.Count > 0 && lines[0].TrimStart().StartsWith('#')) lines.RemoveAt(0);
            return string.Join("\n", lines).Trim();
        }
        var mv = Regex.Match(body, @"<!--\s*vi\s*-->(.*?)(?=<!--\s*en\s*-->|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var me = Regex.Match(body, @"<!--\s*en\s*-->(.*?)(?=<!--\s*vi\s*-->|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        string all = Clean(body);
        string vi = mv.Success ? Clean(mv.Groups[1].Value) : all;
        string en = me.Success ? Clean(me.Groups[1].Value) : all;
        return (vi.Length > 0 ? vi : en, en.Length > 0 ? en : vi);
    }

    public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<double> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(TempDir);
        string file = Path.Combine(TempDir, "new.exe");
        if (File.Exists(file)) File.Delete(file);

        using var http = NewClient(TimeSpan.FromMinutes(15));
        using var res = await http.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        long total = res.Content.Headers.ContentLength ?? info.AssetSize;

        await using (var src = await res.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(file))
        {
            var buf = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                if (total > 0) progress.Report(done * 100.0 / total);
            }
        }

        if (info.AssetSize > 0 && new FileInfo(file).Length != info.AssetSize)
            throw new IOException(Loc.Tr("File tải về bị thiếu/hỏng (sai dung lượng).", "Downloaded file is incomplete/corrupt (size mismatch)."));

        if (!string.IsNullOrEmpty(info.Sha256) && info.Sha256.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            string want = info.Sha256[7..];
            await using var fs = File.OpenRead(file);
            string got = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct));
            if (!got.Equals(want, StringComparison.OrdinalIgnoreCase))
                throw new IOException(Loc.Tr("File tải về không khớp mã SHA-256, đã huỷ để an toàn.", "Downloaded file does not match its SHA-256, aborted for safety."));
        }
        return file;
    }

    /// <summary>Thoát app, thay file exe đang chạy bằng bản mới (chờ app thoát hẳn), rồi mở lại.</summary>
    public static void ApplyAndRestart(string newExe)
    {
        string cur = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(cur) || Path.GetFileName(cur).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(Loc.Tr("Đang chạy từ môi trường dev, không tự thay file được.", "Running from a dev environment, cannot replace the file."));

        string script = Path.Combine(Path.GetDirectoryName(newExe)!, "apply_update.cmd");
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine($"set \"NEW={newExe}\"");
        sb.AppendLine($"set \"OLD={cur}\"");
        sb.AppendLine("for /l %%i in (1,1,90) do (");
        sb.AppendLine("  move /y \"%NEW%\" \"%OLD%\" >nul 2>&1");
        sb.AppendLine("  if not exist \"%NEW%\" goto done");
        sb.AppendLine("  ping -n 2 127.0.0.1 >nul");
        sb.AppendLine(")");
        sb.AppendLine("exit /b 1");
        sb.AppendLine(":done");
        sb.AppendLine("start \"\" \"%OLD%\"");
        File.WriteAllText(script, sb.ToString(), new UTF8Encoding(false));

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"\"{script}\"\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });
        Application.Current.Shutdown();
    }
}
