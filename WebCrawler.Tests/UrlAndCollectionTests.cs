using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WebCrawler.Tests;

public class UrlTests
{
    [Fact]
    public void NewUrlStartsEmpty()
    {
        URL url = new URL("http://site/");
        Assert.Equal(URLStatus.Default, url.Status);
        Assert.Null(url.StatusCode);
        Assert.Null(url.ContentLength);
        Assert.Equal("", url.MimeType);
        Assert.Equal("", url.Notes);
        Assert.True(url.HighlightColor.IsEmpty);
        Assert.Equal(0, url.TimeTakenAttempts);
        Assert.Equal(0.0, url.TimeTaken);
    }

    [Fact]
    public void TimeTakenIsTheAverageOfAllMeasurements()
    {
        URL url = new URL("http://site/");
        url.TimeTaken = 10;
        url.TimeTaken = 20;
        Assert.Equal(15.0, url.TimeTaken);
        Assert.Equal(2, url.TimeTakenAttempts);
    }

    [Fact]
    public void MeasurementsAreTextWithoutRegionalFormattingAndPaddedToTheRequestedCount()
    {
        URL url = new URL("http://site/");
        url.TimeTaken = 1.5;
        Assert.Equal(new[] { "1.5", "", "" }, url.TimeTakenValues(3));
        Assert.Equal("1.5", url.TimeTakenAll());
    }

    [Fact]
    public void LinksAreRememberedOnceAndEmptyOnesIgnored()
    {
        URL url = new URL("http://site/");
        url.AddLinkTo("http://site/a");
        url.AddLinkTo("http://site/a");
        url.AddLinkTo("");
        url.AddLinkTo(null);
        url.AddLinkFrom("http://site/b");
        url.AddLinkFrom("http://site/b");
        Assert.Equal(new[] { "http://site/a" }, url.GetLinksTo());
        Assert.Equal(new[] { "http://site/b" }, url.GetLinksFrom());
    }
}

public class UrlCollectionTests
{
    [Theory]
    [InlineData("http://example.com/", true)]
    [InlineData("https://example.com/a?b=1", true)]
    [InlineData("ftp://example.com/", false)]
    [InlineData("mailto:someone@example.com", false)]
    [InlineData("/relative/path", false)]
    [InlineData("example.com", false)]
    [InlineData("", false)]
    public void OnlyAbsoluteHttpAndHttpsUrlsCanBeCrawled(string url, bool crawlable)
    {
        Uri parsed;
        Assert.Equal(crawlable, URLCollection.TryParseCrawlUri(url, out parsed));
    }

    [Fact]
    public void BlankUrlsAreIgnored()
    {
        URLCollection urls = new URLCollection();
        Assert.Null(urls.Add(""));
        Assert.Null(urls.Add("   "));
        Assert.Null(urls.Add(null));
        Assert.Equal(0, urls.Count());
    }

    [Fact]
    public void InvalidUrlBecomesAnErrorRowAndIsNeverHandedToAWorker()
    {
        URLCollection urls = new URLCollection();
        URL url = urls.Add("not a url");
        Assert.Equal(URLStatus.Error, url.Status);
        Assert.StartsWith("Invalid URL", url.Notes);
        Assert.Null(urls.TryGetNext());
    }

    [Fact]
    public void KnownUrlIsNotAddedTwiceButEveryReferrerIsRemembered()
    {
        URLCollection urls = new URLCollection();
        URL first = urls.Add("http://site/x", 1, "http://site/a");
        URL second = urls.Add("http://site/x", 1, "http://site/b");
        Assert.Same(first, second);
        Assert.Equal(1, urls.Count());
        Assert.Equal(new[] { "http://site/a", "http://site/b" }, first.GetLinksFrom());
    }

    [Fact]
    public void ExcludedAndExternalRowsCountAsFinishedAndNeverWaitForAWorker()
    {
        URLCollection urls = new URLCollection();
        URL skipped = urls.Add("http://site/logout", 1, "http://site/", URLStatus.Skipped);
        urls.Add("http://elsewhere/", 1, "http://site/", URLStatus.External);
        Assert.Equal("Excluded by rule", skipped.Notes);
        Assert.True(urls.IsAllDone());
        Assert.Null(urls.TryGetNext());
        Assert.Equal(2, urls.GetCounts().Processed);
    }

    [Fact]
    public void CountsFollowEveryStatusChange()
    {
        URLCollection urls = new URLCollection();
        URL a = urls.Add("http://site/a");
        URL b = urls.Add("http://site/b");
        URL c = urls.Add("http://site/c");
        Assert.Equal(3, urls.GetCounts().Total);
        Assert.Equal(0, urls.GetCounts().Processed);

        a.Status = URLStatus.Done;
        b.Status = URLStatus.Error;
        c.Status = URLStatus.Redirected;

        URLCounts counts = urls.GetCounts();
        Assert.Equal(3, counts.Total);
        Assert.Equal(3, counts.Processed);
        Assert.Equal(1, counts.Errors);
        Assert.Equal(1, counts.Redirected);
        Assert.Equal(0, counts.Warnings);
    }

