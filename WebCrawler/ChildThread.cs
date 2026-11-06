using CsQuery;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web;

namespace WebCrawler
{
    /// <summary>
    /// The settings one crawl runs with, copied when the crawl starts. Worker threads only ever read this copy,
    /// so changing the Settings dialog while a crawl is running can never change (or corrupt) what they see.
    /// </summary>
    sealed class CrawlOptions
    {
        public readonly Uri StartingUri;
        public readonly int MaxDepth;
        public readonly int MaxMeasurements;
        public readonly bool QuickScan;
        public readonly int MaxConnections;
        public readonly string[] ExcludeRules;
        public readonly SettingsHighlightRule[] HighlightRules;

        public CrawlOptions(Uri startingUri)
        {
            Settings settings = Settings.Instance;
            StartingUri = startingUri;
            MaxDepth = settings.MaxDepth;
            MaxMeasurements = settings.MaxMeasurements;
            QuickScan = settings.QuickScan;
            MaxConnections = Math.Max(1, settings.MaxProcesses);
            ExcludeRules = (settings.ExcludeRules ?? new List<string>()).Where(rule => !string.IsNullOrEmpty(rule)).ToArray();
            HighlightRules = (settings.HighlightRules ?? new List<SettingsHighlightRule>()).ToArray();
        }
    }

    class ChildThread
    {
        // Upper bound for page/error-body text copied into the Notes column.
        private const int MaxNotesBodyChars = 4000;

        // Text bodies larger than this are only read up to this size (a link checker has no use for the rest).
        private const int MaxBodyBytes = 10 * 1024 * 1024;

        private readonly CrawlOptions options;
        private readonly Thread childThread;
        private readonly URLCollection collection;
        private readonly int index;
        private readonly ManualResetEvent stopSignal = new ManualResetEvent(false);
        private volatile bool stopping = false;
        private int currentDepth;

        public bool isRunning
        {
            get
            {
                return childThread != null && childThread.ThreadState == ThreadState.Running;
            }
        }

        public ChildThread(int _index, URLCollection _collection, CrawlOptions _options)
        {
            collection = _collection;
            index = _index;
            options = _options;
            if (options.StartingUri == null)
                throw new InvalidOperationException("There is no valid http/https URL to start crawling from.");
            this.childThread = new Thread(new ThreadStart(ChildThreadProcess));
            this.childThread.Name = "Child Process #" + (index + 1).ToString();
            // A request that is still in flight must never keep the process alive after the window was closed.
            this.childThread.IsBackground = true;
        }

        public void Start()
        {
            this.childThread.Start();
        }

        public void Stop()
        {
            this.stopping = true;
            // Interrupts the pauses between requests and the wait for new work, so a stop takes effect at once.
            this.stopSignal.Set();
            this.collection.WakeWaiters();
        }

        // Sleeps, but wakes up immediately when the thread is asked to stop.
        private void Pause(int milliseconds)
        {
            stopSignal.WaitOne(milliseconds);
        }

