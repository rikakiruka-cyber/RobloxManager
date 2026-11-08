// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.Windows;

namespace RobloxRAMManager;

public partial class App : Application
{
    // Giữ mutex của Roblox để mở được nhiều acc cùng lúc (phải giữ suốt thời gian app chạy)
    private static Mutex? _robloxMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Tránh app tự thoát khi hộp thoại chọn thư mục đóng lại trước khi MainWindow được tạo
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

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

        AppConfig.Language = Loc.Lang;
        AppConfig.Load();
        Loc.Lang = AppConfig.Language;
        AppConfig.Save();
        ThemeManager.Apply(AppConfig.Theme);

        DispatcherUnhandledException += (s, args) =>
        {
            var ex = args.Exception;
            string msg = $"Type: {ex.GetType().Name}\nMessage: {ex.Message}\n\n";
            var inner = ex.InnerException;
            while (inner != null)
            {
                msg += $"--- INNER ---\nType: {inner.GetType().Name}\nMessage: {inner.Message}\nStack: {inner.StackTrace}\n\n";
                inner = inner.InnerException;
            }
            msg += $"--- OUTER STACK ---\n{ex.StackTrace}";

            try { System.IO.File.WriteAllText(DataStore.FilePath("crash.log"), msg); } catch { }
            MessageBox.Show(msg, "Roblox Manager - Crash Detail", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }
}