    [Fact]
    public void IsAllDoneOnlyWhenNothingIsWaitingOrBeingProcessed()
    {
        URLCollection urls = new URLCollection();
        URL url = urls.Add("http://site/a");
        Assert.False(urls.IsAllDone());

        Assert.Same(url, urls.TryGetNext());
        Assert.Equal(URLStatus.Downloading, url.Status);
        Assert.False(urls.IsAllDone());

        url.Status = URLStatus.Done;
        Assert.True(urls.IsAllDone());
    }

    [Fact]
    public void WorkIsHandedOutOldestFirstAndOnlyOnce()
    {
        URLCollection urls = new URLCollection();
        URL a = urls.Add("http://site/a");
        URL b = urls.Add("http://site/b");
        Assert.Same(a, urls.TryGetNext());
        Assert.Same(b, urls.TryGetNext());
        Assert.Null(urls.TryGetNext());
    }

    [Fact]
    public void UrlSetBackToDefaultWaitsForAWorkerAgain()
    {
        URLCollection urls = new URLCollection();
        URL url = urls.Add("http://site/a");
        Assert.Same(url, urls.TryGetNext());
        url.Status = URLStatus.Default;
        Assert.Same(url, urls.TryGetNext());
    }

    [Fact]
    public async Task WaitingWorkerWakesAsSoonAsWorkArrives()
    {
        URLCollection urls = new URLCollection();
        Task<URL> worker = urls.GetNextAsync().AsTask();
        await Task.Delay(100);
        Assert.False(worker.IsCompleted);
        urls.Add("http://site/a");

        Assert.Same(worker, await Task.WhenAny(worker, Task.Delay(5000)));
        URL url = await worker;
        Assert.Equal("http://site/a", url.Url);
        Assert.Equal(URLStatus.Downloading, url.Status);
    }

    [Fact]
    public async Task WaitingWorkerStopsWaitingWhenCancelled()
    {
        URLCollection urls = new URLCollection();
        using CancellationTokenSource cancel = new CancellationTokenSource();
        Task<URL> worker = urls.GetNextAsync(cancel.Token).AsTask();
        await Task.Delay(100);

        cancel.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
    }

    [Fact]
    public async Task DetachReleasesWaitingWorkersWithoutWork()
    {
        URLCollection urls = new URLCollection();
        Task<URL> worker = urls.GetNextAsync().AsTask();
        await Task.Delay(100);

        urls.Detach();

        Assert.Same(worker, await Task.WhenAny(worker, Task.Delay(5000)));
        Assert.Null(await worker);
    }

    [Fact]
    public async Task ManyWaitingWorkersEachGetDifferentWork()
    {
        URLCollection urls = new URLCollection();
        Task<URL>[] workers = new Task<URL>[20];
        for (int i = 0; i < workers.Length; i++)
            workers[i] = urls.GetNextAsync().AsTask();

        for (int i = 0; i < workers.Length; i++)
            urls.Add("http://site/" + i);

        URL[] got = await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(workers.Length, got.Select(u => u.Url).Distinct().Count());
    }

    [Fact]
    public async Task DetachedCollectionTakesNothingAndGivesNothing()
    {
        URLCollection urls = new URLCollection();
        urls.Add("http://site/a");
        urls.Detach();
        Assert.Null(urls.Add("http://site/b"));
        Assert.Null(urls.TryGetNext());
        Assert.Null(await urls.GetNextAsync());
    }

    [Fact]
    public void CrawlScopeIsTheFirstValidUrlWithoutItsQueryString()
    {
        URLCollection urls = new URLCollection();
        Assert.Null(urls.GetStartingUri());
        urls.Add("not a url");
        urls.Add("http://site.example/start/page?x=1&y=2");
        urls.Add("http://other.example/");
        Assert.Equal(new Uri("http://site.example/start/page"), urls.GetStartingUri());
    }

    [Fact]
    public void GridListGetsNewRowsOnlyWhenTheUiThreadSyncs()
    {
        URLCollection urls = new URLCollection();
        urls.Add("http://site/a");
        urls.Add("http://site/b");
        urls.Add("http://site/c");
        Assert.Empty(urls.Collection);

        urls.SyncView();
        Assert.Equal(3, urls.Collection.Count);
    }

    [Fact]
    public void ChangedRowsAreRepaintedOnTheNextSync()
    {
        URLCollection urls = new URLCollection();
        URL a = urls.Add("http://site/a");
        urls.SyncView();

        List<string> events = new List<string>();
        urls.Collection.ListChanged += (sender, e) => events.Add(e.ListChangedType + ":" + e.NewIndex);

        a.Notes = "changed";
        Assert.Empty(events);          // workers never raise UI events themselves
        urls.SyncView();
        Assert.Contains("ItemChanged:0", events);
    }
}
