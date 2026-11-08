// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Text.RegularExpressions;
using System.Web;

namespace RobloxRAMManager;

public sealed record VipLink(long PlaceId, string LinkCode);

public enum VipCheck { Valid, Invalid, Unknown }

/// <summary>Đọc & kiểm tra link server VIP (private server) dạng
/// https://www.roblox.com/games/{placeId}/{tên}?privateServerLinkCode={mã}</summary>
public static class VipServer
{
    private static readonly Regex PathRx = new(@"^/(?:[a-z]{2}(?:-[a-z]{2})?/)?games/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CodeRx = new(@"^[A-Za-z0-9_\-]{8,}$", RegexOptions.Compiled);

    public static bool TryParse(string? text, out VipLink link, out string error)
    {
        link = new VipLink(0, "");
        error = "";
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) { error = Loc.Tr("Link trống.", "Link is empty."); return false; }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
        { error = Loc.Tr("Không phải link hợp lệ (phải bắt đầu bằng https://www.roblox.com/games/...).", "Not a valid URL (must start with https://www.roblox.com/games/...)."); return false; }

        var host = uri.Host.ToLowerInvariant();
        if (host != "roblox.com" && !host.EndsWith(".roblox.com"))
        { error = Loc.Tr("Link không thuộc roblox.com.", "Link is not on roblox.com."); return false; }

        if (uri.AbsolutePath.StartsWith("/share", StringComparison.OrdinalIgnoreCase))
        { error = Loc.Tr("Link dạng /share chưa hỗ trợ. Hãy dùng link server VIP dạng .../games/ID/...?privateServerLinkCode=...", "/share links are not supported. Use the VIP link form .../games/ID/...?privateServerLinkCode=..."); return false; }

        var m = PathRx.Match(uri.AbsolutePath);
        if (!m.Success || !long.TryParse(m.Groups[1].Value, out var place) || place <= 0)
        { error = Loc.Tr("Không tìm thấy Place ID trong link (dạng .../games/123456/...).", "No Place ID found in the link (.../games/123456/...)."); return false; }

        var code = HttpUtility.ParseQueryString(uri.Query)["privateServerLinkCode"];
        if (string.IsNullOrEmpty(code))
        { error = Loc.Tr("Link thiếu privateServerLinkCode — đây không phải link server VIP.", "Link has no privateServerLinkCode — this is not a VIP server link."); return false; }
        if (!CodeRx.IsMatch(code))
        { error = Loc.Tr("Mã privateServerLinkCode có ký tự lạ hoặc quá ngắn, link có thể bị cắt/sai.", "privateServerLinkCode looks malformed/truncated."); return false; }

        link = new VipLink(place, code);
        return true;
    }
}
