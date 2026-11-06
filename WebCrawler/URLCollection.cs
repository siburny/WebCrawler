using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace WebCrawler
{
    /// <summary>Totals shown in the status bar; all values come from one consistent moment.</summary>
    public struct URLCounts
    {
        public int Total;
        public int Processed;
        public int Redirected;
        public int Warnings;
        public int Errors;
    }

    /// <summary>
    /// The set of URLs of one scan. Worker threads add URLs and ask for the next one to check; the UI thread
    /// shows them. Everything shared lives behind one lock, and every operation the workers need is O(1):
    /// a dictionary answers "is this URL known?", a queue holds the URLs still waiting to be checked, and
    /// running counters answer "how many are done?" (so neither the workers nor the status bar ever scan the list).
    ///
    /// The grid is bound to <see cref="Collection"/>, a separate list that only the UI thread touches.
    /// <see cref="SyncView"/> (called from a UI timer) copies new URLs and repaints changed ones into it in
    /// small time-boxed batches, so worker threads never raise UI events and the UI is never flooded with them.
    /// </summary>
    public class URLCollection
    {
        // How long one SyncView call may spend per phase, and how many rows it handles per lock acquisition.
        private const int SyncBudgetMs = 25;
        private const int SyncChunk = 100;

        private readonly object _sync = new object();
        private readonly List<URL> _all = new List<URL>();                       // every URL, in the order it was added
        private readonly Dictionary<string, URL> _index = new Dictionary<string, URL>(StringComparer.Ordinal);
        private readonly Queue<URL> _pending = new Queue<URL>();                 // URLs with status Default, oldest first
        private readonly Queue<URL> _dirty = new Queue<URL>();                   // shown rows whose values changed
        private int _unfinished;   // status < Done
        private int _processed;    // status >= Done
        private int _redirected;
        private int _warnings;
        private int _errors;
        private int _viewed;       // how many entries of _all have been handed to the view list
        private bool _detached;

        private readonly BindingList<URL> _view = new BindingList<URL>();

        public URLCollection()
        {
            _view.AllowNew = false;
            _view.AllowRemove = false;
        }

        /// <summary>True when the text is an absolute http:// or https:// URL, i.e. something that can be requested.</summary>
        public static bool TryParseCrawlUri(string url, out Uri uri)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public URL Add(string url)
        {
            return Add(url, 0, "");
        }

        public URL Add(string url, int depth, string parent)
        {
            return Add(url, depth, parent, URLStatus.Default);
        }

        /// <summary>
        /// Adds a URL unless it is already known. Blank values are ignored (returns null). A URL that is
        /// meant to be requested (status Default) but is not a valid absolute http/https URL is added as an
        /// Error entry instead, so it is shown to the user and never handed to a worker thread.
        /// The URL string itself is stored exactly as given. Returns null once the collection was detached.
        /// </summary>
        public URL Add(string url, int depth, string parent, URLStatus status)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            string note = null;
            if (status == URLStatus.Default)
            {
                Uri parsed;
                if (!TryParseCrawlUri(url, out parsed))
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

                URL temp;
                if (_index.TryGetValue(url, out temp))
                {
                    // Already known: still remember this additional referrer.
                    temp.AddLinkFrom(parent);
                    return temp;
                }

                temp = new URL(url, depth, status);
                if (note != null)
                    temp.Notes = note;
                temp.AddLinkFrom(parent);
                temp.Owner = this;
                temp.Index = _all.Count;

                _all.Add(temp);
                _index.Add(url, temp);
                Account(status, +1);
                if (status == URLStatus.Default)
                {
                    _pending.Enqueue(temp);
                    Monitor.Pulse(_sync);
                }
                return temp;
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
                URLCounts counts = new URLCounts();
                counts.Total = _all.Count;
                counts.Processed = _processed;
                counts.Redirected = _redirected;
                counts.Warnings = _warnings;
                counts.Errors = _errors;
                return counts;
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
        public BindingList<URL> Collection
        {
            get
            {
                return _view;
            }
        }

        /// <summary>
        /// The URL (query string removed) that defines the crawl scope: the first entry that is a valid
        /// absolute http/https URL. Returns null when there is none.
        /// </summary>
        public Uri GetStartingUri()
        {
            lock (_sync)
            {
                foreach (URL entry in _all)
                {
                    string candidate = entry.Url.IndexOf("?") > 0 ? entry.Url.Substring(0, entry.Url.IndexOf("?")) : entry.Url;
                    Uri parsed;
                    if (TryParseCrawlUri(candidate, out parsed))
                        return parsed;
                }
                return null;
            }
        }

        /// <summary>
        /// Takes the next URL that is waiting to be checked and marks it as Downloading. When nothing is waiting
        /// it blocks for up to <paramref name="waitMs"/> milliseconds (it wakes up as soon as work arrives, or on
        /// <see cref="WakeWaiters"/>) and returns null if there is still nothing.
        /// </summary>
        public URL GetNext(int waitMs)
        {
            lock (_sync)
            {
                URL next = DequeueLocked();
                if (next != null || waitMs <= 0 || _detached)
                    return next;

                Monitor.Wait(_sync, waitMs);
                return DequeueLocked();
            }
        }

        private URL DequeueLocked()
        {
            if (_detached)
                return null;

            while (_pending.Count > 0)
            {
                URL next = _pending.Dequeue();
                // Entries can have moved on since they were queued; only hand out ones that still wait.
                if (next.Status == URLStatus.Default)
                {
                    SetStatusLocked(next, URLStatus.Default, URLStatus.Downloading);
                    return next;
                }
            }
            return null;
        }

        /// <summary>True when every URL has reached a finished status (nothing is waiting or being processed).</summary>
        public bool IsAllDone()
        {
            lock (_sync)
            {
                return _unfinished == 0;
            }
        }

        /// <summary>Wakes every thread blocked in <see cref="GetNext"/> so it can notice that it was asked to stop.</summary>
        public void WakeWaiters()
        {
            lock (_sync)
            {
                Monitor.PulseAll(_sync);
            }
        }

        /// <summary>
        /// Called when this collection is replaced by a new one: worker threads still holding it get no more work,
        /// and anything they try to add is dropped.
        /// </summary>
        public void Detach()
        {
            lock (_sync)
            {
                _detached = true;
                Monitor.PulseAll(_sync);
            }
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
                {
                    _pending.Enqueue(url);
                    Monitor.Pulse(_sync);
                }
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
            watch.Reset();
            watch.Start();
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
}
