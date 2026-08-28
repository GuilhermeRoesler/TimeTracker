using TimeTracker.Core.Models;

namespace TimeTracker.Core.Abstractions;

public interface IActiveWindowProvider
{
    ActiveWindowInfo? GetActiveWindow();
}
