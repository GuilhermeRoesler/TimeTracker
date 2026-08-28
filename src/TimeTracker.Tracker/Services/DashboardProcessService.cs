using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TimeTracker.Core;

namespace TimeTracker.Tracker.Services;

internal sealed class DashboardProcessService : IDisposable
{
    private readonly ILogger<DashboardProcessService> _logger;
    private Process? _process;

    public DashboardProcessService(ILogger<DashboardProcessService> logger)
    {
        _logger = logger;
    }

    public void Start(string appDir)
    {
        if (_process is not null && !_process.HasExited)
        {
            return;
        }

        try
        {
            var dashboardProject = Path.Combine(
                appDir,
                "src",
                "TimeTracker.Dashboard",
                "TimeTracker.Dashboard.csproj");

            ProcessStartInfo startInfo;
            if (File.Exists(dashboardProject))
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{dashboardProject}\" --no-launch-profile",
                    WorkingDirectory = appDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }
            else
            {
                var dashboardExe = Path.Combine(appDir, "TimeTracker.Dashboard.exe");
                if (!File.Exists(dashboardExe))
                {
                    _logger.LogWarning("Dashboard não encontrado em {Path}.", dashboardExe);
                    return;
                }

                startInfo = new ProcessStartInfo
                {
                    FileName = dashboardExe,
                    WorkingDirectory = appDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }

            _process = Process.Start(startInfo);
            _logger.LogInformation("Dashboard iniciado em {Url}.", AppConstants.DashboardUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar dashboard.");
        }
    }

    public void Stop()
    {
        if (_process is null || _process.HasExited)
        {
            return;
        }

        try
        {
            _process.CloseMainWindow();
            if (!_process.WaitForExit(2000))
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao encerrar dashboard.");
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignorar falha final de encerramento.
            }
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    public void Dispose() => Stop();
}
