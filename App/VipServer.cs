// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Text.RegularExpressions;
using System.Web;

namespace RobloxRAMManager;

/// <summary>
/// PlaceId/LinkCode đã biết sẵn (link dạng cũ), HOẶC ShareCode chưa giải mã (link dạng /share mới —
/// phải hỏi Roblox lúc đăng nhập mới ra PlaceId/LinkCode thật, xem RobloxLauncher.ResolveShareLinkAsync).
/// </summary>
public sealed record VipLink(long PlaceId, string LinkCode, string? ShareCode = null)
{
    public bool NeedsResolve => !string.IsNullOrEmpty(ShareCode);
}

public enum VipCheck { Valid, Invalid, Unknown }

/// <summary>Đọc & kiểm tra link server VIP (private server). Roblox hiện có 2 dạng:
/// - Dạng cũ: https://www.roblox.com/games/{placeId}/{tên}?privateServerLinkCode={mã}
/// - Dạng mới (mặc định từ nút Share trong game): https://www.roblox.com/share?code={mã}&amp;type=Server
///   (link này KHÔNG chứa sẵn placeId/privateServerLinkCode — phải gọi API resolve-link mới ra được,
///   trước đây app từ chối thẳng dạng này → mất link VIP, tự rơi về vào server thường).</summary>
public static class VipServer
{
    private static readonly Regex PathRx = new(@"^/(?:[a-z]{2}(?:-[a-z]{2})?/)?games/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CodeRx = new(@"^[A-Za-z0-9_\-]{8,}$", RegexOptions.Compiled);
    private static readonly Regex ShareCodeRx = new(@"^[A-Za-z0-9_\-]{6,}$", RegexOptions.Compiled);

    public static bool TryParse(string? text, out VipLink link, out string error)
    {
        link = new VipLink(0, "");
        error = "";
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) { error = Loc.Tr("Link trống.", "Link is empty."); return false; }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
        { error = Loc.Tr("Không phải link hợp lệ (phải bắt đầu bằng https://www.roblox.com/games/... hoặc https://www.roblox.com/share?...).", "Not a valid URL (must start with https://www.roblox.com/games/... or https://www.roblox.com/share?...)."); return false; }

        var host = uri.Host.ToLowerInvariant();
        if (host != "roblox.com" && !host.EndsWith(".roblox.com"))
        { error = Loc.Tr("Link không thuộc roblox.com.", "Link is not on roblox.com."); return false; }

        if (uri.AbsolutePath.StartsWith("/share", StringComparison.OrdinalIgnoreCase))
        {
            var sqs = HttpUtility.ParseQueryString(uri.Query);
            var scode = sqs["code"];
            var stype = sqs["type"];
            if (string.IsNullOrEmpty(scode) || !ShareCodeRx.IsMatch(scode))
            { error = Loc.Tr("Link share thiếu mã hoặc mã bị cắt/sai (dạng .../share?code=...&type=Server).", "Share link is missing its code, or the code is truncated/invalid (.../share?code=...&type=Server)."); return false; }
            if (!string.IsNullOrEmpty(stype) && !stype.Equals("Server", StringComparison.OrdinalIgnoreCase))
            { error = Loc.Tr("Đây không phải link share server (vd link mời bạn bè vào game) — cần link \"Server\" (chia sẻ từ server VIP).", "This isn't a server share link (e.g. a friend-invite link) — need a \"Server\" share link (shared from the VIP server)."); return false; }

            // Chưa biết PlaceId/LinkCode thật — sẽ giải mã bằng cookie của acc lúc đăng nhập/mở acc.
            link = new VipLink(0, "", scode);
            return true;
        }

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