        public void ChildThreadProcess()
        {
            string buffer;

            while (!this.stopping)
            {
                // Reset vars
                buffer = "";
                currentDepth = -1;

                // Blocks until there is something to check (or the crawl is stopped); no polling.
                URL url = collection.GetNext(1000);
                if (url == null)
                    continue;

                if (this.stopping)
                {
                    // Asked to stop while waking up with a URL in hand: give it back instead of leaving it half-done.
                    url.Status = URLStatus.Default;
                    break;
                }

                currentDepth = url.Depth;

                try
                {
                    // Everything that can throw on bad input lives inside this try, so one bad URL
                    // can never take down the worker thread (and with it the whole process).
                    Uri urlCheck;
                    if (!URLCollection.TryParseCrawlUri(url.Url, out urlCheck))
                    {
                        url.Status = URLStatus.Error;
                        url.Notes = "Invalid URL: only absolute http:// or https:// URLs can be checked.";
                        continue;
                    }

                    // Exclude rules must be honoured *before* anything is requested.
                    if (Filter.IsExcluded(url.Url, options.ExcludeRules))
                    {
                        url.Status = URLStatus.Skipped;
                        url.Notes = "Excluded by rule";
                        continue;
                    }

                    System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

                    // Quick scan sends HEAD requests: status, type and size only. The body is never read, so
                    // links are not followed and highlight rules are not evaluated in this mode.
                    // Many servers reject HEAD even though GET works, so those answers fall back to a normal GET.
                    HttpWebResponse response = null;
                    if (options.QuickScan)
                    {
                        try
                        {
                            response = SendRequest(urlCheck, "HEAD", options.MaxConnections);
                        }
                        catch (WebException headError)
                        {
                            if (!HeadRejected(headError))
                                throw;
                            if (headError.Response != null)
                                headError.Response.Close();
                        }
                    }
                    if (response == null)
                        response = SendRequest(urlCheck, "GET", options.MaxConnections);

                    using (response)
                    {
                        url.StatusCode = (int)response.StatusCode;
                        string mime = (response.ContentType ?? "").Split(new char[] { ';' })[0].Trim();
                        url.MimeType = mime;
                        // -1 means the server sent no Content-Length (typical for chunked responses).
                        long contentLength = response.ContentLength;

                        bool redirected = response.ResponseUri.ToString() != urlCheck.ToString();

                        // Only a page that is going to be searched or parsed is downloaded. Images, PDFs, archives
                        // and the like are judged by their response headers alone (status, type, size), and so are
                        // redirects (the target gets its own row) and quick scans. Closing the response without
                        // reading it abandons the rest of the transfer.
                        bool readBody = !options.QuickScan && !redirected && Filter.IsTextual(mime);
                        if (readBody)
                        {
                            long bytesRead;
                            bool truncated;
                            buffer = ReadBody(response, out bytesRead, out truncated);
                            // Without the header, the size of what was actually received is the answer
                            // (unless we stopped reading early, then it is unknown).
                            if (contentLength < 0 && !truncated)
                                contentLength = bytesRead;
                        }
                        // Still unknown: leave it blank instead of showing -1.
                        url.ContentLength = contentLength >= 0 ? (long?)contentLength : null;

                        url.TimeTaken = timer.Elapsed.TotalMilliseconds;
                        if (readBody)
                        {
                            foreach (SettingsHighlightRule rule in options.HighlightRules)
                                if (rule.IsMatch(buffer))
                                {
                                    url.HighlightColor = rule.GetHighlightColor();
                                    url.Notes = "Highlight rule matched";
                                }
                        }

                        if (redirected)
                        {
                            string redirectTarget = response.ResponseUri.ToString();

                            // Register the redirect target *before* this URL is marked Redirected (a "finished"
                            // status). Otherwise IsAllDone() can see every URL finished in between and end the crawl early.
                            if (Filter.IsExcluded(redirectTarget, options.ExcludeRules))
                                collection.Add(redirectTarget, currentDepth, url.Url, URLStatus.Skipped);
                            else if (options.StartingUri.IsBaseOf(response.ResponseUri))
                                collection.Add(redirectTarget, currentDepth, url.Url);
                            else
                                collection.Add(redirectTarget, currentDepth, url.Url, URLStatus.External);

                            url.Notes = "Redirected to " + redirectTarget;
                            // The status code and size above belong to the final page, not to this URL;
                            // that page gets its own row, so leave them blank here.
                            url.StatusCode = null;
                            url.ContentLength = null;
                            url.Status = URLStatus.Redirected;
                        }
                        else if (readBody && Filter.IsHtml(mime) && currentDepth < options.MaxDepth)
                        {
                            url.Status = URLStatus.Parsing;

                            CQ dom = CQ.Create(buffer);

                            // Relative links are resolved against the page they appear on (or its <base href>),
                            // not against the URL the crawl started from.
                            Uri baseUri = response.ResponseUri;
                            string baseHref = dom["base[href]"].Attr("href");
                            if (!string.IsNullOrWhiteSpace(baseHref))
                            {
                                Uri customBase;
                                if (Uri.TryCreate(baseUri, HttpUtility.HtmlDecode(baseHref.Trim()), out customBase)
                                    && (customBase.Scheme == Uri.UriSchemeHttp || customBase.Scheme == Uri.UriSchemeHttps))
                                    baseUri = customBase;
                            }

                            List<IDomObject> links = dom["a"].ToList();
                            foreach (IDomObject link in links)
                            {
                                if (link.Attributes["href"] == null)
                                    continue;

                                string tempurl = HttpUtility.HtmlDecode(link.Attributes["href"].ToString());
                                if (string.IsNullOrWhiteSpace(tempurl) || !Filter.IsValid(tempurl))
                                    continue;

                                Uri temp;
                                if (!Uri.TryCreate(tempurl, UriKind.RelativeOrAbsolute, out temp))
                                {
                                    // Malformed href: record it and carry on with the rest of the page.
                                    collection.Add(tempurl, currentDepth + 1, url.Url, URLStatus.Error);
                                    url.AddLinkTo(tempurl);
                                    continue;
                                }

                                string linkUrl;
                                Uri resolved;
                                if (temp.IsAbsoluteUri)
                                {
                                    linkUrl = tempurl;
                                    resolved = temp;
                                }
                                else if (Uri.TryCreate(baseUri, temp, out resolved))
                                {
                                    linkUrl = resolved.AbsoluteUri;
                                }
                                else
                                {
                                    collection.Add(tempurl, currentDepth + 1, url.Url, URLStatus.Error);
                                    url.AddLinkTo(tempurl);
                                    continue;
                                }

                                if (Filter.IsExcluded(linkUrl, options.ExcludeRules))
                                    collection.Add(linkUrl, currentDepth + 1, url.Url, URLStatus.Skipped);
                                else if (options.StartingUri.IsBaseOf(resolved))
                                    collection.Add(linkUrl, currentDepth + 1, url.Url);
                                else
                                    collection.Add(linkUrl, currentDepth + 1, url.Url, URLStatus.External);

                                url.AddLinkTo(linkUrl);
                            }
                        }
                    }
                }
                catch (WebException e)
                {
                    url.Status = URLStatus.Error;
                    url.ContentLength = null;

                    // HTTP errors (404, 500, ...) arrive as exceptions that still carry a response.
                    // It must be disposed or its connection stays tied up.
                    string errorBody = "";
                    using (HttpWebResponse errorResponse = e.Response as HttpWebResponse)
                    {
                        if (errorResponse != null)
                        {
                            url.StatusCode = (int)errorResponse.StatusCode;
                            errorBody = ReadErrorBody(errorResponse);
                        }
                    }

                    url.Notes = e.Message + Environment.NewLine + "-----------------------------------------------------------" + errorBody;

                    Pause(250);
                    continue;
                }
                catch (Exception e)
                {
                    url.Status = URLStatus.Error;
                    url.ContentLength = null;
                    url.Notes = e.Message + Environment.NewLine + "-----------------------------------------------------------" + Truncate(buffer);

                    Pause(250);
                    continue;
                }

                URLStatus status = url.Status;
                if (!(status == URLStatus.Error || status == URLStatus.External || status == URLStatus.Redirected || status == URLStatus.Skipped || status == URLStatus.Warning))
                {
                    if (url.TimeTakenAttempts >= options.MaxMeasurements)
                        url.Status = URLStatus.Done;
                    else
                        url.Status = URLStatus.Default;
                }
                Pause(250);
            }
        }

