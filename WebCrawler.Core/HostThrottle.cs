using System.Diagnostics;

namespace WebCrawler;

/// <summary>
/// Spaces out the requests to each web site: whatever the number of workers, two requests to the same host start at
/// least <c>delay</c> apart. Waiting takes no thread, and callers are served in the order they asked.
/// </summary>
internal sealed class HostThrottle
{
    private readonly object _sync = new();
    private readonly Dictionary<string, long> _nextFree = new(StringComparer.OrdinalIgnoreCase); // Stopwatch timestamps

    /// <summary>Returns when it is this caller's turn to send a request to <paramref name="host"/>.</summary>
    public async Task WaitAsync(string host, TimeSpan delay, CancellationToken token)
    {
        if (delay <= TimeSpan.Zero)
            return;

        TimeSpan wait;
        lock (_sync)
        {
            long now = Stopwatch.GetTimestamp();
            long turn = _nextFree.TryGetValue(host, out long free) ? Math.Max(now, free) : now;
            _nextFree[host] = turn + (long)(delay.TotalSeconds * Stopwatch.Frequency);
            wait = Stopwatch.GetElapsedTime(now, turn);
        }

        if (wait > TimeSpan.Zero)
            await Task.Delay(wait, token).ConfigureAwait(false);
    }
}
