// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace RobloxRAMManager;

public record ThemeDef(string Id, string Label,
    string Bg, string Surface, string Surface2, string Border,
    string Primary, string Secondary, string Tertiary,
    string Warning, string Danger,
    string Text, string TextDim,
    string Glow1, string Glow2);

public static class ThemeManager
{
    public static string Current { get; private set; } = "aurora";

    public static readonly ThemeDef[] All =
    [
        new("aurora", "🌊 Aurora",
            "#05130E", "#0B241C", "#071B15", "#144233",
            "#34D399", "#00D4FF", "#A78BFA",
            "#FBBF24", "#F87171",
            "#D0F0E0", "#6CA090",
            "#10B981", "#06B6D4"),

        new("cosmos", "🌌 Cosmos",
            "#08081A", "#141432", "#0D0D24", "#262052",
            "#8B5CF6", "#C084FC", "#06B6D4",
            "#F59E0B", "#EF4444",
            "#E0E0F0", "#7C7CA0",
            "#8B5CF6", "#3B82F6"),

        new("hell", "🔥 Hell",
            "#160505", "#2A0E0E", "#1F0A0A", "#481616",
            "#EF4444", "#F97316", "#FBBF24",
            "#FFD700", "#FF0040",
            "#F0D0D0", "#A07060",
            "#EF4444", "#EA580C"),

        new("love", "💗 Love",
            "#150512", "#2B0D23", "#20091A", "#4A1838",
            "#EC4899", "#F472B6", "#A78BFA",
            "#FBBF24", "#EF4444",
            "#F0D0E0", "#A07090",
            "#EC4899", "#A855F7"),

        new("dark", "🌑 Dark",
            "#0A0B0E", "#161820", "#101217", "#252834",
            "#60A5FA", "#34D399", "#A78BFA",
            "#FBBF24", "#EF4444",
            "#E0E0E8", "#6C7293",
            "#2563EB", "#0D9488"),

        new("white", "☀️ White",
            "#F1F5F9", "#FFFFFF", "#F8FAFC", "#CBD5E1",
            "#2563EB", "#059669", "#7C3AED",
            "#D97706", "#DC2626",
            "#0F172A", "#64748B",
            "#93C5FD", "#A7F3D0"),
    ];

    public static void Apply(string id)
    {
        var t = Array.Find(All, x => x.Id == id) ?? All[0];
        var r = Application.Current.Resources;
        bool light = t.Id == "white";

        var cBg = C(t.Bg);
        var cSurf = C(t.Surface);
        var cSurf2 = C(t.Surface2);
        var cPri = C(t.Primary);
        var cSec = C(t.Secondary);
        var cTer = C(t.Tertiary);
        var cG1 = C(t.Glow1);
        var cG2 = C(t.Glow2);

        r["BgBrush"] = B(t.Bg);
        r["SurfaceBrush"] = B(t.Surface);
        r["Surface2Brush"] = B(t.Surface2);
        r["PanelBorderBrush"] = B(t.Border);
        r["AccentPrimaryBrush"] = B(t.Primary);
        r["AccentSecondaryBrush"] = B(t.Secondary);
        r["AccentTertiaryBrush"] = B(t.Tertiary);
        r["AccentWarningBrush"] = B(t.Warning);
        r["AccentDangerBrush"] = B(t.Danger);
        r["TextBrush"] = B(t.Text);
        r["TextDimBrush"] = B(t.TextDim);

        r["GradientH"] = new LinearGradientBrush(cPri, cSec, 0);
        r["GradientV"] = new LinearGradientBrush(cPri, cSec, 90);
        r["RamBarGradient"] = new LinearGradientBrush(cPri, cSec, 0);
        r["CpuBarGradient"] = new LinearGradientBrush(cTer, cPri, 0);
        r["BarTrackBrush"] = new SolidColorBrush(light ? Color.FromArgb(0x30, 0x0F, 0x17, 0x2A) : Color.FromArgb(0x70, 0x00, 0x00, 0x00));

        // ── Glass (kính mờ) ──
        r["GlassBrush"] = new SolidColorBrush(WithA(cSurf, light ? (byte)0xCC : (byte)0x8C));
        r["GlassDeepBrush"] = new SolidColorBrush(WithA(cSurf2, light ? (byte)0xB8 : (byte)0xA6));
        r["GlassActiveBrush"] = new SolidColorBrush(WithA(cPri, light ? (byte)0x30 : (byte)0x38));
        r["PopupBrush"] = new SolidColorBrush(WithA(cSurf2, 0xF4));
        r["HoverBrush"] = new SolidColorBrush(light ? Color.FromArgb(0x14, 0x0F, 0x17, 0x2A) : Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF));
        r["RowAltBrush"] = new SolidColorBrush(light ? Color.FromArgb(0x0A, 0x0F, 0x17, 0x2A) : Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
        r["SwitchOffBrush"] = new SolidColorBrush(light ? Color.FromArgb(0x40, 0x0F, 0x17, 0x2A) : Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF));

