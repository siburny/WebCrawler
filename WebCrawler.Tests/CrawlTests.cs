using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace WebCrawler.Tests;

/// <summary>One full crawl (depth 5, GET requests) of the scripted server, shared by the tests below.</summary>
public sealed class FullScanFixture : IAsyncLifetime
{
    public TestServer Server { get; } = new TestServer();
    public Crawl Result { get; private set; }

    public async Task InitializeAsync()
    {
        await Server.InitializeAsync();

        Settings settings = Settings.CreateDefaults();
        settings.HighlightRules.Add(new SettingsHighlightRule { Expression = "café au lait", HighlightColor = "0xff0000" });
        settings.HighlightRules.Add(new SettingsHighlightRule { Expression = "“quoted”", HighlightColor = "0x00ff00" });
        Settings.Instance = settings;

        Result = new Crawl(Server.BaseUrl, "http://127.0.0.1:1/refused");
        bool finished = await Task.Run(() => Result.WaitUntilDone());
        Result.Stop();
        Assert.True(finished, "the crawl did not finish");
    }

    public async Task DisposeAsync()
    {
        if (Result != null)
            Result.Stop();
        Settings.Instance = Settings.CreateDefaults();
        await Server.DisposeAsync();
    }
}

/// <summary>The same server, but the URLs are given as a list and checked with HEAD requests (quick scan).</summary>
public sealed class QuickScanFixture : IAsyncLifetime
{
    public TestServer Server { get; } = new TestServer();
    public Crawl Result { get; private set; }

    public async Task InitializeAsync()
    {
        await Server.InitializeAsync();

        Settings settings = Settings.CreateDefaults();
        settings.QuickScan = true;
        Settings.Instance = settings;

        Result = new Crawl(new[] { "ok", "headfail", "missing", "boom", "moved", "image.png" }.Select(p => Server.BaseUrl + p).ToArray());
        bool finished = await Task.Run(() => Result.WaitUntilDone());
        Result.Stop();
        Assert.True(finished, "the crawl did not finish");
    }

    public async Task DisposeAsync()
    {
        if (Result != null)
            Result.Stop();
        Settings.Instance = Settings.CreateDefaults();
        await Server.DisposeAsync();
    }
}

[Collection("Settings")]
public class FullScanTests : IClassFixture<FullScanFixture>
{
    private readonly FullScanFixture scan;

    public FullScanTests(FullScanFixture scan)
    {
        this.scan = scan;
    }

    private URL Row(string path)
    {
        URL row = scan.Result.Find(scan.Server.BaseUrl + path);
        Assert.True(row != null, "no row for /" + path);
        return row;
    }

    [Fact]
    public void PageIsDownloadedAndItsDetailsRecorded()
    {
        URL ok = Row("ok");
        Assert.Equal(URLStatus.Done, ok.Status);
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal("text/html", ok.MimeType);
        Assert.Equal(TestServer.OkBody.Length, ok.ContentLength);
        Assert.Equal(1, ok.TimeTakenAttempts);
    }

    [Fact]
    public void LinksOnThePageAreFollowedAndRecordedBothWays()
    {
        URL home = Row("");
        Assert.Equal(URLStatus.Done, home.Status);
        Assert.Contains(scan.Server.BaseUrl + "ok", home.GetLinksTo());
        Assert.Contains(scan.Server.BaseUrl, Row("ok").GetLinksFrom());
    }

    [Fact]
    public void MailtoAndFragmentLinksAreNotCrawled()
    {
        URL[] all = scan.Result.Urls.Snapshot();
        Assert.DoesNotContain(all, u => u.Url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(all, u => u.Url.Contains("#"));
    }

    [Fact]
    public void EntitiesInLinksAreDecodedOnce()
    {
        Assert.Equal(URLStatus.Done, Row("ok?a=1&b=2").Status);
    }

    [Fact]
    public void NotFoundIsAnErrorWithStatusCodeAndStartOfThePage()
    {
        URL missing = Row("missing");
        Assert.Equal(URLStatus.Error, missing.Status);
        Assert.Equal(404, missing.StatusCode);
        Assert.Contains("(404)", missing.Notes);
        Assert.Contains("nope-body", missing.Notes);
        Assert.Equal(0, missing.TimeTakenAttempts);
    }

    [Fact]
    public void ServerErrorIsAnError()
    {
        URL boom = Row("boom");
        Assert.Equal(URLStatus.Error, boom.Status);
        Assert.Equal(500, boom.StatusCode);
        Assert.Contains("kaboom", boom.Notes);
    }

    [Fact]
    public void ConnectionFailureIsAnErrorWithoutStatusCode()
    {
        URL refused = scan.Result.Find("http://127.0.0.1:1/refused");
        Assert.NotNull(refused);
        Assert.Equal(URLStatus.Error, refused.Status);
        Assert.Null(refused.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(refused.Notes));
    }

    [Fact]
    public void RedirectIsRecordedAndItsTargetGetsItsOwnRow()
    {
        URL moved = Row("moved");
        Assert.Equal(URLStatus.Redirected, moved.Status);
        Assert.Equal("Redirected to " + scan.Server.BaseUrl + "ok2", moved.Notes);
        Assert.Null(moved.StatusCode);
        Assert.Equal(URLStatus.Done, Row("ok2").Status);
    }

    [Fact]
    public void RedirectStatusThatTheHttpStackDoesNotFollowIsStillRecorded()
    {
        URL multi = Row("multi");
        Assert.Equal(URLStatus.Redirected, multi.Status);
        Assert.EndsWith("/ok", multi.Notes);
    }

    [Fact]
    public void RedirectLoopEndsInsteadOfHanging()
    {
        Assert.Equal(URLStatus.Redirected, Row("loop").Status);
        Assert.Equal(URLStatus.Redirected, Row("loop2").Status);
    }

    [Fact]
    public void BaseHrefDecidesHowRelativeLinksAreResolved()
    {
        Assert.Equal(URLStatus.Done, Row("sub/page").Status);
        Assert.Equal(URLStatus.Done, Row("other/rel.html").Status);
    }

    [Fact]
    public void OtherHostsAreRecordedButNotCrawled()
    {
        URL external = scan.Result.Find("http://localhost:" + scan.Server.Port + "/other-host");
        Assert.NotNull(external);
        Assert.Equal(URLStatus.External, external.Status);
        Assert.DoesNotContain("GET /other-host", scan.Server.Requests);
    }

    [Fact]
    public void ImagesAreJudgedByTheirHeadersAndNotDownloaded()
    {
        URL image = Row("image.png");
        Assert.Equal(URLStatus.Done, image.Status);
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(5, image.ContentLength);
    }

    [Fact]
    public void SizeOfAnAnswerWithoutContentLengthIsWhatWasReceived()
    {
        Assert.Equal(TestServer.NoLengthBody.Length, Row("nolen").ContentLength);
    }

    [Fact]
    public void HighlightRulesSeeTextInTheCharsetTheServerDeclared()
    {
        URL latin = Row("latin");     // iso-8859-1
        URL w1252 = Row("w1252");     // windows-1252 (needs the code-page encodings to be registered)
        Assert.False(latin.HighlightColor.IsEmpty);
        Assert.False(w1252.HighlightColor.IsEmpty);
        Assert.Equal("Highlight rule matched", latin.Notes);
        Assert.True(Row("ok").HighlightColor.IsEmpty);
    }
}

[Collection("Settings")]
public class QuickScanTests : IClassFixture<QuickScanFixture>
{
    private readonly QuickScanFixture scan;

