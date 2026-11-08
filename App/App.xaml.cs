// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Windows;

namespace RobloxRAMManager;

public partial class App : Application
{
    // Giữ mutex của Roblox để mở được nhiều acc cùng lúc (phải giữ suốt thời gian app chạy)
    private static Mutex? _robloxMutex;
    // Chỉ cho 1 bản Manager chạy: 2 bản cùng theo dõi sẽ cùng mở lại acc khi văng → mở trùng, đá nhau.
    private static Mutex? _singleInstance;
    private DateTime _lastCrashDialog = DateTime.MinValue;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Tránh app tự thoát khi hộp thoại chọn thư mục đóng lại trước khi MainWindow được tạo
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        bool firstInstance = true;
        try { _singleInstance = new Mutex(true, "RobloxRAMManager_SingleInstance", out firstInstance); }
        catch { firstInstance = true; }
        if (!firstInstance)
        {
            MessageBox.Show("Roblox RAM Manager đang chạy rồi (kiểm tra khay hệ thống / Task Manager).\nRoblox RAM Manager is already running.",
                            "Roblox RAM Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try { _robloxMutex = new Mutex(true, "ROBLOX_singletonMutex"); } catch { }

        // Ngôn ngữ mặc định theo hệ thống (sẽ bị ghi đè bởi config nếu đã lưu)
        Loc.Lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? "vi" : "en";

        // Lần đầu: cho người dùng chọn ổ/thư mục lưu config, log, tài khoản
        if (!DataStore.TryLoad())
        {
            var dlg = new DataFolderDialog();
            if (dlg.ShowDialog() != true) { Shutdown(); return; }
            DataStore.Set(dlg.SelectedPath);
            DataStore.MigrateLegacy();
        }

        RuntimeLoader.Init();
        RuntimeLoader.CleanupLeftovers(); // dọn hồ sơ WebView2 tạm còn sót + thư mục runtime/temp kiểu cũ

        AppConfig.Language = Loc.Lang;
        AppConfig.Load();
        Loc.Lang = AppConfig.Language;
        AppConfig.Save();
        ThemeManager.Apply(AppConfig.Theme);

        // Lỗi ở thread nền / task không ai chờ: trước đây biến mất không dấu vết → giờ ghi vào log để truy vết
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            AppLog.Write("[LỖI-NỀN] " + args.Exception.GetBaseException());
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            AppLog.Write("[LỖI-NGHIÊM-TRỌNG] " + args.ExceptionObject);

        DispatcherUnhandledException += (s, args) =>
        {
            var ex = args.Exception;
            AppLog.Write("[LỖI-UI] " + ex);
            string msg = $"Type: {ex.GetType().Name}\nMessage: {ex.Message}\n\n";
            var inner = ex.InnerException;
            while (inner != null)
            {
                msg += $"--- INNER ---\nType: {inner.GetType().Name}\nMessage: {inner.Message}\nStack: {inner.StackTrace}\n\n";
                inner = inner.InnerException;
            }
            msg += $"--- OUTER STACK ---\n{ex.StackTrace}";

            try { System.IO.File.WriteAllText(DataStore.FilePath("crash.log"), msg); } catch { }
            // Lỗi lặp lại mỗi tick của timer sẽ bật hộp thoại liên tục → chỉ hiện tối đa 1 lần / 15 giây (vẫn ghi log đầy đủ)
            if ((DateTime.Now - _lastCrashDialog).TotalSeconds >= 15)
            {
                _lastCrashDialog = DateTime.Now;
                Dlg.Show(msg, "Roblox Manager - Crash Detail", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            args.Handled = true;
        };

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }
}
