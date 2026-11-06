using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace WebCrawler.Tests;

public class CookieListTests
{
    private static string Header(string text, out string error)
    {
        return CookieList.ToHeader(CookieList.Parse(text, out error));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n \r\n")]
    public void NothingTypedMeansNoCookies(string text)
    {
        Assert.Equal("", Header(text, out string error));
        Assert.Null(error);
    }

    [Fact]
    public void EachLineIsOneCookie()
    {
        Assert.Equal("session=abc123; theme=dark", Header("session=abc123\r\ntheme=dark", out string error));
        Assert.Null(error);
    }

    [Fact]
    public void AnyKindOfLineBreakAndBlankLinesAreFine()
    {
        Assert.Equal("a=1; b=2; c=3", Header("a=1\n\n b=2 \r\n\rc=3\r\n", out string error));
        Assert.Null(error);
    }

    [Fact]
    public void ACookieStringPastedFromABrowserWorksToo()
    {
        Assert.Equal("a=1; b=2; c=3", Header("a=1; b=2;\r\nc=3", out string error));
        Assert.Null(error);
    }

    [Fact]
    public void OnlyTheFirstEqualsSignSeparatesNameFromValue()
    {
        Assert.Equal("token=YWJj==; x=a=b", Header("token=YWJj==\r\nx=a=b", out string error));
        Assert.Null(error);
    }

    [Fact]
    public void EmptyValueIsAllowedAndSpacesAroundPartsAreTrimmed()
    {
        Assert.Equal("flag=; name=some value", Header("  flag =  \r\n name = some value ", out string error));
        Assert.Null(error);
    }

    [Fact]
    public void SameNameTwiceIsSentTwice()
    {
        Assert.Equal("a=1; a=2", Header("a=1\r\na=2", out string error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("justaword", 1)]
    [InlineData("a=1\r\n=novalue", 2)]
    [InlineData("a=1\r\n\r\nbad name=1", 3)]
    [InlineData("na,me=1", 1)]
    [InlineData("name=caf\u00e9", 1)]
    [InlineData("n\u00e4me=1", 1)]
    public void EntriesThatCannotBeSentAreReportedWithTheirLine(string text, int line)
    {
        CookieList.Parse(text, out string error);
        Assert.NotNull(error);
        Assert.StartsWith("Line " + line + ":", error);
    }

    [Fact]
    public void UsableCookiesAreKeptWhenAnotherEntryIsNot()
    {
        Assert.Equal("a=1; c=3", Header("a=1\r\nbroken\r\nc=3", out string error));
        Assert.NotNull(error);
    }
}

[Collection("Settings")]
public class CookieCrawlTests
{
    [Fact]
    public void CrawlOptionsBuildOneCookieHeaderAndIgnoreUnusableEntries()
    {
        Settings settings = Settings.CreateDefaults();
        settings.Cookies = "session=abc123\r\nbroken\r\ntheme=dark";
        Settings.Instance = settings;
        Assert.Equal("session=abc123; theme=dark", new CrawlOptions(new Uri("http://site/")).CookieHeader);

        Settings.Instance = Settings.CreateDefaults();
        Assert.Equal("", new CrawlOptions(new Uri("http://site/")).CookieHeader);
    }

    [Fact]
    public async Task EveryRequestCarriesThePredefinedCookies()
    {
        var (server, crawl) = await PolitenessTests.RunAsync(s => { s.RequestDelayMs = 0; s.Cookies = "session=abc123\r\ntheme=dark"; }, "few");
        try
        {
            Assert.Equal(4, server.Log.Count);
            Assert.All(server.Log, request => Assert.Equal("session=abc123; theme=dark", request.Cookie));
        }
        finally
        {
            await PolitenessTests.CleanUpAsync(server, crawl);
        }
    }

    [Fact]
    public async Task QuickScanFallbackRequestAlsoCarriesTheCookies()
    {
        var (server, crawl) = await PolitenessTests.RunAsync(s => { s.RequestDelayMs = 0; s.QuickScan = true; s.Cookies = "session=abc123"; }, "headfail");
        try
        {
            Assert.Equal(new[] { "HEAD", "GET" }, server.Log.Select(r => r.Method).ToArray());
            Assert.All(server.Log, request => Assert.Equal("session=abc123", request.Cookie));
        }
        finally
        {
            await PolitenessTests.CleanUpAsync(server, crawl);
        }
    }

    [Fact]
    public async Task NoCookieHeaderIsSentWhenNoneAreSet()
    {
        var (server, crawl) = await PolitenessTests.RunAsync(s => s.RequestDelayMs = 0, "few");
        try
        {
            Assert.Equal(4, server.Log.Count);
            Assert.All(server.Log, request => Assert.Equal("", request.Cookie));
        }
        finally
        {
            await PolitenessTests.CleanUpAsync(server, crawl);
        }
    }
}
