using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using TimeTracker.Core.Abstractions;
using TimeTracker.Core.Models;

namespace TimeTracker.Tracker.Win32;

internal sealed class Win32ActiveWindowProvider : IActiveWindowProvider
{
    private const uint ProcessQueryInformation = 0x0400;
    private const uint ProcessVmRead = 0x0010;

    private readonly ILogger<Win32ActiveWindowProvider> _logger;

    public Win32ActiveWindowProvider(ILogger<Win32ActiveWindowProvider> logger)
    {
        _logger = logger;
    }

    public ActiveWindowInfo? GetActiveWindow()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            var appName = ResolveProcessName(hwnd);
            var windowTitle = GetWindowTitle(hwnd);
            return new ActiveWindowInfo(appName, windowTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao capturar janela.");
            return null;
        }
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var builder = new StringBuilder(512);
        _ = NativeMethods.GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ResolveProcessName(IntPtr hwnd)
    {
        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
        {
            return "System/Protected";
        }

        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = NativeMethods.OpenProcess(ProcessQueryInformation | ProcessVmRead, false, processId);
            if (handle == IntPtr.Zero)
            {
                return "System/Protected";
            }

            var builder = new StringBuilder(1024);
            if (NativeMethods.GetModuleFileNameEx(handle, IntPtr.Zero, builder, builder.Capacity) == 0)
            {
                return "System/Protected";
            }

            return Path.GetFileName(builder.ToString());
        }
        catch
        {
            return "System/Protected";
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(handle);
            }
        }
    }
}

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr handle);

    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetModuleFileNameEx(
        IntPtr hProcess,
        IntPtr hModule,
        StringBuilder lpFilename,
        int nSize);
}
