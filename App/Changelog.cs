// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Reflection;
using System.Text.Json;

namespace RobloxRAMManager;

public sealed record ChangelogEntry(Version Version, string Date, string[] Vi, string[] En);

/// <summary>Lịch sử cập nhật: đọc CHANGELOG.json được nhúng trong exe (xem offline được).</summary>
public static class Changelog
{
    public static List<ChangelogEntry> Load()
    {
        var list = new List<ChangelogEntry>();
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("changelog.json");
            if (s == null) return list;
            using var doc = JsonDocument.Parse(s, new JsonDocumentOptions { AllowTrailingCommas = true });
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (!e.TryGetProperty("version", out var vp) || !Version.TryParse(vp.GetString(), out var v)) continue;
                string date = e.TryGetProperty("date", out var dp) && dp.ValueKind == JsonValueKind.String ? dp.GetString() ?? "" : "";
                list.Add(new ChangelogEntry(v, date, Strs(e, "vi"), Strs(e, "en")));
            }
        }
        catch { }
        return list.OrderByDescending(x => x.Version).ToList();
    }

    private static string[] Strs(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var a) || a.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        return a.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray();
    }
}
