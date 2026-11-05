using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;

namespace WebCrawler
{
    public class URLCollection
    {
        private BindingListInvoked<URL> _collection;
        private AutoResetEvent _event = new AutoResetEvent(true);

        public URLCollection(ISynchronizeInvoke invoke)
        {
            _collection = new BindingListInvoked<URL>(invoke);
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
        /// The URL string itself is stored exactly as given.
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

            _event.WaitOne();

            URL temp = _collection.FirstOrDefault(x => x.Url == url);
            if (temp == null)
            {
                temp = new URL(url, depth, status);
                if (note != null)
                    temp.Notes = note;
                temp.AddLinkFrom(parent);
                _collection.Add(temp);
            }
            else
            {
                // Already known: still remember this additional referrer.
                temp.AddLinkFrom(parent);
            }

            _event.Set();
            return temp;
        }

        public int Count()
        {
            return _collection.Count;
        }

        public int Count(Func<URL, bool> predicate)
        {
            _event.WaitOne();
            int count = _collection.Count(predicate);
            _event.Set();
            return count;
        }

        public BindingListInvoked<URL> Collection
        {
            get
            {
                return _collection;
            }
        }

        /// <summary>
        /// The URL (query string removed) that defines the crawl scope: the first entry that is a valid
        /// absolute http/https URL. Returns null when there is none.
        /// </summary>
        public Uri GetStartingUri()
        {
            _event.WaitOne();
            try
            {
                foreach (URL entry in _collection)
                {
                    string candidate = entry.Url.IndexOf("?") > 0 ? entry.Url.Substring(0, entry.Url.IndexOf("?")) : entry.Url;
                    Uri parsed;
                    if (TryParseCrawlUri(candidate, out parsed))
                        return parsed;
                }
                return null;
            }
            finally
            {
                _event.Set();
            }
        }

        public URL GetNext()
        {
            _event.WaitOne();
            URL temp = _collection.Where(x => x.Status == URLStatus.Default).FirstOrDefault();
            if (temp != null)
                temp.Status = URLStatus.Downloading;
            _event.Set();
            return temp;
        }

        public bool IsAllDone()
        {
            _event.WaitOne();

            foreach (URL url in _collection)
            {
                if (url.Status < URLStatus.Done)
                {
                    _event.Set();
                    return false;
                }
            }

            _event.Set();
            return true;
        }

        public void Clear()
        {
            _event.WaitOne();
            _collection.Clear();
            _event.Set();
        }
    }

    public class BindingListInvoked<T> : BindingList<T>
    {
        private ISynchronizeInvoke _invoke;
        public BindingListInvoked(ISynchronizeInvoke invoke)
        {
            _invoke = invoke;
        }

        delegate void ListChangedDelegate(ListChangedEventArgs e);
        protected override void OnListChanged(ListChangedEventArgs e)
        {

            if ((_invoke != null) && (_invoke.InvokeRequired))
            {
                IAsyncResult ar = _invoke.BeginInvoke(new ListChangedDelegate(base.OnListChanged), new object[] { e });
            }
            else
            {
                base.OnListChanged(e);
            }
        }
    }
}
