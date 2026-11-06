using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WebCrawler.Tests;

public class HostThrottleTests
{
    [Fact]
    public async Task RequestsToOneHostStartTheDelayApart()
    {
        HostThrottle throttle = new HostThrottle();
        Stopwatch watch = Stopwatch.StartNew();

        // Four callers at once, as four workers would be.
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => throttle.WaitAsync("site:80", TimeSpan.FromMilliseconds(100), CancellationToken.None)));

        // The first goes at once, the others follow at 100, 200 and 300 ms.
        Assert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(280), "took only " + watch.Elapsed);
    }

    [Fact]
    public async Task DifferentHostsDoNotWaitForEachOther()
    {
        HostThrottle throttle = new HostThrottle();
        await throttle.WaitAsync("a:80", TimeSpan.FromSeconds(5), CancellationToken.None);

        Stopwatch watch = Stopwatch.StartNew();
        await throttle.WaitAsync("b:80", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), "waited " + watch.Elapsed);
    }

    [Fact]
    public async Task NoDelayMeansNoWaiting()
    {
        HostThrottle throttle = new HostThrottle();
        Stopwatch watch = Stopwatch.StartNew();
        for (int i = 0; i < 50; i++)
            await throttle.WaitAsync("site:80", TimeSpan.Zero, CancellationToken.None);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), "waited " + watch.Elapsed);
    }

    [Fact]
    public async Task WaitingEndsAtOnceWhenCancelled()
    {
        HostThrottle throttle = new HostThrottle();
        await throttle.WaitAsync("site:80", TimeSpan.FromSeconds(30), CancellationToken.None);

        using CancellationTokenSource cancel = new CancellationTokenSource();
        Task waiting = throttle.WaitAsync("site:80", TimeSpan.FromSeconds(30), cancel.Token);
        await Task.Delay(50);
        cancel.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }
}

