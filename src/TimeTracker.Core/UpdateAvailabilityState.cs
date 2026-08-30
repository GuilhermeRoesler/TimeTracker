namespace TimeTracker.Core;

/// <summary>
/// Estado compartilhado da verificação de atualização (bandeja + API do dashboard).
/// </summary>
public sealed class UpdateAvailabilityState
{
    private readonly object _gate = new();
    private string? _pendingTag;
    private string? _latestVersion;
    private string _currentVersion = "0.0.0";
    private bool _updatesEnabled;
    private bool _installing;

    public bool UpdatesEnabled
    {
        get { lock (_gate) return _updatesEnabled; }
        set { lock (_gate) _updatesEnabled = value; }
    }

    public string CurrentVersion
    {
        get { lock (_gate) return _currentVersion; }
        set { lock (_gate) _currentVersion = value; }
    }

    public bool Available
    {
        get { lock (_gate) return _pendingTag is not null; }
    }

    public string? PendingTag
    {
        get { lock (_gate) return _pendingTag; }
    }

    public string? LatestVersion
    {
        get { lock (_gate) return _latestVersion; }
    }

    public bool Installing
    {
        get { lock (_gate) return _installing; }
        set { lock (_gate) _installing = value; }
    }

    /// <summary>Inicia download/instalação (definido pelo Tracker). Null em modo dashboard isolado / demo.</summary>
    public Func<CancellationToken, Task<UpdateApplyResult>>? ApplyHandler { get; set; }

    public void SetPending(string tagName, string latestVersion)
    {
        lock (_gate)
        {
            _pendingTag = tagName;
            _latestVersion = latestVersion;
        }
    }

    public void ClearPending()
    {
        lock (_gate)
        {
            _pendingTag = null;
            _latestVersion = null;
        }
    }

    public object ToApiResponse()
    {
        lock (_gate)
        {
            return new
            {
                enabled = _updatesEnabled,
                available = _pendingTag is not null,
                installing = _installing,
                currentVersion = _currentVersion,
                latestVersion = _latestVersion,
                tagName = _pendingTag,
            };
        }
    }
}

public sealed record UpdateApplyResult(bool Accepted, string? ErrorMessage = null);
