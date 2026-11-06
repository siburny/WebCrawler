using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace WebCrawler;

/// <summary>Totals shown in the status bar; all values come from one consistent moment.</summary>
public readonly record struct URLCounts(int Total, int Processed, int Redirected, int Warnings, int Errors);

/// <summary>
/// The set of URLs of one scan. Workers add URLs and ask for the next one to check; the UI thread shows them.
/// Everything shared lives behind one lock, and every operation the workers need is O(1): a dictionary answers
/// "is this URL known?", a channel holds the URLs still waiting to be checked (workers wait on it asynchronously,
/// so an idle worker costs no thread), and running counters answer "how many are done?" (so neither the workers
/// nor the status bar ever scan the list).
///
/// The grid is bound to <see cref="Collection"/>, a separate list that only the UI thread touches.
/// <see cref="SyncView"/> (called from a UI timer) copies new URLs and repaints changed ones into it in
/// small time-boxed batches, so workers never raise UI events and the UI is never flooded with them.
/// </summary>
public class URLCollection
{
    // How long one SyncView call may spend per phase, and how many rows it handles per lock acquisition.
    private const int SyncBudgetMs = 25;
    private const int SyncChunk = 100;

    private readonly object _sync = new();
    private readonly List<URL> _all = new();                                          // every URL, in the order it was added
    private readonly Dictionary<string, URL> _index = new(StringComparer.Ordinal);
    private readonly Channel<URL> _pending = Channel.CreateUnbounded<URL>();          // URLs with status Default, oldest first
    private readonly Queue<URL> _dirty = new();                                       // shown rows whose values changed
    private int _unfinished;   // status < Done
    private int _processed;    // status >= Done
    private int _redirected;
    private int _warnings;
    private int _errors;
    private int _viewed;       // how many entries of _all have been handed to the view list
    private bool _detached;

    private readonly BindingList<URL> _view = new() { AllowNew = false, AllowRemove = false };

    /// <summary>True when the text is an absolute http:// or https:// URL, i.e. something that can be requested.</summary>
    public static bool TryParseCrawlUri(string? url, [NotNullWhen(true)] out Uri? uri)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public URL? Add(string? url) => Add(url, 0, "");

    public URL? Add(string? url, int depth, string? parent) => Add(url, depth, parent, URLStatus.Default);

    /// <summary>
    /// Adds a URL unless it is already known. Blank values are ignored (returns null). A URL that is
    /// meant to be requested (status Default) but is not a valid absolute http/https URL is added as an
    /// Error entry instead, so it is shown to the user and never handed to a worker.
    /// The URL string itself is stored exactly as given. Returns null once the collection was detached.
    /// </summary>
    public URL? Add(string? url, int depth, string? parent, URLStatus status)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        string? note = null;
        if (status == URLStatus.Default)
        {
            if (!TryParseCrawlUri(url, out _))
            {
                status = URLStatus.Error;
                note = "Invalid URL: only absolute http:// or https:// URLs can be checked.";
            }
        }
        else if (status == URLStatus.Skipped)
        {
            note = "Excluded by rule";
        }