        var edge = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        if (light)
        {
            edge.GradientStops.Add(new GradientStop(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF), 0));
            edge.GradientStops.Add(new GradientStop(WithA(C(t.Border), 0xCC), 1));
        }
        else
        {
            edge.GradientStops.Add(new GradientStop(Color.FromArgb(0x4A, 0xFF, 0xFF, 0xFF), 0));
            edge.GradientStops.Add(new GradientStop(WithA(cPri, 0x22), 0.55));
            edge.GradientStops.Add(new GradientStop(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF), 1));
        }
        r["GlassEdgeBrush"] = edge;

        // ── Aurora blobs / ribbons ──
        byte k = light ? (byte)0x55 : (byte)0xFF;
        r["AuroraBlobA"] = Radial(cG1, ScaleA(0xB0, k));
        r["AuroraBlobB"] = Radial(cG2, ScaleA(0x98, k));
        r["AuroraBlobC"] = Radial(cTer, ScaleA(0x60, k));
        r["AuroraRibbonA"] = Radial(cPri, ScaleA(0x70, k));
        r["AuroraRibbonB"] = Radial(cSec, ScaleA(0x58, k));

        // Làm tối viền để chữ dễ đọc
        var vig = new RadialGradientBrush { Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.85, RadiusY = 0.85 };
        vig.GradientStops.Add(new GradientStop(Color.FromArgb(0, cBg.R, cBg.G, cBg.B), 0.45));
        vig.GradientStops.Add(new GradientStop(Color.FromArgb(light ? (byte)0x30 : (byte)0xA0, cBg.R, cBg.G, cBg.B), 1));
        r["VignetteBrush"] = vig;

        Current = t.Id;
    }

    private static byte ScaleA(int a, byte k) => (byte)(a * k / 255);

    private static RadialGradientBrush Radial(Color c, byte a)
    {
        var b = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(a, c.R, c.G, c.B), 0.0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(a * 0.45), c.R, c.G, c.B), 0.5));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1.0));
        return b;
    }

    private static Color WithA(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    private static SolidColorBrush B(string hex) => new(C(hex));
    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}

public static class AppConfig
{
    public static string Theme { get; set; } = "aurora";
    public static int CpuCapPercent { get; set; } = 40;
    public static int RamCapMB { get; set; } = 0; // 0 = tắt
    public static int TrimIntervalSec { get; set; } = 10;
    public static int PollIntervalMs { get; set; } = 2000;
    public static bool CrashRecovery { get; set; } = true;
    public static bool AuroraAnimated { get; set; } = true;
    public static string Language { get; set; } = "vi";

    // Discord Webhook
    public static string DiscordWebhookUrl { get; set; } = "";
    public static int DiscordWebhookIntervalMin { get; set; } = 0; // 0 = manual only
    public static bool DiscordAlertOnCrash { get; set; } = true;
    public static string DiscordUserId { get; set; } = "";      // ID Discord để ping khi có sự cố quan trọng
    public static bool AutoUpdateCheck { get; set; } = true;     // tự kiểm tra cập nhật khi mở app
    public static bool IgnoreManualClose { get; set; } = true;   // tự tay đóng game thì không mở lại

    private static string Path_ => DataStore.FilePath("config.json");

    public static void Load()
    {
        try
        {
            if (!File.Exists(Path_)) return;
            var j = JsonDocument.Parse(File.ReadAllText(Path_)).RootElement;
            if (j.TryGetProperty("theme", out var v)) Theme = v.GetString() ?? "aurora";
            if (j.TryGetProperty("cpuCap", out v)) CpuCapPercent = v.GetInt32();
            if (j.TryGetProperty("ramCapMb", out v)) RamCapMB = v.GetInt32();
            if (j.TryGetProperty("trimSec", out v)) TrimIntervalSec = v.GetInt32();
            if (j.TryGetProperty("pollMs", out v)) PollIntervalMs = v.GetInt32();
            if (j.TryGetProperty("crash", out v)) CrashRecovery = v.GetBoolean();
            if (j.TryGetProperty("aurora", out v)) AuroraAnimated = v.GetBoolean();
            if (j.TryGetProperty("lang", out v)) Language = v.GetString() == "en" ? "en" : "vi";
            if (j.TryGetProperty("discordUrl", out v)) DiscordWebhookUrl = v.GetString() ?? "";
            if (j.TryGetProperty("discordInterval", out v)) DiscordWebhookIntervalMin = v.GetInt32();
            if (j.TryGetProperty("discordCrashAlert", out v)) DiscordAlertOnCrash = v.GetBoolean();
            if (j.TryGetProperty("discordUserId", out v)) DiscordUserId = v.GetString() ?? "";
            if (j.TryGetProperty("ignoreManualClose", out v)) IgnoreManualClose = v.GetBoolean();
            if (j.TryGetProperty("autoUpdate", out v)) AutoUpdateCheck = v.GetBoolean();
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(Path_, JsonSerializer.Serialize(new
            {
                theme = Theme, cpuCap = CpuCapPercent,
                ramCapMb = RamCapMB, trimSec = TrimIntervalSec, pollMs = PollIntervalMs,
                crash = CrashRecovery,
                aurora = AuroraAnimated,
                lang = Language,
                discordUrl = DiscordWebhookUrl,
                discordInterval = DiscordWebhookIntervalMin,
                discordCrashAlert = DiscordAlertOnCrash,
                discordUserId = DiscordUserId,
                ignoreManualClose = IgnoreManualClose,
                autoUpdate = AutoUpdateCheck
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
