// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.IO;
using System.Reflection;

namespace RobloxRAMManager;

/// <summary>
/// Mọi thứ liên quan WebView2 nằm gọn trong MỘT thư mục riêng bên trong thư mục dữ liệu:
///   &lt;dữ liệu&gt;\WebView2\runtime   — Core/Wpf + WebView2Loader.dll (nhúng sẵn trong exe, tự giải nén lần đầu)
///   &lt;dữ liệu&gt;\WebView2\profiles  — hồ sơ trình duyệt tạm của từng lần đăng nhập (tự xoá khi đóng cửa sổ)
/// → cạnh exe không có DLL lẻ nào, trong %TEMP% cũng không còn thư mục rác.
/// Lớp này KHÔNG được tham chiếu kiểu WebView2 để tránh nạp DLL sớm.
/// </summary>
public static class RuntimeLoader
{
    private static bool _init;
    private static readonly object _extractLock = new();

    public static string Root => Path.Combine(DataStore.Dir ?? AppDomain.CurrentDomain.BaseDirectory, "WebView2");
    public static string Dir => Path.Combine(Root, "runtime");
    public static string ProfilesDir => Path.Combine(Root, "profiles");

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
            catch (Exception ex) { AppLog.Write("[WebView2] AssemblyResolve lỗi: " + ex.Message); }
            return null;
        };
    }

    /// <summary>Giải nén WebView2Loader.dll, trả về thư mục chứa nó (null nếu không có).</summary>
    public static string? ExtractNative() => Extract("WebView2Loader.dll") != null ? Dir : null;

    /// <summary>Tạo thư mục hồ sơ tạm mới (mỗi lần đăng nhập một thư mục riêng → các acc không dính phiên của nhau).</summary>
    public static string NewProfileDir()
    {
        var p = Path.Combine(ProfilesDir, "wv_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return p;
    }

    /// <summary>Xoá thư mục hồ sơ tạm. Tiến trình msedgewebview2 có thể còn giữ khoá vài giây sau khi đóng cửa sổ → thử lại ở nền.</summary>
    public static void DeleteProfileDir(string dir)
    {
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 12; i++)
            {
                try
                {
                    if (!Directory.Exists(dir)) return;
                    Directory.Delete(dir, true);
                    return;
                }
                catch { await Task.Delay(1000); }
            }
        });
    }

    /// <summary>
    /// Dọn khi mở app: hồ sơ tạm còn sót (app bị tắt đột ngột), thư mục "runtime" kiểu cũ nằm thẳng trong thư mục dữ liệu,
    /// và các thư mục RobloxMgr_wv_* mà bản cũ để lại trong %TEMP%. Chỉ có 1 bản Manager chạy nên không đụng cửa sổ nào đang mở.
    /// </summary>
    public static void CleanupLeftovers()
    {
        _ = Task.Run(() =>
        {
            try { if (Directory.Exists(ProfilesDir)) foreach (var d in Directory.GetDirectories(ProfilesDir)) TryDelete(d); } catch { }

            try
            {
                var legacy = Path.Combine(DataStore.Dir ?? "", "runtime");
                if (DataStore.Dir != null && Directory.Exists(legacy) && File.Exists(Path.Combine(legacy, "WebView2Loader.dll")))
                    TryDelete(legacy);
            }
            catch { }

            try
            {
                foreach (var d in Directory.GetDirectories(Path.GetTempPath(), "RobloxMgr_wv_*")) TryDelete(d);
            }
            catch { }
        });
    }

    private static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, true); } catch { }
    }

    private static string? Extract(string fileName)
    {
        lock (_extractLock)
        {
            var target = Path.Combine(Dir, fileName);
            try
            {
                var asm = typeof(RuntimeLoader).Assembly;
                using var src = asm.GetManifestResourceStream("rt." + fileName);
                if (src == null) return null;

                Directory.CreateDirectory(Dir);
                if (!File.Exists(target) || new FileInfo(target).Length != src.Length)
                {
                    var tmp = target + ".tmp";
                    using (var fs = File.Create(tmp)) src.CopyTo(fs);
                    File.Move(tmp, target, true);
                }
                return target;
            }
            catch (Exception ex)
            {
                AppLog.Write($"[WebView2] Không giải nén được {fileName}: {ex.Message}");
                // File đã có sẵn (đang được nạp nên không ghi đè được) thì cứ dùng
                return File.Exists(target) ? target : null;
            }
        }
    }
}
