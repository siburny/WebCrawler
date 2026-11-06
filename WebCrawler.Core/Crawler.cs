using System.Net;
using System.Text;
using AngleSharp.Html.Parser;

namespace WebCrawler;

/// <summary>
/// Checks the URLs of one <see cref="URLCollection"/>: a fixed number of worker tasks each take the next waiting
/// URL, request it, record the result and add every link found on the page. Workers wait for new work without
/// holding a thread, so any number of them can idle cheaply. The crawl ends when <see cref="Stop"/> is called
/// (the caller decides when that is, usually once <see cref="URLCollection.IsAllDone"/> is true).
/// </summary>
public sealed class Crawler : IDisposable
{
    // Upper bound for page/error-body text copied into the Notes column.
    private const int MaxNotesBodyChars = 4000;

    // Text bodies larger than this are only read up to this size (a link checker has no use for the rest).
    private const int MaxBodyBytes = 10 * 1024 * 1024;

    private const string NotesSeparator = "-----------------------------------------------------------";

    // One client for every worker (and every crawl): it owns the connection pool. Cookies and automatic
    // decompression are off, so no session state leaks from one URL to the next and sizes are the on-the-wire sizes.
    // The only cookies sent are the predefined ones of the settings, as a plain Cookie header (see SendAsync).
    private static readonly HttpClient Http = CreateHttpClient();

    private readonly URLCollection _collection;
    private readonly CrawlOptions _options;
    // Cancelled by Stop(), so a request that is still in flight ends at once instead of running to completion.
    private readonly CancellationTokenSource _cancel = new();
    // Keeps requests to one site at least CrawlOptions.RequestDelay apart, however many workers there are.
    private readonly HostThrottle _throttle = new();
    private Task[] _workers = Array.Empty<Task>();
    private int _started;

