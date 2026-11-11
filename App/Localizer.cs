// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Windows;
using System.Windows.Controls;

namespace RobloxRAMManager;

/// <summary>Đa ngôn ngữ VI/EN. Giữ XAML tiếng Việt gốc, dịch bằng từ điển khi chạy.</summary>
public static class Loc
{
    public static string Lang { get; set; } = "vi";
    public static bool IsEn => Lang == "en";

    /// <summary>Chọn chuỗi theo ngôn ngữ hiện tại (dùng cho chuỗi sinh trong code).</summary>
    public static string Tr(string vi, string en) => IsEn ? en : vi;

    private static readonly Dictionary<string, string> Map = new()
    {
        // Title bar / sidebar tooltips
        ["Gửi báo cáo Discord ngay"] = "Send Discord report now",
        ["Gửi Discord"] = "Send Discord",
        ["Thu nhỏ"] = "Minimize",
        ["Phóng to / Thu về"] = "Maximize / Restore",
        ["Đóng"] = "Close",
        ["Tổng quan hệ thống (Overview)"] = "System overview",
        ["Cấu hình Discord Webhook Bot"] = "Discord Webhook Bot settings",
        ["Tối ưu nhanh toàn bộ (Trim + Cap CPU)"] = "Quick optimize all (Trim + CPU cap)",
        ["Trợ giúp / Help"] = "Help",
        ["Cài đặt hệ thống (Settings)"] = "Settings",
        // Overview
        ["Tổng quan"] = "Overview",
        ["RAM HỆ THỐNG"] = "SYSTEM RAM",
        ["CPU HỆ THỐNG"] = "SYSTEM CPU",
        ["TÀI KHOẢN TREO"] = "IDLE ACCOUNTS",
        ["Đang chạy:"] = "Running:",
        ["Crash / Văng:"] = "Crashed:",
        ["Tối ưu toàn bộ (Trim + Cap CPU 40% + 2 Cores)"] = "Optimize all (Trim + CPU cap 40% + 2 cores)",
        ["Tối Ưu Tất Cả"] = "Optimize All",
        ["Giải phóng RAM toàn bộ Roblox"] = "Free RAM of all Roblox instances",
        ["Tự Mở Lại Khi Văng"] = "Auto Relaunch on Crash",
        ["DANH SÁCH ROBLOX ĐANG TREO  ·  nhấp đúp để mở chi tiết"] = "RUNNING ROBLOX LIST  ·  double-click for details",
        ["↻ Quét lại"] = "↻ Rescan",
        ["Tên cửa sổ"] = "Window title",
        ["ĐÃ TREO"] = "UPTIME",
        ["Trạng thái"] = "Status",
        // Account detail
        ["● Đang hoạt động"] = "● Running",
        ["← Về Tổng Quan"] = "← Back to Overview",
        ["THỜI GIAN ĐÃ TREO"] = "RUNNING TIME",
        ["BỘ NHỚ RAM TIÊU THỤ"] = "RAM USAGE",
        ["MỨC ĐỘ ĂN CPU"] = "CPU USAGE",
        ["Tối Ưu Acc Này"] = "Optimize This Acc",
        ["🧹 Dọn RAM Acc Này"] = "🧹 Trim This Acc's RAM",
        ["🪟 Mở Lên Màn Hình"] = "🪟 Bring To Front",
        ["✕ Tắt Acc Này"] = "✕ Kill This Acc",
        // Discord view
        ["CẤU HÌNH DISCORD WEBHOOK BOT"] = "DISCORD WEBHOOK BOT SETTINGS",
        ["Tự động gửi thông số thời gian chạy, số acc đang treo và mức độ ăn RAM/CPU lên Discord."] = "Automatically sends uptime, idle account count and RAM/CPU usage to Discord.",
        ["Tần suất tự động gửi báo cáo"] = "Auto report interval",
        ["Tắt (Chỉ gửi thủ công)"] = "Off (manual only)",
        ["Mỗi 5 phút"] = "Every 5 min",
        ["Mỗi 15 phút"] = "Every 15 min",
        ["Mỗi 30 phút"] = "Every 30 min",
        ["Mỗi 1 giờ"] = "Every 1 hour",
        ["Mỗi 2 giờ"] = "Every 2 hours",
        ["Tự động gửi cảnh báo khẩn cấp ngay khi có acc Roblox bị văng/crash"] = "Send an urgent alert as soon as a Roblox account crashes",
        ["💾 Lưu Cấu Hình"] = "💾 Save Settings",
        ["🚀 Gửi Thử Ngay"] = "🚀 Send Test Now",
        ["← Quay lại"] = "← Back",
        // Settings panel
        ["CÀI ĐẶT HỆ THỐNG"] = "SYSTEM SETTINGS",
        ["CHỦ ĐỀ GIAO DIỆN"] = "THEME",
        ["HIỆU ỨNG"] = "EFFECTS",
        ["Aurora chuyển động (tắt để nhẹ máy hơn)"] = "Animated Aurora (turn off to be lighter)",
        ["HIỆU NĂNG & TỐI ƯU"] = "PERFORMANCE & OPTIMIZATION",
        ["Giới hạn CPU (Hard Cap mỗi Acc, % tổng CPU máy, 100 = tắt)"] = "CPU limit (hard cap per acc, % of total CPU, 100 = off)",
        ["Giới hạn RAM cứng (mỗi Acc, tối thiểu 200 MB)"] = "Hard RAM limit (per acc, min 200 MB)",
        ["Giới hạn CPU (Hard Cap mỗi Acc nền, % tổng CPU máy, 100 = tắt)"] = "CPU limit (hard cap per background acc, % of total CPU, 100 = off)",
        ["Giới hạn CPU cho acc nền (100 = tắt). Đặt dưới 30% có thể gây rớt mạng/ngắt kết nối."] = "CPU limit for background accounts (100 = off). Below 30% may cause network drops/disconnects.",
        ["Giới hạn RAM cứng (mỗi Acc nền, 0 = tắt, tối thiểu 256 MB, bước 256 MB)"] = "Hard RAM limit (per background acc, 0 = off, min 256 MB, 256 MB steps)",
        ["Áp giới hạn RAM cả acc đang chơi (mặc định chỉ acc nền; acc đang chơi có thể giật nếu cần nhiều RAM hơn mức giới hạn)"] = "Apply RAM limit to the active account too (default: background accounts only; the active account may stutter if it needs more RAM than the limit)",
        ["Giới hạn RAM cứng (0 = tắt). Dưới 1 GB Roblox có thể đứng hình do page-fault liên tục."] = "Hard RAM limit (0 = off). Below 1 GB Roblox may freeze from constant page faults.",
        ["Tự động dọn RAM (Auto-Trim)"] = "Auto RAM trim (Auto-Trim)",
        ["Mỗi 5 giây"] = "Every 5 sec",
        ["Mỗi 10 giây"] = "Every 10 sec",
        ["Mỗi 20 giây"] = "Every 20 sec",
        ["Mỗi 30 giây"] = "Every 30 sec",
        ["Tắt"] = "Off",
        ["Tần suất quét tiến trình (Poll)"] = "Process scan rate (Poll)",
        ["1 giây"] = "1 sec",
        ["2 giây"] = "2 sec",
        ["5 giây"] = "5 sec",
        ["NGÔN NGỮ"] = "LANGUAGE",
        ["THÔNG TIN"] = "ABOUT",
        ["Hỗ trợ tối ưu RAM, CPU, giới hạn core, phát hiện crash và thông báo qua Discord Webhook bot."]
            = "Optimizes RAM, CPU, core limits, detects crashes and notifies via Discord Webhook bot.",
        ["Quản lý tài khoản Roblox"] = "Roblox account manager",
        ["QUẢN LÝ TÀI KHOẢN ROBLOX"] = "ROBLOX ACCOUNT MANAGER",
        ["Lưu tài khoản (mã hoá AES-256) để tự đăng nhập và mở lại game khi bị văng."] = "Save accounts (AES-256 encrypted) to auto sign in and reopen the game after a crash.",
        ["Tài khoản đã lưu"] = "Saved accounts",
        ["Tên đăng nhập"] = "Username",
        ["Mật khẩu"] = "Password",
        ["Tự mở lại acc này khi bị văng"] = "Auto reopen this account on crash",
        ["💾 Lưu acc"] = "💾 Save account",
        ["➕ Acc mới"] = "➕ New account",
        ["🗑 Xoá"] = "🗑 Delete",
        ["DỮ LIỆU"] = "DATA",
        ["📁 Mở thư mục"] = "📁 Open folder",
        ["🔀 Đổi nơi lưu"] = "🔀 Change location",
        ["TRỢ GIÚP"] = "HELP",
        ["● Không phản hồi"] = "● Not responding",
        ["Discord ID của bạn (để bot ping khi có sự cố quan trọng)"] = "Your Discord ID (so the bot can ping you on important issues)",
        ["Chỉ ping khi: acc văng mà không mở lại được, hoặc link server VIP bị sai. Lấy ID: Discord → Cài đặt → Nâng cao → bật Chế độ nhà phát triển → chuột phải vào tên bạn → Sao chép ID người dùng."] = "Pings only when an account crashes and cannot be reopened, or the VIP server link is wrong. Get your ID: Discord → Settings → Advanced → enable Developer Mode → right-click your name → Copy User ID.",
        ["Nhấp đúp vào acc để mở ngay"] = "Double-click an account to open it right away",
        ["Chỉ cần nhập tên + mật khẩu rồi bấm Lưu. App tự đăng nhập, lần sau bấm Mở là vào luôn (không cần Place ID/cookie). Mật khẩu để trống = giữ giá trị đã lưu. Muốn thêm acc khác: gõ tên + mật khẩu mới rồi bấm Lưu — acc cũ vẫn còn (hoặc bấm ➕ Acc mới)."] = "Just enter username + password and press Save. The app signs in by itself; next time just press Open (no Place ID/cookie needed). Empty password = keep the saved one. To add another account: type its username + password and press Save — existing accounts stay (or press ➕ New account).",
        ["Nâng cao (tuỳ chọn)"] = "Advanced (optional)",
        ["Place ID hoặc link game (để trống = mở thẳng app Roblox)"] = "Place ID or game link (empty = open the Roblox app directly)",
        ["Cookie .ROBLOSECURITY (chỉ khi muốn dán tay)"] = "Cookie .ROBLOSECURITY (only if you want to paste it manually)",
        ["Link server VIP (khi bị văng sẽ kiểm tra link rồi vào lại đúng server này)"] = "VIP server link (on a crash the link is checked, then the same server is rejoined)",
        ["Chỉ mở lại khi game tự tắt/văng — tự tay đóng game thì không mở lại (áp dụng mọi acc)"] = "Reopen only when the game closes by itself/crashes — closing it yourself does nothing (all accounts)",
        ["🚀 Mở acc"] = "🚀 Open account",
        ["🚀 Mở tất cả"] = "🚀 Open all",
        ["CẬP NHẬT"] = "UPDATES",
        ["Mở Discord của tác giả"] = "Open the author's Discord",
        ["Tự kiểm tra bản cập nhật khi mở app"] = "Check for updates when the app starts",
        ["🔄 Kiểm tra cập nhật"] = "🔄 Check for updates",
        ["📜 Lịch sử cập nhật"] = "📜 Update history",
        // --- Bổ sung: các chuỗi còn sót (tránh lẫn tiếng Việt khi chọn English) ---
        ["SERVER VIP CỦA ACC NÀY"] = "THIS ACCOUNT'S VIP SERVER",
        ["Dán link server VIP đầy đủ (từ nút Share trong game hoặc link có "] = "Paste the full VIP server link (from the in-game Share button, or a link containing ",
        ["). Khi acc này bị văng, app tự kiểm tra link rồi mở lại ĐÚNG server VIP này — không bỏ trống nếu không muốn bị rơi vào server thường."]
            = "). When this account crashes, the app checks the link and reopens that exact VIP server — don't leave it empty unless you're fine with landing in a public server.",
        ["https://www.roblox.com/share?code=...&type=Server  hoặc  https://www.roblox.com/games/ID?privateServerLinkCode=..."]
            = "https://www.roblox.com/share?code=...&type=Server  or  https://www.roblox.com/games/ID?privateServerLinkCode=...",
        ["Chọn client để mở Roblox"] = "Choose the client used to open Roblox",
        ["Chọn game đã từng chơi"] = "Pick a game you've played",
        ["Client dùng để mở"] = "Client to launch with",
        ["Quét lại máy tìm client Roblox"] = "Scan this PC for Roblox clients",
        ["🔄 Quét lại"] = "🔄 Rescan",
        ["Game để vào (đã từng chơi)"] = "Game to join (previously played)",
        ["Bấm để chọn game đã từng chơi (tên/ảnh luôn lấy mới từ Roblox)"] = "Click to pick a game you've played (name/thumbnail always fetched fresh from Roblox)",
        ["Chưa chọn game (mở thẳng app Roblox)"] = "No game selected (opens the Roblox app)",
        ["Bắt đầu: --:--"] = "Started: --:--",
        ["Đỉnh: 0 MB · Private: 0 MB"] = "Peak: 0 MB · Private: 0 MB",
        ["Độ ưu tiên: Normal · Giới hạn: 40%"] = "Priority: Normal · Limit: 40%",
    };

