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
    private readonly AppUpdateService _updateService;
    private readonly SynchronizationContext _uiContext;
    private int _updateCheckRunning;
    private CancellationTokenSource? _startupCheckCts;

    public TrayApplicationContext(AppUpdateService updateService)
    {
        _updateService = updateService;
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIconLoader.Load(),
            Text = $"{AppConstants.AppDisplayName} {AppUpdateService.GetCurrentVersion()}",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        _notifyIcon.DoubleClick += (_, _) => DashboardWindowService.Open();

        if (!AppPaths.IsDevelopmentRun())
        {
            _notifyIcon.BalloonTipClicked += (_, _) => _ = CheckForUpdatesAsync(silentIfUpToDate: false);
            ScheduleStartupUpdateCheck();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _startupCheckCts?.Cancel();
            _startupCheckCts?.Dispose();
            _startupCheckCts = null;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            DashboardWindowService.Close();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir Dashboard", null, (_, _) => DashboardWindowService.Open());
        if (!AppPaths.IsDevelopmentRun())
        {
            menu.Items.Add("Verificar atualizações...", null, (_, _) => _ = CheckForUpdatesAsync(silentIfUpToDate: false));
            menu.Items.Add(new ToolStripSeparator());
        }

        menu.Items.Add("Sair", null, (_, _) => ExitThread());
        return menu;
    }

    private void ScheduleStartupUpdateCheck()
    {
        _startupCheckCts = new CancellationTokenSource();
        var token = _startupCheckCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(12), token);
                await CheckForUpdatesAsync(silentIfUpToDate: true, cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // Encerrando.
            }
        }, token);
    }

    private async Task CheckForUpdatesAsync(bool silentIfUpToDate, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _updateCheckRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var update = await _updateService.CheckForUpdateAsync(cancellationToken);
            if (update is null)
            {
                if (!silentIfUpToDate)
                {
                    RunOnUi(() => MessageBox.Show(
                        $"Você já está na versão mais recente ({AppUpdateService.GetCurrentVersion()}).",
                        AppConstants.AppDisplayName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information));
                }

                return;
            }

            if (silentIfUpToDate)
            {
                RunOnUi(() =>
                {
                    _notifyIcon.BalloonTipTitle = "Atualização disponível";
                    _notifyIcon.BalloonTipText =
                        $"{AppConstants.AppDisplayName} {update.TagName} está disponível. Clique para atualizar.";
                    _notifyIcon.ShowBalloonTip(8000);
                });
                return;
            }

            var accept = false;
            RunOnUi(() =>
            {
                accept = MessageBox.Show(
                    $"Nova versão {update.TagName} disponível (atual: {AppUpdateService.GetCurrentVersion()}).\n\n" +
                    "Deseja baixar e instalar agora? O aplicativo será encerrado durante a instalação.",
                    AppConstants.AppDisplayName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes;
            });

            if (!accept)
            {
                return;
            }

            RunOnUi(() =>
            {
                _notifyIcon.BalloonTipTitle = "Baixando atualização";
                _notifyIcon.BalloonTipText = $"Baixando {update.TagName}…";
                _notifyIcon.ShowBalloonTip(5000);
            });

            var setupPath = await _updateService.DownloadSetupAsync(update, cancellationToken: cancellationToken);

            RunOnUi(() => _updateService.LaunchSetupAndExit(
                setupPath,
                prepareExit: () => DashboardWindowService.Close(),
                exitApplication: ExitThread));
        }
        catch (OperationCanceledException)
        {
            // Ignorar.
        }
        catch (Exception ex)
        {
            if (!silentIfUpToDate)
            {
                RunOnUi(() => MessageBox.Show(
                    $"Não foi possível verificar/atualizar:\n{ex.Message}",
                    AppConstants.AppDisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning));
            }
        }
        finally
        {
            Interlocked.Exchange(ref _updateCheckRunning, 0);
        }
    }

    private void RunOnUi(Action action)
    {
        if (SynchronizationContext.Current == _uiContext)
        {
            action();
            return;
        }

        _uiContext.Send(_ => action(), null);
    }
}

internal static class Program
{
    private static TrayApplicationContext? _trayContext;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Mutex Local (instância única) + Global (Inno Setup elevado detecta AppMutex).
        using var localMutex = TryCreateMutex(@"Local\" + AppConstants.AppMutexName, out var createdNew);
        using var globalMutex = TryCreateMutex(@"Global\" + AppConstants.AppMutexName, out _);
        if (!createdNew)
        {
            MessageBox.Show(
                $"{AppConstants.AppDisplayName} já está em execução.",
                AppConstants.AppDisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ResolveAndConfigurePaths();
        WebView2ProfileCache.InvalidateIfVersionChanged(AppUpdateService.GetCurrentVersion());
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
        builder.Services.AddSingleton<AppUpdateService>();

        var app = builder.Build();
        app.MapDashboard();
        app.Start();

        var startupService = new StartupShortcutService(
            app.Services.GetRequiredService<ILogger<StartupShortcutService>>());
        startupService.EnsureStartupShortcut(installDir, Environment.ProcessPath ?? Application.ExecutablePath);

        SessionEndingEventHandler sessionEnding = (_, e) =>
        {
            _trayContext?.ExitThread();
            e.Cancel = false;
        };
        SystemEvents.SessionEnding += sessionEnding;

        _trayContext = new TrayApplicationContext(
            app.Services.GetRequiredService<AppUpdateService>());

        try
        {
            Application.Run(_trayContext);
        }
        finally
        {
            SystemEvents.SessionEnding -= sessionEnding;
            // Parar Kestrel fora do Dispose da UI — StopAsync no thread da mensagem
            // pode travar o processo e deixar o `dotnet run`/terminal preso.
            ShutdownWebApp(app);
        }

        // Mantém referências vivas até o fim do processo (Inno / segunda instância).
        GC.KeepAlive(localMutex);
        GC.KeepAlive(globalMutex);
    }

    private static Mutex? TryCreateMutex(string name, out bool createdNew)
    {
        try
        {
            return new Mutex(initiallyOwned: true, name, out createdNew);
        }
        catch
        {
            createdNew = true;
            return null;
        }
    }

    private static void ShutdownWebApp(WebApplication webApp)
    {
        try
        {
            webApp.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch
        {
            // Encerrando; ignorar falhas de shutdown.
        }

        try
        {
            webApp.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // Encerrando; ignorar falhas de dispose.
        }
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
