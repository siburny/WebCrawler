using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace WebCrawler.Tests;

/// <summary>
/// A small scripted web server on a free local port. Every path stands for one situation a link checker has to
/// cope with: errors, redirects, servers that reject HEAD, unusual character sets, answers without a length...
/// </summary>
public sealed class TestServer : IAsyncLifetime
{
    public const string OkBody = "<html><body>ok</body></html>";
    public static readonly string NoLengthBody = "<html><body>" + new string('x', 100) + "</body></html>";

    private static readonly string[] HomeLinks =
    {
        "/ok", "/missing", "/boom", "/moved", "/multi", "/headfail", "/latin", "/w1252", "/image.png", "/nolen",
        "/loop", "/sub/page", "/ok?a=1&amp;b=2", "mailto:someone@example.com", "#top"
    };

    private WebApplication app;

    /// <summary>Always ends with a slash.</summary>
    public string BaseUrl { get; private set; }

    public int Port { get; private set; }

    /// <summary>Every request received, as "METHOD /path".</summary>
    public ConcurrentQueue<string> Requests { get; } = new ConcurrentQueue<string>();

    /// <summary>Every request received, with the moment it arrived and the User-Agent and Cookie headers it carried.</summary>
    public ConcurrentQueue<RecordedRequest> Log { get; } = new ConcurrentQueue<RecordedRequest>();

    public async Task InitializeAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        app = builder.Build();
        app.Run(Handle);
        await app.StartAsync();

        Port = new Uri(app.Urls.First()).Port;
        BaseUrl = "http://127.0.0.1:" + Port + "/";
    }

    public async Task DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }

    private async Task Handle(HttpContext c)
    {
        string path = c.Request.Path.Value;
        Requests.Enqueue(c.Request.Method + " " + path);
        Log.Enqueue(new RecordedRequest(c.Request.Method, path, c.Request.Headers.UserAgent.ToString(), Stopwatch.GetTimestamp(), c.Request.Headers.Cookie.ToString()));

        // Some servers answer HEAD with "method not allowed" although GET works.
        if (c.Request.Method == "HEAD" && path == "/headfail")
        {
            await Send(c, 405, "text/plain", "HEAD not allowed");
            return;
        }

        switch (path)
        {
            case "/":
                await Send(c, 200, "text/html; charset=utf-8", Home());
                break;
            case "/ok":
            case "/ok2":
            case "/headfail":
                await Send(c, 200, "text/html", OkBody);
                break;
            case "/few":
                await Send(c, 200, "text/html", "<html><body><a href=\"/ok\">a</a><a href=\"/ok2\">b</a><a href=\"/image.png\">c</a></body></html>");
                break;
            case "/missing":
                await Send(c, 404, "text/html", "<html>nope-body</html>");
                break;
            case "/boom":
                await Send(c, 500, "text/plain", "kaboom");
                break;
            case "/moved":
                await Send(c, 301, null, "", location: "/ok2");
                break;
            case "/multi":
                // A redirect status HttpClient does not follow on its own.
                await Send(c, 300, "text/html", "multiple choices", location: "/ok");
                break;
            case "/loop":
                await Send(c, 302, null, "", location: "/loop2");
                break;
            case "/loop2":
                await Send(c, 302, null, "", location: "/loop");
                break;
            case "/latin":
                await Send(c, 200, "text/html; charset=iso-8859-1", Encoding.Latin1.GetBytes("<html><body>café au lait</body></html>"));
                break;
            case "/w1252":
                // Latin1 maps U+0093 / U+0094 to the single bytes 0x93 / 0x94, which are curly quotes in windows-1252.
                await Send(c, 200, "text/html; charset=windows-1252", Encoding.Latin1.GetBytes("<html><body>\u0093quoted\u0094</body></html>"));
                break;
            case "/image.png":
                await Send(c, 200, "image/png", "12345");
                break;
            case "/nolen":
                await Send(c, 200, "text/html", NoLengthBody, length: false);
                break;
            case "/sub/page":
                await Send(c, 200, "text/html", "<html><head><base href=\"/other/\"></head><body><a href=\"rel.html\">r</a></body></html>");
                break;
            case "/other/rel.html":
                await Send(c, 200, "text/html", "<html>rel</html>");
                break;
            case "/slow":
                try
                {
                    await Task.Delay(6000, c.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                await Send(c, 200, "text/html", "<html>slow</html>");
                break;
            default:
                await Send(c, 404, "text/plain", "unknown " + path);
                break;
        }
    }

    private string Home()
    {
        string links = string.Concat(HomeLinks.Select(href => "<a href=\"" + href + "\">x</a>"));
        // "localhost" is a different host than "127.0.0.1", so this one is outside the crawl's scope.
        links += "<a href=\"http://localhost:" + Port + "/other-host\">external</a>";
        return "<html><head><title>t</title></head><body>" + links + "</body></html>";
    }

    private static Task Send(HttpContext c, int status, string contentType, string body, bool length = true, string location = null)
    {
        return Send(c, status, contentType, Encoding.UTF8.GetBytes(body), length, location);
    }

    private static async Task Send(HttpContext c, int status, string contentType, byte[] body, bool length = true, string location = null)
    {
        c.Response.StatusCode = status;
        if (contentType != null)
            c.Response.ContentType = contentType;
        if (location != null)
            c.Response.Headers["Location"] = location;
        if (length)
            c.Response.ContentLength = body.Length;
        else
            await c.Response.StartAsync(); // headers go out now, without a length: the body is sent chunked

        if (c.Request.Method != "HEAD")
            await c.Response.Body.WriteAsync(body, 0, body.Length);
    }
}

/// <summary>One request as the scripted server saw it.</summary>
public sealed record RecordedRequest(string Method, string Path, string UserAgent, long Timestamp, string Cookie);
