using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTracker.Core;
using TimeTracker.Tracker.Services;

namespace TimeTracker.Tracker;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly IHost _host;
    private readonly CancellationTokenSource _shutdownCts = new();

    public TrayApplicationContext(IHost host)
    {
        _host = host;

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "Time Tracker",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        _notifyIcon.DoubleClick += (_, _) => OpenDashboard();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _shutdownCts.Cancel();
            _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _host.Dispose();
            _shutdownCts.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir Dashboard", null, (_, _) => OpenDashboard());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitThread());
        return menu;
    }

    private static void OpenDashboard()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = AppConstants.DashboardUrl,
            UseShellExecute = true,
        });
    }

    private static Icon CreateTrayIcon()
    {
        const int size = 64;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.FromArgb(0, 128, 255));
        graphics.FillRectangle(Brushes.White, size / 2, 0, size / 2, size / 2);
        graphics.FillRectangle(Brushes.White, 0, size / 2, size / 2, size / 2);

        return Icon.FromHandle(bitmap.GetHicon());
    }
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        AppPaths.SetAppDir(ResolveAppDirectory());
        var appDir = AppPaths.GetAppDir();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddTrackerServices())
            .Build();

        host.Start();

        var startupService = new StartupShortcutService(
            host.Services.GetRequiredService<ILogger<StartupShortcutService>>());
        startupService.EnsureStartupShortcut(appDir, Environment.ProcessPath ?? Application.ExecutablePath);

        Application.Run(new TrayApplicationContext(host));
    }

    private static string ResolveAppDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TimeTracker.sln")) ||
                File.Exists(Path.Combine(directory.FullName, "main.py")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
