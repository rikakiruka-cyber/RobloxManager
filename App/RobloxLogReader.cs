// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.IO;
using System.Text.RegularExpressions;

namespace RobloxRAMManager;

/// <summary>
/// Đọc log của Roblox (%LOCALAPPDATA%\Roblox\logs) để biết acc đang ở game nào (PlaceId),
/// kể cả khi mở bằng app / tự bấm vào game — để lần sau mở lại hoặc khi bị văng thì vào đúng game cũ.
/// </summary>
public static class RobloxLogReader
{
    private static readonly Regex JoinRx = new(@"Joining game '[^']*' place (\d+) at", RegexOptions.Compiled);
    private static readonly Regex AltRx = new(@"placeId[""':=\s]+(\d{5,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Dictionary<string, (long len, long place)> Cache = new();

    public static long FindPlaceId(DateTime procStart)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
            if (!Directory.Exists(dir)) return 0;

            // Log của đúng tiến trình này được tạo ngay sau khi nó khởi động
            var file = new DirectoryInfo(dir).EnumerateFiles("*Player*.log")
                .Where(f => f.CreationTime >= procStart.AddSeconds(-5) && f.CreationTime <= procStart.AddSeconds(120))
                .OrderBy(f => Math.Abs((f.CreationTime - procStart).TotalSeconds))
                .FirstOrDefault();
            if (file == null) return 0;

            file.Refresh();
            if (Cache.TryGetValue(file.FullName, out var c) && c.len == file.Length) return c.place;

            long place = 0;
            using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                var m = JoinRx.Match(line);
                if (m.Success && long.TryParse(m.Groups[1].Value, out var id)) { place = id; continue; }
                if (place == 0 && line.Contains("placeId", StringComparison.OrdinalIgnoreCase))
                {
                    var a = AltRx.Match(line);
                    if (a.Success && long.TryParse(a.Groups[1].Value, out var id2)) place = id2;
                }
            }
            Cache[file.FullName] = (file.Length, place);
            return place;
        }
        catch { return 0; }
    }
}