    public QuickScanTests(QuickScanFixture scan)
    {
        this.scan = scan;
    }

    private URL Row(string path)
    {
        URL row = scan.Result.Find(scan.Server.BaseUrl + path);
        Assert.True(row != null, "no row for /" + path);
        return row;
    }

    [Fact]
    public void HeadIsEnoughWhenTheServerAnswersIt()
    {
        Assert.Equal(URLStatus.Done, Row("ok").Status);
        Assert.Contains("HEAD /ok", scan.Server.Requests);
        Assert.DoesNotContain("GET /ok", scan.Server.Requests);
    }

    [Fact]
    public void ServerThatRejectsHeadIsAskedAgainWithGet()
    {
        URL headfail = Row("headfail");
        Assert.Equal(URLStatus.Done, headfail.Status);
        Assert.Equal(200, headfail.StatusCode);
        Assert.Contains("HEAD /headfail", scan.Server.Requests);
        Assert.Contains("GET /headfail", scan.Server.Requests);
    }

    [Fact]
    public void RealErrorsStayErrorsAfterTheGetFallback()
    {
        Assert.Equal(404, Row("missing").StatusCode);
        Assert.Equal(URLStatus.Error, Row("missing").Status);
        Assert.Equal(URLStatus.Error, Row("boom").Status);
    }

    [Fact]
    public void SizeComesFromTheHeadAnswer()
    {
        Assert.Equal(5, Row("image.png").ContentLength);
    }

    [Fact]
    public void RedirectsAreStillReportedAndTheirTargetsChecked()
    {
        Assert.Equal(URLStatus.Redirected, Row("moved").Status);
        Assert.Equal(URLStatus.Done, Row("ok2").Status);
    }
}

[Collection("Settings")]
public class StopTests
{
    [Fact]
    public async Task StoppingAbortsARequestInFlightAndHandsTheUrlBack()
    {
        Settings.Instance = Settings.CreateDefaults();
        TestServer server = new TestServer();
        await server.InitializeAsync();
        try
        {
            using (Crawl crawl = new Crawl(server.BaseUrl + "slow"))
            {
                DateTime give_up = DateTime.UtcNow.AddSeconds(10);
                while (!server.Requests.Contains("GET /slow") && DateTime.UtcNow < give_up)
                    await Task.Delay(20);
                Assert.Contains("GET /slow", server.Requests);

                Stopwatch watch = Stopwatch.StartNew();
                crawl.Stop();

                URL slow = crawl.Find(server.BaseUrl + "slow");
                while (slow.Status != URLStatus.Default && watch.Elapsed < TimeSpan.FromSeconds(4))
                    await Task.Delay(10);

                // The server would take 6 seconds to answer; a stop must not wait for that, nor report an error.
                Assert.Equal(URLStatus.Default, slow.Status);
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(4));
                Assert.Equal("", slow.Notes);
                Assert.True(crawl.WaitUntilStopped(), "the workers did not finish after the stop");
            }
        }
        finally
        {
            await server.DisposeAsync();
            Settings.Instance = Settings.CreateDefaults();
        }
    }

    [Fact]
    public async Task StoppingAfterTheScanEndedReleasesTheIdleWorkers()
    {
        Settings.Instance = Settings.CreateDefaults();
        TestServer server = new TestServer();
        await server.InitializeAsync();
        try
        {
            using (Crawl crawl = new Crawl(server.BaseUrl + "ok"))
            {
                Assert.True(crawl.WaitUntilDone());
                crawl.Stop();
                Assert.True(crawl.WaitUntilStopped(), "idle workers did not finish after the stop");
            }
        }
        finally
        {
            await server.DisposeAsync();
            Settings.Instance = Settings.CreateDefaults();
        }
    }
}
