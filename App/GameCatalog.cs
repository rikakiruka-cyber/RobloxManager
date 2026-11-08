// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace RobloxRAMManager;

/// <summary>Một game đã từng chơi. Tên lưu ở đây chỉ để dự phòng khi mất mạng — tên/ảnh hiển thị luôn lấy mới từ Roblox.</summary>
public sealed class GameEntry
{
    public long PlaceId { get; set; }
    public long UniverseId { get; set; }
    public string Name { get; set; } = "";
    public DateTime LastPlayed { get; set; } = DateTime.Now;
}

/// <summary>Thông tin game lấy trực tiếp từ Roblox ở thời điểm hiện tại.</summary>
public sealed class GameLive
{
    public string Name { get; set; } = "";
    public string? IconUrl { get; set; }
}

/// <summary>Lịch sử game đã chơi + lấy tên/ảnh mới nhất từ API công khai của Roblox (không cần đăng nhập).</summary>
public static class GameCatalog
{
    private const int MaxHistory = 30;
    private static readonly object Gate = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static List<GameEntry> _history = new();
    private static bool _loaded;

    private static string PathFile => DataStore.FilePath("games.json");

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (File.Exists(PathFile))
                _history = JsonSerializer.Deserialize<List<GameEntry>>(File.ReadAllText(PathFile)) ?? new();
        }
        catch { _history = new(); }
    }

    private static void Save()
    {
        try { File.WriteAllText(PathFile, JsonSerializer.Serialize(_history)); } catch { }
    }

    /// <summary>Danh sách game đã chơi, mới chơi gần nhất ở trên cùng (bản sao an toàn).</summary>
    public static List<GameEntry> Snapshot()
    {
        lock (Gate)
        {
            EnsureLoaded();
            return _history.OrderByDescending(g => g.LastPlayed).ToList();
        }
    }

    public static GameEntry? Find(long placeId)
    {
        lock (Gate) { EnsureLoaded(); return _history.FirstOrDefault(g => g.PlaceId == placeId); }
    }

    /// <summary>Ghi nhận vừa chơi game này (thêm mới hoặc cập nhật thời điểm chơi).</summary>
    public static void Record(long placeId)
    {
        if (placeId <= 0) return;
        lock (Gate)
        {
            EnsureLoaded();
            var g = _history.FirstOrDefault(x => x.PlaceId == placeId);
            if (g == null) _history.Add(g = new GameEntry { PlaceId = placeId });
            g.LastPlayed = DateTime.Now;
            if (_history.Count > MaxHistory)
                _history = _history.OrderByDescending(x => x.LastPlayed).Take(MaxHistory).ToList();
            Save();
        }
    }

    public static void Clear()
    {
        lock (Gate) { EnsureLoaded(); _history.Clear(); Save(); }
    }

    /// <summary>Hỏi Roblox tên + ảnh MỚI NHẤT của các game (mỗi lần gọi là lấy mới, không dùng bản cũ).
    /// Lỗi mạng → trả về những gì lấy được; UI tự dùng tên đã lưu để dự phòng.</summary>
    public static async Task<Dictionary<long, GameLive>> FetchLiveAsync(IEnumerable<GameEntry> entries)
    {
        var result = new Dictionary<long, GameLive>();
        var list = entries.ToList();
        if (list.Count == 0) return result;
        try
        {
            // 1) placeId → universeId (chỉ cần hỏi 1 lần, lưu lại)
            await Task.WhenAll(list.Where(g => g.UniverseId == 0).Select(async g =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(await Http.GetStringAsync(
                        $"https://apis.roblox.com/universes/v1/places/{g.PlaceId}/universe"));
                    if (doc.RootElement.TryGetProperty("universeId", out var u) && u.ValueKind == JsonValueKind.Number)
                        g.UniverseId = u.GetInt64();
                }
                catch { }
            }));

            var withUni = list.Where(g => g.UniverseId > 0).ToList();
            if (withUni.Count == 0) return result;
            string ids = string.Join(",", withUni.Select(g => g.UniverseId).Distinct());

            // 2) tên game hiện tại + 3) ảnh icon hiện tại — hỏi song song
            var namesTask = Http.GetStringAsync($"https://games.roblox.com/v1/games?universeIds={ids}");
            var iconsTask = Http.GetStringAsync(
                $"https://thumbnails.roblox.com/v1/games/icons?universeIds={ids}&returnPolicy=PlaceHolder&size=128x128&format=Png&isCircular=false");

            var names = new Dictionary<long, string>();
            var icons = new Dictionary<long, string>();
            try
            {
                using var d = JsonDocument.Parse(await namesTask);
                foreach (var it in d.RootElement.GetProperty("data").EnumerateArray())
                    names[it.GetProperty("id").GetInt64()] = it.GetProperty("name").GetString() ?? "";
            }
            catch { }
            try
            {
                using var d = JsonDocument.Parse(await iconsTask);
                foreach (var it in d.RootElement.GetProperty("data").EnumerateArray())
                {
                    string? url = it.TryGetProperty("imageUrl", out var iu) ? iu.GetString() : null;
                    if (!string.IsNullOrEmpty(url)) icons[it.GetProperty("targetId").GetInt64()] = url;
                }
            }
            catch { }

            bool changed = false;
            foreach (var g in withUni)
            {
                names.TryGetValue(g.UniverseId, out var name);
                icons.TryGetValue(g.UniverseId, out var icon);
                result[g.PlaceId] = new GameLive { Name = name ?? g.Name, IconUrl = icon };
                if (!string.IsNullOrEmpty(name) && g.Name != name) { g.Name = name; changed = true; }
            }
            lock (Gate)
            {
                // đồng bộ universeId/tên mới vào lịch sử đã lưu
                foreach (var g in list)
                {
                    var stored = _history.FirstOrDefault(x => x.PlaceId == g.PlaceId);
                    if (stored != null && (stored.UniverseId != g.UniverseId || stored.Name != g.Name))
                    { stored.UniverseId = g.UniverseId; stored.Name = g.Name; changed = true; }
                }
                if (changed) Save();
            }
        }
        catch { /* mất mạng → dùng tên đã lưu */ }
        return result;
    }
}
