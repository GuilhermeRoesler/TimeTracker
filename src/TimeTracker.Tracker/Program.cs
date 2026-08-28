using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TimeTracker.Core;
using TimeTracker.Dashboard;
using TimeTracker.Tracker.Services;

namespace TimeTracker.Tracker;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly WebApplication _webApp;

    public TrayApplicationContext(WebApplication webApp)
    {
        _webApp = webApp;

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIconLoader.Load(),
            Text = AppConstants.AppDisplayName,
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        _notifyIcon.DoubleClick += (_, _) => DashboardWindowService.Open();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            DashboardWindowService.Close();
            _webApp.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            (_webApp as IAsyncDisposable)?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir Dashboard", null, (_, _) => DashboardWindowService.Open());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitThread());
        return menu;
    }
}

internal static class Program
{
    private static TrayApplicationContext? _trayContext;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        ResolveAndConfigurePaths();
        var installDir = AppPaths.GetInstallDir();

        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = webRoot,
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();

        builder.WebHost.UseUrls($"http://{AppConstants.DashboardHost}:{AppConstants.DashboardPort}");
        builder.Services.AddTrackerServices();
        builder.Services.AddDashboardServices();

        var app = builder.Build();
        app.MapDashboard();
        app.Start();

        var startupService = new StartupShortcutService(
            app.Services.GetRequiredService<ILogger<StartupShortcutService>>());
        startupService.EnsureStartupShortcut(installDir, Environment.ProcessPath ?? Application.ExecutablePath);

        SystemEvents.SessionEnding += (_, e) =>
        {
            _trayContext?.ExitThread();
            e.Cancel = false;
        };

        _trayContext = new TrayApplicationContext(app);
        Application.Run(_trayContext);
    }

    private static void ResolveAndConfigurePaths()
    {
        var baseDir = Path.GetFullPath(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var solutionRoot = AppPaths.FindSolutionRoot(baseDir);

        if (solutionRoot is not null)
        {
            AppPaths.Configure(solutionRoot, solutionRoot);
            return;
        }

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppDisplayName);
        AppPaths.Configure(dataDir, baseDir);
    }
}