    private static readonly Dictionary<string, string> Rev =
        Map.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>Dịch một chuỗi tĩnh theo ngôn ngữ hiện tại (không đổi nếu không có trong từ điển).</summary>
    public static string Static(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (IsEn) return Map.TryGetValue(s, out var en) ? en : TranslateMultiline(s, true);
        return Rev.TryGetValue(s, out var vi) ? vi : TranslateMultiline(s, false);
    }

    /// <summary>Như Static nhưng giữ nguyên khoảng trắng đầu/cuối (dùng cho Run nằm giữa câu).</summary>
    private static string StaticKeepWs(string s)
    {
        var direct = Static(s);
        if (!ReferenceEquals(direct, s) && direct != s) return direct;
        var core = s.Trim();
        if (core.Length == 0) return s;
        var t = Static(core);
        if (t == core) return s;
        int lead = s.Length - s.TrimStart().Length;
        int trail = s.Length - s.TrimEnd().Length;
        return s[..lead] + t + s[^trail..];
    }

    // Đoạn "THÔNG TIN" gồm nhiều dòng: dịch từng dòng.
    private static string TranslateMultiline(string s, bool toEn)
    {
        if (!s.Contains('\n')) return s;
        var parts = s.Split('\n');
        bool changed = false;
        for (int i = 0; i < parts.Length; i++)
        {
            var d = toEn ? Map : Rev;
            if (d.TryGetValue(parts[i], out var t)) { parts[i] = t; changed = true; }
        }
        return changed ? string.Join('\n', parts) : s;
    }

