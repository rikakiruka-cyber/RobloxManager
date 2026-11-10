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
    // file → (số byte đã xử lý xong, PlaceId gần nhất)
    private static readonly Dictionary<string, (long offset, long place)> Cache = new();
    private static readonly object CacheLock = new();
    private const int Chunk = 256 * 1024;
    private const long MaxPerCall = 8L * 1024 * 1024; // mỗi lượt đọc tối đa 8 MB, phần còn lại để lượt sau

    private static void ScanLine(string line, ref long place)
    {
        if (!line.Contains("place", StringComparison.OrdinalIgnoreCase)) return;
        var m = JoinRx.Match(line);
        if (m.Success && long.TryParse(m.Groups[1].Value, out var id)) { place = id; return; }
        if (place == 0)
        {
            var a = AltRx.Match(line);
            if (a.Success && long.TryParse(a.Groups[1].Value, out var id2)) place = id2;
        }
    }

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
            long offset = 0, place = 0;
            lock (CacheLock)
            {
                if (Cache.TryGetValue(file.FullName, out var c) && file.Length >= c.offset)
                {
                    offset = c.offset; place = c.place;
                    if (offset == file.Length) return place; // không có dữ liệu mới → không mở file
                }
            }

            // Chỉ đọc phần MỚI, chỉ xử lý dòng đã kết thúc bằng '\n'; offset = số byte đã xử lý thật sự
            // (dùng fs.Position sau StreamReader sẽ lệch do buffer → đọc lại gần hết file mỗi lần).
            using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var buf = new byte[Chunk];
                long done = 0;
                while (done < MaxPerCall)
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    int n = fs.Read(buf, 0, Chunk);
                    if (n <= 0) break;
                    int last = Array.LastIndexOf(buf, (byte)'\n', n - 1);
                    if (last < 0)
                    {
                        if (n < Chunk) break;     // dòng dang ghi dở → chờ lượt sau
                        offset += n; done += n;   // dòng dài bất thường → bỏ qua
                        continue;
                    }
                    foreach (var line in System.Text.Encoding.UTF8.GetString(buf, 0, last + 1).Split('\n'))
                        if (line.Length > 0) ScanLine(line, ref place);
                    offset += last + 1; done += last + 1;
                    if (n < Chunk) break;
                }
            }

            lock (CacheLock)
            {
                if (Cache.Count > 200) Cache.Clear();
                Cache[file.FullName] = (offset, place);
            }
            return place;
        }
        catch { return 0; }
    }
}
