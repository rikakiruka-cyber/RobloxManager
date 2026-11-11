// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RobloxRAMManager;

/// <summary>Lấy icon (avatar) của client từ chính file .exe để hiện trên thanh bên / menu chọn client.</summary>
public static class ClientIcons
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attrs, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>Trả về icon của exe, hoặc null nếu không đọc được (khi đó UI dùng icon mặc định).</summary>
    public static ImageSource? Get(ClientInfo? client)
    {
        if (client == null || string.IsNullOrEmpty(client.ExePath)) return null;
        if (Cache.TryGetValue(client.ExePath, out var cached)) return cached;

        ImageSource? img = null;
        try
        {
            var info = new SHFILEINFO();
            var h = SHGetFileInfo(client.ExePath, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
            if (h != IntPtr.Zero && info.hIcon != IntPtr.Zero)
            {
                try
                {
                    var bs = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bs.Freeze();
                    img = bs;
                }
                finally { DestroyIcon(info.hIcon); }
            }
        }
        catch { /* không lấy được icon thì thôi, dùng icon mặc định */ }

        Cache[client.ExePath] = img;
        return img;
    }

    public static void Clear() => Cache.Clear();
}