        private static HttpWebResponse SendRequest(Uri uri, string method, int connectionLimit)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            // .NET Framework allows only 2 simultaneous connections per host by default, which would quietly
            // reduce any number of worker threads above 2 to 2 (and the waiting would count against the timeout).
            if (request.ServicePoint.ConnectionLimit < connectionLimit)
                request.ServicePoint.ConnectionLimit = connectionLimit;
            request.Method = method;
            request.AllowAutoRedirect = true;
            request.Timeout = 60000;
            return (HttpWebResponse)request.GetResponse();
        }

        /// <summary>True when a HEAD request was answered in a way that usually means "HEAD not supported here".</summary>
        private static bool HeadRejected(WebException e)
        {
            HttpWebResponse response = e.Response as HttpWebResponse;
            if (response == null)
                return false;

            switch (response.StatusCode)
            {
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Forbidden:
                case HttpStatusCode.NotFound:
                case HttpStatusCode.MethodNotAllowed:
                case HttpStatusCode.NotImplemented:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Reads the response body as text, at most <see cref="MaxBodyBytes"/> bytes of it.
        /// <paramref name="bytesRead"/> is the number of bytes received; <paramref name="truncated"/> tells whether the
        /// body was cut off at the limit.
        /// </summary>
        private static string ReadBody(WebResponse response, out long bytesRead, out bool truncated)
        {
            truncated = false;
            using (MemoryStream body = new MemoryStream())
            {
                using (Stream stream = response.GetResponseStream())
                {
                    byte[] chunk = new byte[16 * 1024];
                    int read;
                    while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                    {
                        body.Write(chunk, 0, read);
                        if (body.Length >= MaxBodyBytes)
                        {
                            truncated = true;
                            break;
                        }
                    }
                }

                bytesRead = body.Length;
                body.Position = 0;
                using (StreamReader reader = new StreamReader(body))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static string Truncate(string text)
        {
            if (text == null || text.Length <= MaxNotesBodyChars)
                return text;
            return text.Substring(0, MaxNotesBodyChars) + "...";
        }

        /// <summary>Reads (a bounded amount of) the body of an HTTP error response; never throws.</summary>
        private static string ReadErrorBody(WebResponse response)
        {
            try
            {
                using (Stream stream = response.GetResponseStream())
                {
                    if (stream == null)
                        return "";
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        char[] chunk = new char[MaxNotesBodyChars];
                        int read = reader.Read(chunk, 0, chunk.Length);
                        return read > 0 ? new string(chunk, 0, read) : "";
                    }
                }
            }
            catch (Exception)
            {
                return "";
            }
        }
    }

    static class Filter
    {
        public static bool IsExcluded(string url, string[] rules)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                if (url.Contains(rules[i]))
                    return true;
            }
            return false;
        }

        /// <summary>Pages that are searched for links to follow.</summary>
        public static bool IsHtml(string mime)
        {
            return mime.Equals("text/html", StringComparison.OrdinalIgnoreCase)
                || mime.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Responses whose body is text, i.e. worth downloading to evaluate highlight rules (and, for HTML, links).</summary>
        public static bool IsTextual(string mime)
        {
            if (mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                return true;

            switch (mime.ToLowerInvariant())
            {
                case "application/xhtml+xml":
                case "application/xml":
                case "application/json":
                case "application/javascript":
                case "application/x-javascript":
                case "application/ecmascript":
                    return true;
            }

            return mime.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)
                || mime.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsValid(string url)
        {
            if (url.StartsWith("#"))
                return false;
            else if (url.ToLower().StartsWith("javascript:") || url.ToLower().StartsWith("mailto:"))
                return false;

            /*Uri uri;
                  if(!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri))
                      return false;*/

            return true;
        }
    }
}
