namespace NewsAgent.Dashboard;

/// <summary>
/// Tracks the status of digest runs for the /status endpoint.
/// Thread-safe singleton updated by DigestWorker after each cycle.
/// </summary>
public sealed class StatusTracker
{
    private readonly Lock _lock = new();
    private DateTimeOffset? _lastRunTime;
    private DateTimeOffset? _nextRunTime;
    private int _lastArticleCount;
    private int _totalDigestsGenerated;
    private string? _lastError;

    /// <summary>Records a successful digest run.</summary>
    public void RecordSuccess(int articleCount)
    {
        lock (_lock)
        {
            _lastRunTime = DateTimeOffset.UtcNow;
            _lastArticleCount = articleCount;
            _totalDigestsGenerated++;
            _lastError = null;
        }
    }

    /// <summary>Records a failed digest run.</summary>
    public void RecordError(string error)
    {
        lock (_lock)
        {
            _lastRunTime = DateTimeOffset.UtcNow;
            _lastError = error;
        }
    }

    /// <summary>Updates the next scheduled run time.</summary>
    public void SetNextRun(DateTimeOffset nextRun)
    {
        lock (_lock)
        {
            _nextRunTime = nextRun;
        }
    }

    /// <summary>Returns a snapshot of the current status.</summary>
    public StatusSnapshot GetSnapshot(string outputDirectory)
    {
        lock (_lock)
        {
            var digestCount = Directory.Exists(outputDirectory)
                ? Directory.GetFiles(outputDirectory, "digest-*.html").Length
                : 0;

            return new StatusSnapshot(
                Health: _lastError is null ? "ok" : "error",
                LastRunTime: _lastRunTime,
                NextRunTime: _nextRunTime,
                DigestsGenerated: digestCount,
                LastArticleCount: _lastArticleCount,
                LastError: _lastError);
        }
    }
}

/// <summary>Status snapshot returned by the /status endpoint.</summary>
public record StatusSnapshot(
    string Health,
    DateTimeOffset? LastRunTime,
    DateTimeOffset? NextRunTime,
    int DigestsGenerated,
    int LastArticleCount,
    string? LastError);