[Collection("Settings")]
public class CrawlOptionsTests
{
    [Fact]
    public void ProgramNamesItselfWhenTheSettingsNameNothingElse()
    {
        Settings.Instance = Settings.CreateDefaults();
        CrawlOptions options = new CrawlOptions(new Uri("http://site/"));

        Assert.Equal(CrawlOptions.DefaultUserAgent, options.UserAgent);
        Assert.StartsWith("Mozilla/5.0 (compatible; WebCrawler/", options.UserAgent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankUserAgentSettingMeansTheDefault(string configured)
    {
        Settings settings = Settings.CreateDefaults();
        settings.UserAgent = configured;
        Settings.Instance = settings;

        Assert.Equal(CrawlOptions.DefaultUserAgent, new CrawlOptions(new Uri("http://site/")).UserAgent);
        Settings.Instance = Settings.CreateDefaults();
    }

    [Fact]
    public void ConfiguredUserAgentIsUsedWithoutSurroundingSpaces()
    {
        Settings settings = Settings.CreateDefaults();
        settings.UserAgent = "  MyChecker/2.0 (+https://example.com)  ";
        Settings.Instance = settings;

        Assert.Equal("MyChecker/2.0 (+https://example.com)", new CrawlOptions(new Uri("http://site/")).UserAgent);
        Settings.Instance = Settings.CreateDefaults();
    }

    [Fact]
    public void DelayDefaultsToAQuarterSecondAndNeverGoesNegative()
    {
        Settings.Instance = Settings.CreateDefaults();
        Assert.Equal(TimeSpan.FromMilliseconds(250), new CrawlOptions(new Uri("http://site/")).RequestDelay);

        Settings settings = Settings.CreateDefaults();
        settings.RequestDelayMs = -5;
        Settings.Instance = settings;
        Assert.Equal(TimeSpan.Zero, new CrawlOptions(new Uri("http://site/")).RequestDelay);
        Settings.Instance = Settings.CreateDefaults();
    }
}

[Collection("Settings")]
public class PolitenessTests
{
    private static async Task<(TestServer Server, Crawl Crawl)> RunAsync(Action<Settings> configure, string path)
    {
        Settings settings = Settings.CreateDefaults();
        configure(settings);
        Settings.Instance = settings;

        TestServer server = new TestServer();
        await server.InitializeAsync();
        Crawl crawl = new Crawl(server.BaseUrl + path);
        bool finished = await crawl.WaitUntilDoneAsync();
        crawl.Stop();
        Assert.True(finished, "the crawl did not finish");
        return (server, crawl);
    }

    private static async Task CleanUpAsync(TestServer server, Crawl crawl)
    {
        crawl.Dispose();
        await server.DisposeAsync();
        Settings.Instance = Settings.CreateDefaults();
    }

    [Fact]
    public async Task EveryRequestCarriesTheDefaultUserAgent()
    {
        var (server, crawl) = await RunAsync(s => s.RequestDelayMs = 0, "few");
        try
        {
            Assert.Equal(4, server.Log.Count);
            Assert.All(server.Log, request => Assert.Equal(CrawlOptions.DefaultUserAgent, request.UserAgent));
        }
        finally
        {
            await CleanUpAsync(server, crawl);
        }
    }

    [Fact]
    public async Task ConfiguredUserAgentIsSentOnEveryRequest()
    {
        var (server, crawl) = await RunAsync(s => { s.RequestDelayMs = 0; s.UserAgent = "MyChecker/2.0 (+https://example.com)"; }, "few");
        try
        {
            Assert.Equal(4, server.Log.Count);
            Assert.All(server.Log, request => Assert.Equal("MyChecker/2.0 (+https://example.com)", request.UserAgent));
        }
        finally
        {
            await CleanUpAsync(server, crawl);
        }
    }

    [Fact]
    public async Task QuickScanFallbackRequestAlsoCarriesTheUserAgent()
    {
        var (server, crawl) = await RunAsync(s => { s.RequestDelayMs = 0; s.QuickScan = true; }, "headfail");
        try
        {
            Assert.Equal(new[] { "HEAD", "GET" }, server.Log.Select(r => r.Method).ToArray());
            Assert.All(server.Log, request => Assert.Equal(CrawlOptions.DefaultUserAgent, request.UserAgent));
        }
        finally
        {
            await CleanUpAsync(server, crawl);
        }
    }

    [Fact]
    public async Task RequestsToTheSiteAreSpacedOutNoMatterHowManyConnectionsThereAre()
    {
        var (server, crawl) = await RunAsync(s => { s.RequestDelayMs = 400; s.MaxProcesses = 5; }, "few");
        try
        {
            long[] arrivals = server.Log.Select(r => r.Timestamp).OrderBy(t => t).ToArray();
            Assert.Equal(4, arrivals.Length);
            for (int i = 1; i < arrivals.Length; i++)
            {
                TimeSpan gap = Stopwatch.GetElapsedTime(arrivals[i - 1], arrivals[i]);
                Assert.True(gap >= TimeSpan.FromMilliseconds(330), "requests " + i + " and " + (i + 1) + " were only " + gap.TotalMilliseconds + " ms apart");
            }
        }
        finally
        {
            await CleanUpAsync(server, crawl);
        }
    }

    [Fact]
    public async Task TheWaitForTheSitesTurnIsNotCountedAsResponseTime()
    {
        var (server, crawl) = await RunAsync(s => { s.RequestDelayMs = 400; s.MaxProcesses = 5; }, "few");
        try
        {
            // The three linked pages waited 0.4 to 1.2 seconds for their turn, but the local server answers in milliseconds.
            // (The start page is left out: it is the first request of the process and pays for the cold start.)
            URL[] linked = crawl.Urls.Snapshot().Skip(1).ToArray();
            Assert.Equal(3, linked.Length);
            foreach (URL url in linked)
                Assert.True(url.TimeTaken < 300, url.Url + " took " + url.TimeTaken + " ms");
        }
        finally
        {
            await CleanUpAsync(server, crawl);
        }
    }

    [Fact]
    public async Task ZeroDelayTurnsTheLimitOff()
    {
        var (server, crawl) = await RunAsync(s => { s.RequestDelayMs = 0; s.MaxProcesses = 5; }, "few");
        try
        {
            // The three linked pages are requested together (the start page is left out: it pays for the cold start).
            long[] arrivals = server.Log.Select(r => r.Timestamp).OrderBy(t => t).Skip(1).ToArray();
            Assert.Equal(3, arrivals.Length);
            TimeSpan span = Stopwatch.GetElapsedTime(arrivals.First(), arrivals.Last());
            Assert.True(span < TimeSpan.FromMilliseconds(300), "the three requests took " + span.TotalMilliseconds + " ms to arrive");
        }
        finally
        {
            await CleanUpAsync(server, crawl);
        }
    }
}