    public Crawler(URLCollection collection, CrawlOptions options)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(options);
        _collection = collection;
        _options = options;
    }

    /// <summary>Completes when every worker has finished, i.e. some time after <see cref="Stop"/>.</summary>
    public Task Completion => Task.WhenAll(_workers);

    /// <summary>Starts the workers; they begin checking at once. Can only be called once.</summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("The crawl was already started.");

        _workers = new Task[_options.MaxConnections];
        for (int i = 0; i < _workers.Length; i++)
        {
            // Task.Run: the workers must never run on (and block) the caller's UI thread.
            _workers[i] = Task.Run(WorkerAsync);
        }
    }

    /// <summary>Asks the workers to stop. Requests in flight are cancelled and their URLs are put back to be checked again.</summary>
    public void Stop() => _cancel.Cancel();

    public void Dispose()
    {
        Stop();
        // The workers still read the token while they wind down, so it is released only once they are all gone.
        _ = Completion.ContinueWith(_ => _cancel.Dispose(), TaskScheduler.Default);
    }

    private async Task WorkerAsync()
    {
        CancellationToken token = _cancel.Token;
        try
        {
            while (true)
            {
                URL? url = await _collection.GetNextAsync(token).ConfigureAwait(false);
                if (url == null)
                    return; // the collection was replaced: no more work

                // Asked to stop while a URL arrived: give it back instead of leaving it half-done.
                if (token.IsCancellationRequested)
                {
                    url.Status = URLStatus.Default;
                    return;
                }

                await CheckAsync(url, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal end of a crawl.
        }
    }

    /// <summary>
    /// Checks one URL and records everything about it. Anything that can go wrong with one URL is reported in
    /// that URL's row and never escapes, so one bad address can never take down a worker. Only a stop escapes,
    /// as OperationCanceledException.
    /// </summary>
    private async Task CheckAsync(URL url, CancellationToken token)
    {
        string body = "";
        int depth = url.Depth;

        try
        {
            if (!URLCollection.TryParseCrawlUri(url.Url, out Uri? urlCheck))
            {
                url.Notes = "Invalid URL: only absolute http:// or https:// URLs can be checked.";
                url.Status = URLStatus.Error;
                return;
            }

            // Exclude rules must be honoured *before* anything is requested.
            if (Filter.IsExcluded(url.Url, _options.ExcludeRules))
            {
                url.Notes = "Excluded by rule";
                url.Status = URLStatus.Skipped;
                return;
            }

            // Measures the response time only: the wait for this site's turn (see SendAsync) is not counted.
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

            // Quick scan sends HEAD requests: status, type and size only. The body is never read, so
            // links are not followed and highlight rules are not evaluated in this mode.
            // Many servers reject HEAD even though GET works, so those answers fall back to a normal GET.
            HttpResponseMessage? response = null;
            if (_options.QuickScan)
            {
                response = await SendAsync(urlCheck, HttpMethod.Head, timer, token).ConfigureAwait(false);
                if (HeadRejected(response))
                {
                    response.Dispose();
                    response = null;
                }
            }
            response ??= await SendAsync(urlCheck, HttpMethod.Get, timer, token).ConfigureAwait(false);

            using (response)
            {
                // HttpClient does not throw for 4xx/5xx answers, so they are reported here. Like a failed request
                // they get no timing and no MIME type, only the status code and the start of the error page.
                if ((int)response.StatusCode >= 400)
                {
                    string reason = string.IsNullOrEmpty(response.ReasonPhrase) ? response.StatusCode.ToString() : response.ReasonPhrase;
                    url.ContentLength = null;
                    url.StatusCode = (int)response.StatusCode;
                    url.Notes = $"The remote server returned an error: ({(int)response.StatusCode}) {reason}."
                        + Environment.NewLine + NotesSeparator + await ReadErrorBodyAsync(response, token).ConfigureAwait(false);
                    url.Status = URLStatus.Error;
                    return;
                }

                url.StatusCode = (int)response.StatusCode;
                string mime = (RawContentType(response) ?? "").Split(';')[0].Trim();
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
                bool readBody = !_options.QuickScan && !redirected && Filter.IsTextual(mime);
                if (readBody)
                {
                    (body, long bytesRead, bool truncated) = await ReadBodyAsync(response, token).ConfigureAwait(false);
                    // Without the header, the size of what was actually received is the answer
                    // (unless we stopped reading early, then it is unknown).
                    if (contentLength < 0 && !truncated)
                        contentLength = bytesRead;
                }
                // Still unknown: leave it blank instead of showing -1.
                url.ContentLength = contentLength >= 0 ? contentLength : null;

                url.TimeTaken = timer.Elapsed.TotalMilliseconds;
                if (readBody)
                {
                    foreach (SettingsHighlightRule rule in _options.HighlightRules)
                    {
                        if (rule.IsMatch(body))
                        {
                            url.HighlightColor = rule.GetHighlightColor();
                            url.Notes = "Highlight rule matched";
                        }
                    }
                }

                if (redirected)
                {
                    string redirectTarget = finalUri.ToString();

                    // Register the redirect target *before* this URL is marked Redirected (a "finished"
                    // status). Otherwise IsAllDone() can see every URL finished in between and end the crawl early.
                    AddLink(redirectTarget, finalUri, depth, url.Url);

                    url.Notes = "Redirected to " + redirectTarget;
                    // The status code and size above belong to the final page, not to this URL;
                    // that page gets its own row, so leave them blank here.
                    url.StatusCode = null;
                    url.ContentLength = null;
                    url.Status = URLStatus.Redirected;
                }
                else if (readBody && Filter.IsHtml(mime) && depth < _options.MaxDepth)
                {
                    url.Status = URLStatus.Parsing;
                    ParseLinks(url, body, finalUri);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Stopped while a request was in flight: give the URL back instead of reporting a bogus error.
            url.Status = URLStatus.Default;
            throw;
        }
        catch (Exception e)
        {
            url.ContentLength = null;
            url.Notes = DescribeError(e) + Environment.NewLine + NotesSeparator + Truncate(body);
            url.Status = URLStatus.Error;
            return;
        }

        URLStatus status = url.Status;
        if (status is not (URLStatus.Error or URLStatus.External or URLStatus.Redirected or URLStatus.Skipped or URLStatus.Warning))
        {
            // Measured often enough? Otherwise it goes back in the queue for another timing.
            url.Status = url.TimeTakenAttempts >= _options.MaxMeasurements ? URLStatus.Done : URLStatus.Default;
        }
    }

    /// <summary>Adds every link of the page to the collection and records them on the page's row.</summary>
    private void ParseLinks(URL url, string html, Uri pageUri)
    {
        int depth = url.Depth;

        // AngleSharp decodes character references in attribute values while parsing,
        // so the hrefs below must not be HTML-decoded a second time.
        var dom = new HtmlParser().ParseDocument(html);

        // Relative links are resolved against the page they appear on (or its <base href>),
        // not against the URL the crawl started from.
        Uri baseUri = pageUri;
        string? baseHref = dom.QuerySelector("base[href]")?.GetAttribute("href");
        if (!string.IsNullOrWhiteSpace(baseHref)
            && Uri.TryCreate(baseUri, baseHref.Trim(), out Uri? customBase)
            && (customBase.Scheme == Uri.UriSchemeHttp || customBase.Scheme == Uri.UriSchemeHttps))
        {
            baseUri = customBase;
        }

        foreach (var link in dom.QuerySelectorAll("a[href]"))
        {
            string? href = link.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href) || !Filter.IsValid(href))
                continue;

            if (!Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out Uri? parsed))
            {
                // Malformed href: record it and carry on with the rest of the page.
                _collection.Add(href, depth + 1, url.Url, URLStatus.Error);
                url.AddLinkTo(href);
                continue;
            }

            string linkUrl;
            Uri? resolved;
            if (parsed.IsAbsoluteUri)
            {
                linkUrl = href;
                resolved = parsed;
            }
            else if (Uri.TryCreate(baseUri, parsed, out resolved))
            {
                linkUrl = resolved.AbsoluteUri;
            }
            else
            {
                _collection.Add(href, depth + 1, url.Url, URLStatus.Error);
                url.AddLinkTo(href);
                continue;
            }

            AddLink(linkUrl, resolved, depth + 1, url.Url);
            url.AddLinkTo(linkUrl);
        }
    }

    // Files a found address as excluded, part of the site (to be checked) or external (shown, not followed).
    private void AddLink(string linkUrl, Uri resolved, int depth, string from)
    {
        if (Filter.IsExcluded(linkUrl, _options.ExcludeRules))
            _collection.Add(linkUrl, depth, from, URLStatus.Skipped);
        else if (_options.StartingUri.IsBaseOf(resolved))
            _collection.Add(linkUrl, depth, from);
        else
            _collection.Add(linkUrl, depth, from, URLStatus.External);
    }

    private static HttpClient CreateHttpClient()
    {
        // Lets pages declared as windows-1252, shift_jis and the like be read with the right encoding.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // TLS versions are deliberately left to the operating system (no SslProtocols / ServicePointManager
        // setting), so TLS 1.3 works and newer versions need no code change.

        SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = true,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            // A long crawl of one host should still notice DNS changes.
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };

        // The timeout covers the wait for the response headers (the body is read afterwards, see ReadBodyAsync).
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
    }

    /// <summary>
    /// Sends one request once it is this site's turn (see <see cref="CrawlOptions.RequestDelay"/>), identifying the
    /// program in the User-Agent header and carrying the predefined cookies in the Cookie header. The time spent waiting for the turn is taken off <paramref name="timer"/>.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(Uri uri, HttpMethod method, System.Diagnostics.Stopwatch timer, CancellationToken token)
    {
        timer.Stop();
        try
        {
            await _throttle.WaitAsync(uri.Authority, _options.RequestDelay, token).ConfigureAwait(false);
        }
        finally
        {
            timer.Start();
        }

        HttpRequestMessage request = new(method, uri);
        // Not validated: whatever the user typed in the settings is sent as it is.
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
        if (_options.CookieHeader.Length > 0)
            request.Headers.TryAddWithoutValidation("Cookie", _options.CookieHeader);
        return await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
    }

    /// <summary>
    /// The address the response really came from. HttpClient follows redirects itself, but it refuses some
    /// (https to http, for one) and hands back the 3xx answer instead; its Location header still says where it points.
    /// </summary>
    private static Uri ResolveFinalUri(HttpResponseMessage response)
    {
        Uri final = response.RequestMessage!.RequestUri!;
        int code = (int)response.StatusCode;
        if (code is >= 300 and < 400 && response.Headers.Location != null
            && Uri.TryCreate(final, response.Headers.Location, out Uri? target)
            && (target.Scheme == Uri.UriSchemeHttp || target.Scheme == Uri.UriSchemeHttps))
        {
            return target;
        }
        return final;
    }

    /// <summary>The Content-Type header exactly as the server sent it, or null when there is none.</summary>
    private static string? RawContentType(HttpResponseMessage response)
    {
        return response.Content.Headers.NonValidated.TryGetValues("Content-Type", out var values) ? values.ToString() : null;
    }

    /// <summary>True when a HEAD request was answered in a way that usually means "HEAD not supported here".</summary>
    private static bool HeadRejected(HttpResponseMessage response)
    {
        return response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.NotFound
            or HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented;
    }

    /// <summary>The text encoding named by the response's charset, or UTF-8 when it names none (or one we do not know).</summary>
    private static Encoding GetEncoding(HttpContent content)
    {
        try
        {
            string? charset = content.Headers.ContentType?.CharSet;
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
    /// Reads the response body as text, at most <see cref="MaxBodyBytes"/> bytes of it. Also returns the number of
    /// bytes received and whether the body was cut off at the limit.
    /// </summary>
    private static async Task<(string Text, long BytesRead, bool Truncated)> ReadBodyAsync(HttpResponseMessage response, CancellationToken token)
    {
        bool truncated = false;
        using MemoryStream body = new();
        await using (Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
        {
            byte[] chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, token).ConfigureAwait(false)) > 0)
            {
                body.Write(chunk, 0, read);
                if (body.Length >= MaxBodyBytes)
                {
                    truncated = true;
                    break;
                }
            }
        }

        long bytesRead = body.Length;
        body.Position = 0;
        // The charset from the response headers decides how the bytes become text (a byte order mark wins over it).
        using StreamReader reader = new(body, GetEncoding(response.Content), true);
        return (reader.ReadToEnd(), bytesRead, truncated);
    }

    private static string Truncate(string text)
    {
        return text.Length <= MaxNotesBodyChars ? text : text[..MaxNotesBodyChars] + "...";
    }

    /// <summary>Reads (a bounded amount of) the body of an HTTP error response. Only a stop escapes.</summary>
    private static async Task<string> ReadErrorBodyAsync(HttpResponseMessage response, CancellationToken token)
    {
        try
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using StreamReader reader = new(stream, GetEncoding(response.Content), true);
            char[] chunk = new char[MaxNotesBodyChars];
            int read = await reader.ReadAsync(chunk.AsMemory(), token).ConfigureAwait(false);
            return read > 0 ? new string(chunk, 0, read) : "";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return "";
        }
    }

    /// <summary>The exception's message plus those of the exceptions inside it (HttpClient puts the real cause, e.g. "connection refused", there).</summary>
    private static string DescribeError(Exception e)
    {
        StringBuilder text = new(e.Message);
        for (Exception? inner = e.InnerException; inner != null; inner = inner.InnerException)
        {
            if (!text.ToString().Contains(inner.Message, StringComparison.Ordinal))
                text.Append(" -> ").Append(inner.Message);
        }
        return text.ToString();
    }
}
