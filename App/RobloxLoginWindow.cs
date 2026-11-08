// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace RobloxRAMManager;

/// <summary>
/// Cửa sổ đăng nhập Roblox (WebView2) giống Roblox Account Manager: tự điền user/mật khẩu,
/// người dùng chỉ giải captcha/2FA nếu có, rồi app tự lấy cookie .ROBLOSECURITY và đóng cửa sổ.
/// Mỗi lần dùng một thư mục profile tạm riêng nên các acc không dính phiên của nhau.
/// </summary>
public sealed class RobloxLoginWindow : Window
{
    private readonly WebView2 _web = new();
    private readonly string _user, _pass;
    private readonly string _profileDir = Path.Combine(Path.GetTempPath(), "RobloxMgr_wv_" + Guid.NewGuid().ToString("N"));
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TextBlock _hint = new() { Margin = new Thickness(12, 8, 12, 8), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private bool _filled, _polling;

    public string? Cookie { get; private set; }

    public RobloxLoginWindow(string user, string pass)
    {
        _user = user; _pass = pass;
        Title = Loc.Tr("Đăng nhập Roblox: ", "Roblox sign-in: ") + user;
        Width = 520; Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.ApplyThemeWindow();
        _hint.Fg("TextBrush");
        _hint.Text = Loc.Tr("Đang tự điền tài khoản... Nếu có captcha/2FA, hãy hoàn tất 1 lần, cửa sổ sẽ tự đóng.",
                            "Filling in your account... If a captcha/2FA appears, finish it once; this window closes itself.");

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_hint, 0); Grid.SetRow(_web, 1);
        grid.Children.Add(_hint); grid.Children.Add(_web);
        Content = grid;

        Loaded += async (_, _) => await InitAsync();
        _timer.Tick += async (_, _) => await PollCookieAsync();
        Closed += (_, _) =>
        {
            _timer.Stop();
            try { _web.Dispose(); } catch { }
            try { if (Directory.Exists(_profileDir)) Directory.Delete(_profileDir, true); } catch { }
        };
    }

    private async Task InitAsync()
    {
        try
        {
            var nativeDir = RuntimeLoader.ExtractNative();
            if (nativeDir != null) CoreWebView2Environment.SetLoaderDllFolderPath(nativeDir);
            var env = await CoreWebView2Environment.CreateAsync(null, _profileDir);
            await _web.EnsureCoreWebView2Async(env);
            _web.CoreWebView2.CookieManager.DeleteAllCookies();
            _web.CoreWebView2.NavigationCompleted += async (_, _) => await TryFillAsync();
            _web.CoreWebView2.Navigate("https://www.roblox.com/login");
            _timer.Start();
        }
        catch (Exception ex)
        {
            Dlg.Show(Loc.Tr("Không mở được trình duyệt nhúng (cần WebView2 Runtime):\n", "Cannot start the embedded browser (WebView2 Runtime required):\n") + ex.Message,
                            Title, MessageBoxButton.OK, MessageBoxImage.Warning, this);
            DialogResult = false;
        }
    }

    private async Task TryFillAsync()
    {
        if (_filled || _web.CoreWebView2 == null) return;
        if (!(_web.Source?.AbsolutePath ?? "").Contains("login", StringComparison.OrdinalIgnoreCase)) return;
        _filled = true;
        string js = @"(async()=>{
  const w=ms=>new Promise(r=>setTimeout(r,ms));
  const set=(el,v)=>{Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,v);
    el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));};
  for(let i=0;i<60;i++){
    const u=document.querySelector('#login-username'),p=document.querySelector('#login-password'),b=document.querySelector('#login-button');
    if(u&&p&&b){set(u," + JsonSerializer.Serialize(_user) + @");set(p," + JsonSerializer.Serialize(_pass) + @");await w(500);b.click();return;}
    await w(500);
  }
})();";
        try { await _web.CoreWebView2.ExecuteScriptAsync(js); } catch { _filled = false; }
    }

    private async Task PollCookieAsync()
    {
        if (_polling || _web.CoreWebView2 == null) return;
        _polling = true;
        try
        {
            var cookies = await _web.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
            var c = cookies.FirstOrDefault(x => x.Name == ".ROBLOSECURITY" && x.Value.Length > 50);
            if (c != null)
            {
                Cookie = c.Value;
                _timer.Stop();
                DialogResult = true;
            }
        }
        catch { }
        finally { _polling = false; }
    }
}
