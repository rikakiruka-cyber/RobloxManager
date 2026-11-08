// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.IO;
using System.Reflection;

namespace RobloxRAMManager;

/// <summary>
/// WebView2 (Core/Wpf + WebView2Loader.dll) được nhúng sẵn trong exe, lần đầu cần dùng sẽ tự giải nén
/// vào thư mục ẩn "runtime" bên trong thư mục dữ liệu (cạnh logs/config) → cạnh exe không có DLL lẻ nào.
/// Lớp này KHÔNG được tham chiếu kiểu WebView2 để tránh nạp DLL sớm.
/// </summary>
public static class RuntimeLoader
{
    private static bool _init;
    public static string Dir => Path.Combine(DataStore.Dir ?? AppDomain.CurrentDomain.BaseDirectory, "runtime");

    public static void Init()
    {
        if (_init) return;
        _init = true;
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            try
            {
                var name = new AssemblyName(args.Name).Name;
                if (name is "Microsoft.Web.WebView2.Core" or "Microsoft.Web.WebView2.Wpf")
                {
                    var path = Extract(name + ".dll");
                    if (path != null) return Assembly.LoadFrom(path);
                }
            }
            catch { }
            return null;
        };
    }

    /// <summary>Giải nén WebView2Loader.dll, trả về thư mục chứa nó (null nếu không có).</summary>
    public static string? ExtractNative() => Extract("WebView2Loader.dll") != null ? Dir : null;

    private static string? Extract(string fileName)
    {
        try
        {
            var asm = typeof(RuntimeLoader).Assembly;
            using var src = asm.GetManifestResourceStream("rt." + fileName);
            if (src == null) return null;

            var di = Directory.CreateDirectory(Dir);
            try { di.Attributes |= FileAttributes.Hidden; } catch { }

            var target = Path.Combine(Dir, fileName);
            if (!File.Exists(target) || new FileInfo(target).Length != src.Length)
            {
                var tmp = target + ".tmp";
                using (var fs = File.Create(tmp)) src.CopyTo(fs);
                File.Move(tmp, target, true);
            }
            return target;
        }
        catch { return null; }
    }
}
