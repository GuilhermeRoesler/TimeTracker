using Microsoft.Web.WebView2.Core;
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
        // UDF padrão fica ao lado do .exe (Program Files) e falha sem escrita —
        // usar a pasta de dados do app (%LocalAppData% em produção).
        var userDataFolder = Path.Combine(AppPaths.GetDataDir(), "WebView2");
        Directory.CreateDirectory(userDataFolder);
        var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await _webView.EnsureCoreWebView2Async(environment);

        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        // shell=app: o HTML mostra a opção discreta "abrir no navegador" só nesta janela.
        _webView.Source = new Uri($"{AppConstants.DashboardUrl}/?shell=app");
    }

    private static void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string message;
        try
        {
            message = e.TryGetWebMessageAsString();
        }
        catch (ArgumentException)
        {
            message = e.WebMessageAsJson;
        }

        if (string.Equals(message, "openInBrowser", StringComparison.Ordinal)
            || message.Contains("openInBrowser", StringComparison.Ordinal))
        {
            DashboardWindowService.OpenInBrowser();
        }
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

        DashboardWindow? window = null;
        try
        {
            window = new DashboardWindow();
            window.FormClosed += (_, _) =>
            {
                if (ReferenceEquals(_window, window))
                {
                    _window = null;
                }
            };

            _window = window;
            window.Show();
            window.Activate();
            await window.InitializeAsync();
        }
        catch (Exception)
        {
            if (window is { IsDisposed: false })
            {
                window.Close();
            }

            _window = null;
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

    public static void OpenInBrowser()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = AppConstants.DashboardUrl,
            UseShellExecute = true,
        });
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
}
