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
    private readonly UpdateAvailabilityState _updateState;
    private readonly SynchronizationContext _uiContext;
    private int _updateCheckRunning;
    private CancellationTokenSource? _startupCheckCts;
    private AppUpdateInfo? _pendingUpdate;

    public TrayApplicationContext(AppUpdateService updateService, UpdateAvailabilityState updateState)
    {
        _updateService = updateService;
        _updateState = updateState;
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIconLoader.Load(),
            Text = $"{AppConstants.AppDisplayName} {AppUpdateService.GetCurrentVersion()}",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        _notifyIcon.DoubleClick += (_, _) => DashboardWindowService.Open();

        var updatesEnabled = !AppPaths.IsDevelopmentRun();
        _updateState.UpdatesEnabled = updatesEnabled;
        _updateState.CurrentVersion = FormatVersion(AppUpdateService.GetCurrentVersion());
        _updateState.ApplyHandler = ApplyPendingUpdateAsync;

        if (updatesEnabled)
        {
            _notifyIcon.BalloonTipClicked += (_, _) => _ = InstallUpdateAsync(confirm: true);
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
            _updateState.ApplyHandler = null;

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
                // Check cedo para o botão da UI aparecer ao abrir o dashboard.
                await Task.Delay(TimeSpan.FromSeconds(3), token);
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
                _pendingUpdate = null;
                _updateState.ClearPending();

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

            RememberPending(update);

            if (silentIfUpToDate)
            {
                RunOnUi(() =>
                {
                    _notifyIcon.BalloonTipTitle = "Atualização disponível";
                    _notifyIcon.BalloonTipText =
                        $"{AppConstants.AppDisplayName} {update.TagName} está disponível. Abra o painel ou clique para atualizar.";
                    _notifyIcon.ShowBalloonTip(8000);
                });
                return;
            }

            await InstallUpdateAsync(confirm: true, cancellationToken);
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

    private async Task<UpdateApplyResult> ApplyPendingUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var started = await InstallUpdateAsync(confirm: false, cancellationToken);
            if (!started)
            {
                return new UpdateApplyResult(
                    Accepted: false,
                    _updateState.Available
                        ? "Não foi possível iniciar a atualização."
                        : "Nenhuma atualização pendente.");
            }

            return new UpdateApplyResult(Accepted: true);
        }
        catch (OperationCanceledException)
        {
            _updateState.Installing = false;
            return new UpdateApplyResult(Accepted: false, "Atualização cancelada.");
        }
        catch (Exception ex)
        {
            _updateState.Installing = false;
            return new UpdateApplyResult(Accepted: false, ex.Message);
        }
    }

    /// <returns>True se o download/Setup foi iniciado.</returns>
    private async Task<bool> InstallUpdateAsync(bool confirm, CancellationToken cancellationToken = default)
    {
        if (_updateState.Installing)
        {
            return true;
        }

        var update = _pendingUpdate ?? await _updateService.CheckForUpdateAsync(cancellationToken);
        if (update is null)
        {
            _pendingUpdate = null;
            _updateState.ClearPending();
            if (confirm)
            {
                RunOnUi(() => MessageBox.Show(
                    $"Você já está na versão mais recente ({AppUpdateService.GetCurrentVersion()}).",
                    AppConstants.AppDisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information));
            }

            return false;
        }

        RememberPending(update);

        if (confirm)
        {
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
                return false;
            }
        }

        _updateState.Installing = true;

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
        return true;
    }

    private void RememberPending(AppUpdateInfo update)
    {
        _pendingUpdate = update;
        _updateState.SetPending(update.TagName, FormatVersion(update.Version));
    }

    private static string FormatVersion(Version version) =>
        $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";

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
            app.Services.GetRequiredService<AppUpdateService>(),
            app.Services.GetRequiredService<UpdateAvailabilityState>());

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

        var appRoot = AppPaths.GetDefaultProductionAppRoot();
        var dataDir = AppPaths.GetDefaultProductionDataDir();
        AppPaths.MigrateLegacyProductionDataIfNeeded(appRoot, dataDir);
        AppPaths.Configure(dataDir, baseDir, appRoot);
    }
}
