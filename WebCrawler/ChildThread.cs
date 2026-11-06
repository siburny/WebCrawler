using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

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

        private const string NotesSeparator = "-----------------------------------------------------------";

        // One client for every worker thread (and every crawl): it owns the connection pool. Cookies and automatic
        // decompression are off, so requests behave like the old HttpWebRequest ones: no session state leaks from
        // one URL to the next and sizes are the on-the-wire sizes.
        private static readonly HttpClient Http = CreateHttpClient();

        private readonly CrawlOptions options;
        private readonly Thread childThread;
        private readonly URLCollection collection;
        private readonly int index;
        private readonly ManualResetEvent stopSignal = new ManualResetEvent(false);
        // Cancelled by Stop(), so a request that is still in flight ends at once instead of running to completion.
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
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
            this.cancel.Cancel();
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
                    HttpResponseMessage response = null;
                    if (options.QuickScan)
                    {
                        response = SendRequest(urlCheck, HttpMethod.Head);
                        if (HeadRejected(response))
                        {
                            response.Dispose();
                            response = null;
                        }
                    }
                    if (response == null)
                        response = SendRequest(urlCheck, HttpMethod.Get);

                    using (response)
                    {
                        // HttpClient does not throw for 4xx/5xx answers, so they are reported here. Like a failed request
                        // they get no timing and no MIME type, only the status code and the start of the error page.
                        if ((int)response.StatusCode >= 400)
                        {
                            url.Status = URLStatus.Error;
                            url.ContentLength = null;
                            url.StatusCode = (int)response.StatusCode;
                            string reason = string.IsNullOrEmpty(response.ReasonPhrase) ? response.StatusCode.ToString() : response.ReasonPhrase;
                            url.Notes = "The remote server returned an error: (" + (int)response.StatusCode + ") " + reason + "."
                                + Environment.NewLine + NotesSeparator + ReadErrorBody(response);

                            Pause(250);
                            continue;
                        }

                        url.StatusCode = (int)response.StatusCode;
                        string mime = (RawContentType(response) ?? "").Split(new char[] { ';' })[0].Trim();
                        url.MimeType = mime;
                        // -1 means the server sent no Content-Length (typical for chunked responses).
                        long contentLength = response.Content.Headers.ContentLength ?? -1;

                        // Where the request ended up (HttpClient follows redirects itself).
                        Uri finalUri = ResolveFinalUri(response);
                        bool redirected = finalUri.ToString() != urlCheck.ToString();

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
                            string redirectTarget = finalUri.ToString();

                            // Register the redirect target *before* this URL is marked Redirected (a "finished"
                            // status). Otherwise IsAllDone() can see every URL finished in between and end the crawl early.
                            if (Filter.IsExcluded(redirectTarget, options.ExcludeRules))
                                collection.Add(redirectTarget, currentDepth, url.Url, URLStatus.Skipped);
                            else if (options.StartingUri.IsBaseOf(finalUri))
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

                            // AngleSharp decodes character references in attribute values while parsing,
                            // so the hrefs below must not be HTML-decoded a second time.
                            var dom = new HtmlParser().ParseDocument(buffer);

                            // Relative links are resolved against the page they appear on (or its <base href>),
                            // not against the URL the crawl started from.
                            Uri baseUri = finalUri;
                            string baseHref = dom.QuerySelector("base[href]")?.GetAttribute("href");
                            if (!string.IsNullOrWhiteSpace(baseHref))
                            {
                                Uri customBase;
                                if (Uri.TryCreate(baseUri, baseHref.Trim(), out customBase)
                                    && (customBase.Scheme == Uri.UriSchemeHttp || customBase.Scheme == Uri.UriSchemeHttps))
                                    baseUri = customBase;
                            }

                            foreach (var link in dom.QuerySelectorAll("a[href]"))
                            {
                                string tempurl = link.GetAttribute("href");
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
                catch (OperationCanceledException) when (stopping)
                {
                    // Stopped while a request was in flight: give the URL back instead of reporting a bogus error.
                    url.Status = URLStatus.Default;
                    break;
                }
                catch (Exception e)
                {
                    url.Status = URLStatus.Error;
                    url.ContentLength = null;
                    url.Notes = DescribeError(e) + Environment.NewLine + NotesSeparator + Truncate(buffer);

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

        private static HttpClient CreateHttpClient()
        {
            SocketsHttpHandler handler = new SocketsHttpHandler();
            handler.AllowAutoRedirect = true;
            handler.UseCookies = false;
            handler.AutomaticDecompression = DecompressionMethods.None;
            // A long crawl of one host should still notice DNS changes.
            handler.PooledConnectionLifetime = TimeSpan.FromMinutes(2);

            HttpClient client = new HttpClient(handler);
            // Covers the wait for the response headers (the body is read afterwards, see ReadBody).
            client.Timeout = TimeSpan.FromSeconds(60);
            return client;
        }

        private HttpResponseMessage SendRequest(Uri uri, HttpMethod method)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, uri);
            return Http.Send(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
        }

        /// <summary>
        /// The address the response really came from. HttpClient follows redirects itself, but it refuses some
        /// (https to http, for one) and hands back the 3xx answer instead; its Location header still says where it points.
        /// </summary>
        private static Uri ResolveFinalUri(HttpResponseMessage response)
        {
            Uri final = response.RequestMessage.RequestUri;
            int code = (int)response.StatusCode;
            if (code >= 300 && code < 400 && response.Headers.Location != null)
            {
                Uri target;
                if (Uri.TryCreate(final, response.Headers.Location, out target)
                    && (target.Scheme == Uri.UriSchemeHttp || target.Scheme == Uri.UriSchemeHttps))
                    return target;
            }
            return final;
        }

        /// <summary>The Content-Type header exactly as the server sent it, or null when there is none.</summary>
        private static string RawContentType(HttpResponseMessage response)
        {
            System.Net.Http.Headers.HeaderStringValues values;
            return response.Content.Headers.NonValidated.TryGetValues("Content-Type", out values) ? values.ToString() : null;
        }

        /// <summary>True when a HEAD request was answered in a way that usually means "HEAD not supported here".</summary>
        private static bool HeadRejected(HttpResponseMessage response)
        {
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

        /// <summary>The text encoding named by the response's charset, or UTF-8 when it names none (or one we do not know).</summary>
        private static Encoding GetEncoding(HttpContent content)
        {
            try
            {
                string charset = content.Headers.ContentType?.CharSet;
                if (!string.IsNullOrWhiteSpace(charset))
                    return Encoding.GetEncoding(charset.Trim().Trim('"'));
            }
            catch (ArgumentException)
            {
            }
            catch (FormatException)
            {
            }
            return Encoding.UTF8;
        }

        /// <summary>
        /// Reads the response body as text, at most <see cref="MaxBodyBytes"/> bytes of it.
        /// <paramref name="bytesRead"/> is the number of bytes received; <paramref name="truncated"/> tells whether the
        /// body was cut off at the limit.
        /// </summary>
        private string ReadBody(HttpResponseMessage response, out long bytesRead, out bool truncated)
        {
            truncated = false;
            using (MemoryStream body = new MemoryStream())
            {
                using (Stream stream = response.Content.ReadAsStream(cancel.Token))
                {
                    byte[] chunk = new byte[16 * 1024];
                    int read;
                    while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                    {
                        cancel.Token.ThrowIfCancellationRequested();
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
                // The charset from the response headers decides how the bytes become text (a byte order mark wins over it).
                using (StreamReader reader = new StreamReader(body, GetEncoding(response.Content), true))
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
        private string ReadErrorBody(HttpResponseMessage response)
        {
            try
            {
                using (Stream stream = response.Content.ReadAsStream(cancel.Token))
                using (StreamReader reader = new StreamReader(stream, GetEncoding(response.Content), true))
                {
                    char[] chunk = new char[MaxNotesBodyChars];
                    int read = reader.Read(chunk, 0, chunk.Length);
                    return read > 0 ? new string(chunk, 0, read) : "";
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>The exception's message plus those of the exceptions inside it (HttpClient puts the real cause, e.g. "connection refused", there).</summary>
        private static string DescribeError(Exception e)
        {
            StringBuilder text = new StringBuilder(e.Message);
            for (Exception inner = e.InnerException; inner != null; inner = inner.InnerException)
            {
                if (!text.ToString().Contains(inner.Message))
                    text.Append(" -> ").Append(inner.Message);
            }
            return text.ToString();
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
