using System.Diagnostics;

namespace TimeTracker.UpdateHelper;

/// <summary>
/// Aguarda o TimeTracker encerrar (com kill forçado se necessário) e só então inicia o Setup.
/// Evita race condition de arquivos bloqueados em Program Files durante o upgrade.
/// </summary>
internal static class Program
{
    private const string AppProcessName = "TimeTracker";
    private const int DefaultTimeoutSeconds = 30;

    private static int Main(string[] args)
    {
        if (!TryParseArgs(args, out var pid, out var setupPath, out var timeoutSeconds))
        {
            return 1;
        }

        if (string.IsNullOrWhiteSpace(setupPath) || !File.Exists(setupPath))
        {
            return 2;
        }

        WaitForExitOrKill(pid, TimeSpan.FromSeconds(timeoutSeconds));
        KillLeftoverAppProcesses(exceptPid: Environment.ProcessId);
        // Libera handles de arquivo (exe/dll em Program Files) antes do Inno sobrescrever.
        Thread.Sleep(750);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = setupPath,
                UseShellExecute = true,
            });
        }
        catch
        {
            return 3;
        }

        return 0;
    }

    private static bool TryParseArgs(
        string[] args,
        out int pid,
        out string setupPath,
        out int timeoutSeconds)
    {
        pid = 0;
        setupPath = "";
        timeoutSeconds = DefaultTimeoutSeconds;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--pid", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
                && int.TryParse(args[++i], out var parsedPid))
            {
                pid = parsedPid;
                continue;
            }

            if (arg.Equals("--setup", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                setupPath = args[++i].Trim('"');
                continue;
            }

            if (arg.Equals("--timeout", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
                && int.TryParse(args[++i], out var parsedTimeout) && parsedTimeout > 0)
            {
                timeoutSeconds = Math.Clamp(parsedTimeout, 5, 120);
            }
        }

        return pid > 0 && !string.IsNullOrWhiteSpace(setupPath);
    }

    private static void WaitForExitOrKill(int pid, TimeSpan timeout)
    {
        Process? process;
        try
        {
            process = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return;
        }

        try
        {
            if (process.HasExited)
            {
                return;
            }

            if (process.WaitForExit(timeout))
            {
                return;
            }

            try
            {
                process.Kill(entireProcessTree: true);
                _ = process.WaitForExit(5_000);
            }
            catch
            {
                // Processo pode ter sumido entre o timeout e o Kill.
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void KillLeftoverAppProcesses(int exceptPid)
    {
        foreach (var process in Process.GetProcessesByName(AppProcessName))
        {
            try
            {
                if (process.Id == exceptPid || process.HasExited)
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                _ = process.WaitForExit(5_000);
            }
            catch
            {
                // Ignorar processos que já terminaram ou sem permissão.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