        lock (_sync)
        {
            if (_detached)
                return null;

            if (_index.TryGetValue(url, out URL? existing))
            {
                // Already known: still remember this additional referrer.
                existing.AddLinkFrom(parent);
                return existing;
            }

            URL added = new(url, depth, status);
            if (note != null)
                added.Notes = note;
            added.AddLinkFrom(parent);
            added.Owner = this;
            added.Index = _all.Count;

            _all.Add(added);
            _index.Add(url, added);
            Account(status, +1);
            if (status == URLStatus.Default)
                _pending.Writer.TryWrite(added);
            return added;
        }
    }

    /// <summary>Number of URLs known so far (including ones the grid has not been given yet).</summary>
    public int Count()
    {
        lock (_sync)
        {
            return _all.Count;
        }
    }

    public URLCounts GetCounts()
    {
        lock (_sync)
        {
            return new URLCounts(_all.Count, _processed, _redirected, _warnings, _errors);
        }
    }

    /// <summary>A copy of all URLs, safe to enumerate on any thread (used for reports).</summary>
    public URL[] Snapshot()
    {
        lock (_sync)
        {
            return _all.ToArray();
        }
    }

    /// <summary>
    /// The list the grid is bound to. It is only ever modified by <see cref="SyncView"/>, i.e. on the UI
    /// thread, so the UI can read it freely. It can trail the real contents by a fraction of a second.
    /// </summary>
    public BindingList<URL> Collection => _view;

    /// <summary>
    /// The URL (query string removed) that defines the crawl scope: the first entry that is a valid
    /// absolute http/https URL. Returns null when there is none.
    /// </summary>
    public Uri? GetStartingUri()
    {
        lock (_sync)
        {
            foreach (URL entry in _all)
            {
                int query = entry.Url.IndexOf('?');
                string candidate = query > 0 ? entry.Url.Substring(0, query) : entry.Url;
                if (TryParseCrawlUri(candidate, out Uri? parsed))
                    return parsed;
            }
            return null;
        }
    }

    /// <summary>
    /// Takes the next URL that is waiting to be checked and marks it as Downloading, or returns null when
    /// nothing is waiting right now.
    /// </summary>
    public URL? TryGetNext()
    {
        while (_pending.Reader.TryRead(out URL? candidate))
        {
            URL? claimed = Claim(candidate);
            if (claimed != null)
                return claimed;
        }
        return null;
    }

    /// <summary>
    /// Takes the next URL that is waiting to be checked and marks it as Downloading. When nothing is waiting it
    /// waits (without holding a thread) until work arrives. Returns null once the collection was
    /// <see cref="Detach">detached</see>; throws <see cref="OperationCanceledException"/> when cancelled.
    /// </summary>
    public async ValueTask<URL?> GetNextAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            URL candidate;
            try
            {
                candidate = await _pending.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                return null;
            }

            URL? claimed = Claim(candidate);
            if (claimed != null)
                return claimed;
        }
    }

    // Entries can have moved on since they were queued; only hand out ones that still wait.
    private URL? Claim(URL candidate)
    {
        lock (_sync)
        {
            if (_detached || candidate.Status != URLStatus.Default)
                return null;

            SetStatusLocked(candidate, URLStatus.Default, URLStatus.Downloading);
            return candidate;
        }
    }

    /// <summary>True when every URL has reached a finished status (nothing is waiting or being processed).</summary>
    public bool IsAllDone()
    {
        lock (_sync)
        {
            return _unfinished == 0;
        }
    }

    /// <summary>
    /// Called when this collection is replaced by a new one: workers still holding it get no more work
    /// (those waiting for some are released), and anything they try to add is dropped.
    /// </summary>
    public void Detach()
    {
        lock (_sync)
        {
            _detached = true;
        }
        _pending.Writer.TryComplete();
    }

    // URL.Status setter. Keeps the counters and the work queue consistent with the new status.
    internal void ChangeStatus(URL url, URLStatus value)
    {
        lock (_sync)
        {
            URLStatus old = url.Status;
            if (old == value)
                return;

            SetStatusLocked(url, old, value);
            if (value == URLStatus.Default)
                _pending.Writer.TryWrite(url);
        }
    }

    // Any other URL property setter: remember that the row needs repainting.
    internal void MarkChanged(URL url)
    {
        lock (_sync)
        {
            MarkDirtyLocked(url);
        }
    }

    private void SetStatusLocked(URL url, URLStatus old, URLStatus value)
    {
        Account(old, -1);
        Account(value, +1);
        url.SetStatusCore(value);
        MarkDirtyLocked(url);
    }

    private void Account(URLStatus status, int delta)
    {
        if (status < URLStatus.Done)
            _unfinished += delta;
        else
            _processed += delta;

        if (status == URLStatus.Redirected)
            _redirected += delta;
        else if (status == URLStatus.Warning)
            _warnings += delta;
        else if (status == URLStatus.Error)
            _errors += delta;
    }

    private void MarkDirtyLocked(URL url)
    {
        // Rows the grid does not have yet need no repaint: they are read fresh when they are added to it.
        if (!url.DirtyQueued && url.Index < _viewed)
        {
            url.DirtyQueued = true;
            _dirty.Enqueue(url);
        }
    }

    /// <summary>
    /// UI thread only. Hands new URLs to the grid's list and repaints rows whose values changed, working for
    /// at most a few milliseconds per call; whatever is left is picked up by the next call.
    /// </summary>
    public void SyncView()
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < SyncBudgetMs)
        {
            URL[] chunk;
            lock (_sync)
            {
                int take = Math.Min(_all.Count - _viewed, SyncChunk);
                if (take <= 0)
                    break;
                chunk = new URL[take];
                _all.CopyTo(_viewed, chunk, 0, take);
                _viewed += take;
            }
            foreach (URL url in chunk)
                _view.Add(url);
        }

        // Updates get their own time slice so a long run of new rows can never starve them.
        watch.Restart();
        while (watch.ElapsedMilliseconds < SyncBudgetMs)
        {
            URL[] chunk;
            lock (_sync)
            {
                int take = Math.Min(_dirty.Count, SyncChunk);
                if (take <= 0)
                    break;
                chunk = new URL[take];
                for (int i = 0; i < take; i++)
                {
                    chunk[i] = _dirty.Dequeue();
                    chunk[i].DirtyQueued = false;
                }
            }
            foreach (URL url in chunk)
            {
                if (url.Index < _view.Count)
                    _view.ResetItem(url.Index);
            }
        }
    }
}
