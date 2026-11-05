using CsQuery;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web;
using System.Windows.Forms;

namespace WebCrawler
{
    class ChildThread
    {
        // Upper bound for page/error-body text copied into the Notes column.
        private const int MaxNotesBodyChars = 4000;

        private int maximumDepth = Settings.Instance.MaxDepth;
        private int maximumMeasurements = Settings.Instance.MaxMeasurements;
        private bool quickScan = Settings.Instance.QuickScan;
        private Thread childThread;
        private int currentDepth;
        private Uri startingUri;
        private URLCollection collection;
        private bool stopping = false;
        private int index;

        public bool isRunning
        {
            get
            {
                return childThread != null && childThread.ThreadState == ThreadState.Running;
            }
        }

        public ChildThread(int _index, URLCollection _collection)
        {
            collection = _collection;
            index = _index;
            startingUri = collection.GetStartingUri();
            if (startingUri == null)
                throw new InvalidOperationException("There is no valid http/https URL to start crawling from.");
            this.childThread = new Thread(new ThreadStart(ChildThreadProcess));
            this.childThread.Name = "Child Process #" + (index + 1).ToString();
        }

        public void Start()
        {
            this.childThread.Start();
        }

        public void Stop()
        {
            //if (this.childThread.ThreadState == ThreadState.Running)
            this.stopping = true;
            //ThreadPool.QueueUserWorkItem(new WaitCallback(delegate { this.childThread.Join(15000); }));
        }

        public void ChildThreadProcess()
        {
            string buffer;

            while (true)
            {
                if (this.stopping)
                    break;

                // Reset vars
                buffer = "";
                currentDepth = -1;
                URL url = collection.GetNext();

                // if nothing to check, wait
                if (url == null)
                {
                    Thread.Sleep(1000);
                    continue;
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
                    if (Filter.IsExcluded(url.Url))
                    {
                        url.Status = URLStatus.Skipped;
                        url.Notes = "Excluded by rule";
                        continue;
                    }

                    DateTime start = DateTime.Now;

                    // Quick scan sends HEAD requests: status, type and size only. The body is never read, so
                    // links are not followed and highlight rules are not evaluated in this mode.
                    // Many servers reject HEAD even though GET works, so those answers fall back to a normal GET.
                    HttpWebResponse response = null;
                    if (quickScan)
                    {
                        try
                        {
                            response = SendRequest(urlCheck, "HEAD");
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
                        response = SendRequest(urlCheck, "GET");

                    using (response)
                    {
                        url.StatusCode = (int)response.StatusCode;
                        url.MimeType = response.ContentType.Split(new char[] { ';' })[0].Trim();
                        // -1 means the server sent no Content-Length (typical for chunked responses).
                        long contentLength = response.ContentLength;
                        if (!quickScan)
                        {
                            using (MemoryStream body = new MemoryStream())
                            {
                                using (Stream responseStream = response.GetResponseStream())
                                {
                                    responseStream.CopyTo(body);
                                }
                                // Without the header, the size of what was actually received is the answer.
                                if (contentLength < 0)
                                    contentLength = body.Length;

                                body.Position = 0;
                                using (StreamReader readStream = new StreamReader(body))
                                {
                                    buffer = readStream.ReadToEnd();
                                }
                            }
                        }
                        // Still unknown (e.g. quick scan without a header): leave it blank instead of showing -1.
                        url.ContentLength = contentLength >= 0 ? (long?)contentLength : null;

                        url.TimeTaken = (DateTime.Now - start).TotalMilliseconds;
                        if (!quickScan)
                        {
                            foreach (SettingsHighlightRule rule in Settings.Instance.HighlightRules)
                                if (rule.IsMatch(buffer))
                                {
                                    url.HighlightColor = rule.GetHighlightColor();
                                    url.Notes = "Highlight rule matched";
                                }
                        }

                        if (response.ResponseUri.ToString() != urlCheck.ToString())
                        {
                            string redirectTarget = response.ResponseUri.ToString();

                            // Register the redirect target *before* this URL is marked Redirected (a "finished"
                            // status). Otherwise IsAllDone() can see every URL finished in between and end the crawl early.
                            if (Filter.IsExcluded(redirectTarget))
                                collection.Add(redirectTarget, currentDepth, url.Url, URLStatus.Skipped);
                            else if (startingUri.IsBaseOf(response.ResponseUri))
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
                        else if (!quickScan && Filter.IsMimeSupported(response.ContentType) && currentDepth < maximumDepth)
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

                                if (Filter.IsExcluded(linkUrl))
                                    collection.Add(linkUrl, currentDepth + 1, url.Url, URLStatus.Skipped);
                                else if (startingUri.IsBaseOf(resolved))
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

                    Thread.Sleep(250);
                    continue;
                }
                catch (Exception e)
                {
                    url.Status = URLStatus.Error;
                    url.ContentLength = null;
                    url.Notes = e.Message + Environment.NewLine + "-----------------------------------------------------------" + Truncate(buffer);

                    Thread.Sleep(250);
                    continue;
                }

                if (!(url.Status == URLStatus.Error || url.Status == URLStatus.External || url.Status == URLStatus.Redirected || url.Status == URLStatus.Skipped || url.Status == URLStatus.Warning))
                {
                    if (url.TimeTakenAttempts >= maximumMeasurements)
                        url.Status = URLStatus.Done;
                    else
                        url.Status = URLStatus.Default;
                }
                Application.DoEvents();
                Thread.Sleep(250);
            }
        }

        private static HttpWebResponse SendRequest(Uri uri, string method)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
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
        public static bool IsExcluded(string url)
        {
            for (int i = 0; i < Settings.Instance.ExcludeRules.Count; i++)
            {
                if (url.Contains(Settings.Instance.ExcludeRules[i]))
                    return true;
            }
            return false;
        }

        public static bool IsMimeSupported(string mime)
        {
            if (mime.StartsWith("text/"))
                return true;
            else if (mime == "application/x-javascript")
                return false;
            else
                return false;
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