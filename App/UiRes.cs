// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;

namespace RobloxRAMManager;

/// <summary>Gán màu theo resource ĐỘNG: đổi theme thì màu tự đổi theo (khác với FindResource chỉ chụp brush tại thời điểm gọi).</summary>
public static class UiRes
{
    public static void Fg(this Control c, string key) => c.SetResourceReference(Control.ForegroundProperty, key);
    public static void Fg(this TextBlock t, string key) => t.SetResourceReference(TextBlock.ForegroundProperty, key);
    public static void Bg(this Control c, string key) => c.SetResourceReference(Control.BackgroundProperty, key);
    public static void Border(this Control c, string key) => c.SetResourceReference(Control.BorderBrushProperty, key);
    public static void FillRes(this Shapes.Shape s, string key) => s.SetResourceReference(Shapes.Shape.FillProperty, key);

    /// <summary>Trong suốt (bỏ liên kết) hoặc bám resource.</summary>
    public static void BgOrClear(this Control c, bool on, string key)
    {
        if (on) c.SetResourceReference(Control.BackgroundProperty, key);
        else { c.ClearValue(Control.BackgroundProperty); c.Background = Brushes.Transparent; }
    }

    /// <summary>Nền + chữ cho cửa sổ/hộp thoại: luôn theo theme đang chọn.</summary>
    public static void ApplyThemeWindow(this Window w)
    {
        w.SetResourceReference(Control.BackgroundProperty, "BgBrush");
        w.SetResourceReference(Control.ForegroundProperty, "TextBrush");
    }
}