    /// <summary>Duyệt cây giao diện và dịch Text / Content / ToolTip / Header.</summary>
    public static void Apply(DependencyObject root)
    {
        switch (root)
        {
            case TextBlock tb when !string.IsNullOrEmpty(tb.Text) && tb.Inlines.Count <= 1:
                tb.Text = Static(tb.Text); break;
            case ContentControl cc when cc.Content is string cs:
                cc.Content = Static(cs); break;
            case System.Windows.Documents.Run run when !string.IsNullOrEmpty(run.Text):
                run.Text = StaticKeepWs(run.Text); break;
            case DataGrid dg:
                foreach (var col in dg.Columns)
                    if (col.Header is string h) col.Header = Static(h);
                break;
        }
        if (root is HeaderedContentControl hcc && hcc.Header is string hdr)
            hcc.Header = Static(hdr);
        if (root is FrameworkElement fe && fe.ToolTip is string tip)
            fe.ToolTip = Static(tip);

        // Chụp danh sách con TRƯỚC khi dịch: sửa Run.Text / TextBlock.Text làm đổi Inlines đang được duyệt
        // → "Collection was modified" (RangeContentEnumerator.MoveNext).
        var kids = new List<DependencyObject>();
        try { foreach (var child in LogicalTreeHelper.GetChildren(root)) if (child is DependencyObject d) kids.Add(d); }
        catch (InvalidOperationException) { }
        foreach (var d in kids)
        {
            try { Apply(d); } catch (InvalidOperationException) { }
        }
    }
}
