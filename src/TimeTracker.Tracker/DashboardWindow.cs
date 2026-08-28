using Microsoft.Web.WebView2.WinForms;
using TimeTracker.Core;

namespace TimeTracker.Tracker;

internal sealed class DashboardWindow : Form
{
    private readonly WebView2 _webView;

    public DashboardWindow()
    {
        Text = AppConstants.AppDisplayName;
        Icon = AppIconLoader.Load();
        Width = 1280;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
        };
        Controls.Add(_webView);
    }

    public async Task InitializeAsync()
    {
        await _webView.EnsureCoreWebView2Async(null);
        _webView.Source = new Uri(AppConstants.DashboardUrl);
    }
}

internal static class DashboardWindowService
{
    private static DashboardWindow? _window;

    public static async void Open()
    {
        if (_window is { IsDisposed: false })
        {
            ActivateExisting();
            return;
        }

        try
        {
            var window = new DashboardWindow();
            window.FormClosed += (_, _) => _window = null;
            await window.InitializeAsync();

            _window = window;
            window.Show();
            window.Activate();
        }
        catch (Exception)
        {
            OpenInBrowser();
        }
    }

    public static void Close()
    {
        if (_window is { IsDisposed: false })
        {
            _window.Close();
            _window = null;
        }
    }

    private static void ActivateExisting()
    {
        if (_window is null || _window.IsDisposed)
        {
            return;
        }

        _window.Show();
        _window.WindowState = FormWindowState.Normal;
        _window.Activate();
        _window.BringToFront();
    }

    private static void OpenInBrowser()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = AppConstants.DashboardUrl,
            UseShellExecute = true,
        });
    }
}
