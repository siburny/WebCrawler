using System.Drawing;
using System.Globalization;

namespace WebCrawler;

/// <summary>
/// One checked address. Workers fill it in while the UI thread reads it, so every field that is not atomic to read
/// (structs, lists) is guarded by a lock, and every change is reported to the owning <see cref="URLCollection"/>,
/// which tells the grid to repaint the row on the UI thread.
/// </summary>
public class URL
{
    // Guards the non-atomic fields below. Never held while calling into the owning collection.
    private readonly object _lock = new();

    // Set once by the owning URLCollection when the URL is added to it (see URLCollection.Add).
    internal URLCollection? Owner;
    internal int Index;
    internal bool DirtyQueued; // guarded by the owner's lock

    private volatile URLStatus _status;

    public URLStatus Status
    {
        get => _status;
        set
        {
            // The collection keeps its counters and work queue in step with every status change.
            URLCollection? owner = Owner;
            if (owner != null)
                owner.ChangeStatus(this, value);
            else
                _status = value;
        }
    }

    // Only called by URLCollection, under its lock.
    internal void SetStatusCore(URLStatus value) => _status = value;

    public string Url { get; }

    public int Depth { get; }

    private string _mimeType = "";

    public string MimeType
    {
        get => _mimeType;
        set
        {
            _mimeType = value;
            Changed();
        }
    }

    private string _notes = "";

    public string Notes
    {
        get => _notes;
        set
        {
            _notes = value;
            Changed();
        }
    }

    private long? _contentLength;

    /// <summary>Size of the response body in bytes, or null when unknown (no Content-Length header and no body read).</summary>
    public long? ContentLength
    {
        get { lock (_lock) { return _contentLength; } }
        set
        {
            lock (_lock) { _contentLength = value; }
            Changed();
        }
    }

    private readonly List<double> _timeTaken = new();

    /// <summary>Average of all measurements so far; assigning a value records one more measurement.</summary>
    public double TimeTaken
    {
        get
        {
            lock (_lock) { return _timeTaken.Count == 0 ? 0.0 : _timeTaken.Average(); }
        }
        set
        {
            lock (_lock) { _timeTaken.Add(value); }
            Changed();
        }
    }

    /// <summary>Number of measurements recorded so far.</summary>
    public int TimeTakenAttempts
    {
        get
        {
            lock (_lock) { return _timeTaken.Count; }
        }
    }

    public string TimeTakenAll(string separator = "\t")
    {
        lock (_lock)
        {
            return string.Join(separator, _timeTaken.Select(x => x.ToString(CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>
    /// One text value per measurement (invariant culture, so the decimal separator never depends on the
    /// machine's regional settings), padded with empty strings up to <paramref name="padTo"/> entries.
    /// </summary>
    public string[] TimeTakenValues(int padTo)
    {
        List<string> values;
        lock (_lock)
        {
            values = _timeTaken.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();
        }
        while (values.Count < padTo)
            values.Add("");
        return values.ToArray();
    }

    private Color _highlightColor;

    public Color HighlightColor
    {
        get { lock (_lock) { return _highlightColor; } }
        set
        {
            lock (_lock) { _highlightColor = value; }
            Changed();
        }
    }

    private int? _statusCode;

    /// <summary>HTTP status code of the response, or null when no HTTP response was received.</summary>
    public int? StatusCode
    {
        get { lock (_lock) { return _statusCode; } }
        set
        {
            lock (_lock) { _statusCode = value; }
            Changed();
        }
    }

    private readonly List<string> _linksFrom = new();
    private readonly List<string> _linksTo = new();
    private readonly HashSet<string> _linksFromSet = new();
    private readonly HashSet<string> _linksToSet = new();

    /// <summary>Records a page that links to this URL (duplicates and empty values are ignored).</summary>
    public void AddLinkFrom(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return;
        lock (_lock)
        {
            if (_linksFromSet.Add(url))
                _linksFrom.Add(url);
        }
    }

    /// <summary>Records a URL that this page links to (duplicates and empty values are ignored).</summary>
    public void AddLinkTo(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return;
        lock (_lock)
        {
            if (_linksToSet.Add(url))
                _linksTo.Add(url);
        }
    }

    public string[] GetLinksFrom()
    {
        lock (_lock)
        {
            return _linksFrom.ToArray();
        }
    }

    public string[] GetLinksTo()
    {
        lock (_lock)
        {
            return _linksTo.ToArray();
        }
    }

    public URL(string url, int depth = 0, URLStatus status = URLStatus.Default)
    {
        _status = status;
        Url = url;
        Depth = depth;
    }

    // Tells the owning collection that this row changed and needs repainting (no-op until it has an owner).
    private void Changed() => Owner?.MarkChanged(this);
}

public enum URLStatus
{
    Default = 0,
    Downloading = 1,
    Parsing = 2,
    Done = 10,
    Redirected = 11,
    Warning = 20,
    Error = 30,
    External = 40,
    Skipped = 100
}
