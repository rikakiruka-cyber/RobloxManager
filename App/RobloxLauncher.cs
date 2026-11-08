// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace RobloxRAMManager;

/// <summary>Đăng nhập tài khoản Roblox (web API chính thức) và mở game/app bằng auth ticket.</summary>
public static class RobloxLauncher
{
    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false })
    { Timeout = TimeSpan.FromSeconds(25) };

    /// <summary>
    /// Đảm bảo acc có cookie còn sống: thử cookie cũ → đăng nhập ngầm bằng user/mk →
    /// nếu Roblox đòi captcha/2FA thì gọi browserLogin (cửa sổ đăng nhập tự điền sẵn user/mk).
    /// </summary>
    public static async Task<(bool ok, string msg)> EnsureLoginAsync(AccountEntry acc, Func<AccountEntry, Task<bool>>? browserLogin)
    {
        try
        {
            if (!string.IsNullOrEmpty(acc.Cookie) && await IsCookieValidAsync(acc.Cookie))
                return (true, Loc.Tr("Tài khoản đã đăng nhập sẵn.", "Account is already signed in."));

            if (string.IsNullOrEmpty(acc.Password))
                return (false, Loc.Tr("Cookie hết hạn và chưa có mật khẩu để đăng nhập lại.", "Cookie expired and no password saved to sign in again."));

            var (cookie, err, needBrowser) = await LoginAsync(acc.Username, acc.Password);
            if (cookie == null && needBrowser && browserLogin != null && await browserLogin(acc))
                cookie = acc.Cookie;
            if (string.IsNullOrEmpty(cookie)) return (false, err);

            acc.Cookie = cookie;
            AccountVault.Save();
            return (true, Loc.Tr("Đăng nhập thành công.", "Signed in."));
        }
        catch (Exception ex)
        {
            return (false, Loc.Tr("Lỗi: ", "Error: ") + ex.Message);
        }
    }

    /// <summary>placeId &lt;= 0 → mở thẳng app Roblox (đã đăng nhập sẵn), không cần Place ID.
    /// <paramref name="client"/> khác null → mở thẳng bằng đúng client đó (có kiểm tra trước) thay vì
    /// qua trình xử lý URI mặc định của Windows.</summary>
    public static async Task<(bool ok, string msg)> LaunchAsync(AccountEntry acc, long placeId, Func<AccountEntry, Task<bool>>? browserLogin = null, string? vipLinkCode = null, ClientInfo? client = null)
    {
        try
        {
            string? ticket = null;
            if (!string.IsNullOrEmpty(acc.Cookie))
                ticket = await GetTicketAsync(acc.Cookie, placeId);

            if (ticket == null && !string.IsNullOrEmpty(acc.Cookie) && await IsCookieValidAsync(acc.Cookie))
                return (false, Loc.Tr("Roblox chưa trả vé mở game (lỗi tạm thời), thử lại sau ít giây.", "Roblox did not return a launch ticket (temporary error), try again in a few seconds."));

            if (ticket == null)
            {
                var oldCookie = acc.Cookie;
                acc.Cookie = ""; // cookie hết hạn → buộc đăng nhập lại
                var (ok, msg) = await EnsureLoginAsync(acc, browserLogin);
                if (!ok)
                {
                    // Đăng nhập lại thất bại (mất mạng, bị giới hạn tốc độ...) → trả cookie cũ về, đừng làm mất phiên đã lưu
                    acc.Cookie = oldCookie;
                    return (false, msg);
                }
                ticket = await GetTicketAsync(acc.Cookie, placeId);
                if (ticket == null)
                    return (false, Loc.Tr("Đăng nhập được nhưng không lấy được vé mở game.", "Signed in but could not get a launch ticket."));
            }

            long tracker = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string uri;
            if (placeId > 0)
            {
                string launcher = string.IsNullOrEmpty(vipLinkCode)
                    ? $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId={tracker}&placeId={placeId}&isPlayTogetherGame=false"
                    : $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestPrivateGame&browserTrackerId={tracker}&placeId={placeId}&accessCode=&linkCode={Uri.EscapeDataString(vipLinkCode)}";
                uri = $"roblox-player:1+launchmode:play+gameinfo:{ticket}+launchtime:{now}" +
                      $"+placelauncherurl:{Uri.EscapeDataString(launcher)}+browsertrackerid:{tracker}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
            }
            else
            {
                // Không có Place ID: mở app Roblox (màn hình chính) với acc đã đăng nhập
                uri = $"roblox-player:1+launchmode:app+gameinfo:{ticket}+launchtime:{now}" +
                      $"+browsertrackerid:{tracker}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
            }
            if (client != null)
                return await ClientManager.LaunchAndVerifyAsync(client, uri);

            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            return (true, Loc.Tr("Đã gửi lệnh mở.", "Launch command sent."));
        }
        catch (Exception ex)
        {
            return (false, Loc.Tr("Lỗi: ", "Error: ") + ex.Message);
        }
    }

    /// <summary>Hỏi Roblox xem link server VIP còn dùng được không (không mở game). Không chắc chắn → Unknown.</summary>
    public static async Task<(VipCheck result, string msg)> CheckVipAsync(string cookie, long placeId, string linkCode)
    {
        try
        {
            string body = JsonSerializer.Serialize(new { placeId, isTeleport = false, gameJoinAttemptId = Guid.NewGuid().ToString(), accessCode = "", linkCode });
            string? csrf = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://gamejoin.roblox.com/v1/join-private-game");
                req.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
                req.Headers.TryAddWithoutValidation("User-Agent", "Roblox/WinInet");
                req.Headers.TryAddWithoutValidation("Referer", $"https://www.roblox.com/games/{placeId}");
                if (csrf != null) req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                using var res = await Http.SendAsync(req);

                if (res.StatusCode == System.Net.HttpStatusCode.Forbidden && csrf == null &&
                    res.Headers.TryGetValues("x-csrf-token", out var c))
                { csrf = c.First(); continue; }

                if (!res.IsSuccessStatusCode)
                    return (VipCheck.Unknown, Loc.Tr($"Không kiểm tra được (HTTP {(int)res.StatusCode}).", $"Could not verify (HTTP {(int)res.StatusCode})."));

                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                int status = root.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.Number ? st.GetInt32() : -1;
                string message = root.TryGetProperty("message", out var ms) ? ms.GetString() ?? "" : "";

                if (status == 2) return (VipCheck.Valid, Loc.Tr("Link VIP hợp lệ.", "VIP link is valid."));
                if (status is 0 or 1 or 5 or -1) // đang chờ / thử lại / server đầy / không rõ → không kết luận là sai
                    return (VipCheck.Unknown, Loc.Tr($"Chưa xác nhận được link ({message}).", $"Link not confirmed ({message})."));
                return (VipCheck.Invalid, Loc.Tr($"Roblox từ chối link VIP: {message} (status {status}).", $"Roblox rejected the VIP link: {message} (status {status})."));
            }
        }
        catch (Exception ex)
        {
            return (VipCheck.Unknown, Loc.Tr("Không kiểm tra được: ", "Could not verify: ") + ex.Message);
        }
        return (VipCheck.Unknown, "");
    }

    private static async Task<bool> IsCookieValidAsync(string cookie)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
            req.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
            using var res = await Http.SendAsync(req);
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private static async Task<string?> GetTicketAsync(string cookie, long placeId)
    {
        string? csrf = null;
        string referer = placeId > 0 ? $"https://www.roblox.com/games/{placeId}" : "https://www.roblox.com/home";
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket");
            req.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
            req.Headers.TryAddWithoutValidation("Referer", referer);
            req.Headers.TryAddWithoutValidation("Origin", "https://www.roblox.com");
            if (csrf != null) req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            using var res = await Http.SendAsync(req);
            if (res.StatusCode == System.Net.HttpStatusCode.Forbidden && res.Headers.TryGetValues("x-csrf-token", out var c))
            { csrf = c.First(); continue; }
            if (res.Headers.TryGetValues("rbx-authentication-ticket", out var t)) return t.First();
            return null;
        }
        return null;
    }

    private static async Task<(string? cookie, string error, bool needBrowser)> LoginAsync(string user, string pass)
    {
        string body = JsonSerializer.Serialize(new { ctype = "Username", cvalue = user, password = pass });
        string? csrf = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v2/login");
            if (csrf != null) req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var res = await Http.SendAsync(req);

            if (res.StatusCode == System.Net.HttpStatusCode.Forbidden && csrf == null &&
                res.Headers.TryGetValues("x-csrf-token", out var c))
            { csrf = c.First(); continue; }

            if (res.IsSuccessStatusCode && res.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (var ck in cookies)
                    if (ck.StartsWith(".ROBLOSECURITY=", StringComparison.Ordinal))
                    {
                        var v = ck[".ROBLOSECURITY=".Length..];
                        int end = v.IndexOf(';');
                        return (end >= 0 ? v[..end] : v, "", false);
                    }
            }

            // Captcha / 2FA / xác minh thiết bị → cần cửa sổ trình duyệt (chỉ phải làm 1 lần)
            if (res.Headers.Contains("rblx-challenge-id"))
                return (null, Loc.Tr("Roblox yêu cầu captcha/2FA.", "Roblox requires captcha/2FA."), true);

            string text = await res.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(text);
                var msg = doc.RootElement.GetProperty("errors")[0].GetProperty("message").GetString();
                bool captcha = msg != null && msg.Contains("captcha", StringComparison.OrdinalIgnoreCase);
                return (null, Loc.Tr("Đăng nhập thất bại: ", "Sign-in failed: ") + msg, captcha);
            }
            catch
            {
                return (null, Loc.Tr($"Đăng nhập thất bại (HTTP {(int)res.StatusCode}).", $"Sign-in failed (HTTP {(int)res.StatusCode})."), false);
            }
        }
        return (null, Loc.Tr("Đăng nhập thất bại.", "Sign-in failed."), false);
    }
}
