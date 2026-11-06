using System;
using System.Linq;
using System.Threading.Tasks;

namespace WebCrawler.Tests;

/// <summary>
/// Runs a crawl the way the application does and lets a test wait for it and inspect the results.
/// It is the only place in the tests that knows how a crawl is started.
/// </summary>
public sealed class Crawl : IDisposable
{
    private readonly Crawler crawler;

    public URLCollection Urls { get; } = new URLCollection();

    /// <summary>Starts crawling at once; the first URL decides which addresses count as part of the site.</summary>
    public Crawl(params string[] startUrls)
    {
        foreach (string url in startUrls)
            Urls.Add(url);

        crawler = new Crawler(Urls, new CrawlOptions(Urls.GetStartingUri()));
        crawler.Start();
    }

    /// <summary>Waits (without blocking a thread: the tests run on few) until every URL has a final status.</summary>
    public async Task<bool> WaitUntilDoneAsync(int seconds = 30)
    {
        DateTime end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            if (Urls.IsAllDone())
                return true;
            await Task.Delay(20);
        }
        return Urls.IsAllDone();
    }

    public void Stop()
    {
        crawler.Stop();
    }

    public URL Find(string url)
    {
        return Urls.Snapshot().FirstOrDefault(u => u.Url == url);
    }

    /// <summary>Waits until every worker has actually finished (after <see cref="Stop"/>).</summary>
    public async Task<bool> WaitUntilStoppedAsync(int seconds = 10)
    {
        try
        {
            await crawler.Completion.WaitAsync(TimeSpan.FromSeconds(seconds));
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        crawler.Dispose();
    }
}
