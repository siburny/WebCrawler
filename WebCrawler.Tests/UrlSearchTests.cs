using System.Collections.Generic;
using Xunit;

namespace WebCrawler.Tests;

public class UrlSearchTests
{
    private static URL Row(string url, string notes = "", string mimeType = "", URLStatus status = URLStatus.Default, int? statusCode = null)
    {
        URL row = new URL(url);
        row.Notes = notes;
        row.MimeType = mimeType;
        row.Status = status;
        row.StatusCode = statusCode;
        return row;
    }

    [Fact]
    public void TermsAreTheWordsOfTheSearchText()
    {
        Assert.Equal(new[] { "foo", "bar" }, UrlSearch.ParseTerms("  foo   bar\t"));
        Assert.Empty(UrlSearch.ParseTerms(""));
        Assert.Empty(UrlSearch.ParseTerms("   "));
        Assert.Empty(UrlSearch.ParseTerms(null));
    }

    [Fact]
    public void MatchingIgnoresCaseAndLooksAtAddressNotesTypeStatusAndHttpCode()
    {
        URL row = Row("http://Site/Products", "moved elsewhere", "text/html", URLStatus.Redirected, 301);

        Assert.True(UrlSearch.Matches(row, new[] { "products" }));
        Assert.True(UrlSearch.Matches(row, new[] { "ELSEWHERE" }));
        Assert.True(UrlSearch.Matches(row, new[] { "text/html" }));
        Assert.True(UrlSearch.Matches(row, new[] { "redirected" }));
        Assert.True(UrlSearch.Matches(row, new[] { "301" }));
        Assert.False(UrlSearch.Matches(row, new[] { "404" }));
    }

    [Fact]
    public void EveryTermMustMatchSomewhereInTheRow()
    {
        URL row = Row("http://site/products", statusCode: 404);

        Assert.True(UrlSearch.Matches(row, new[] { "404", "products" }));
        Assert.False(UrlSearch.Matches(row, new[] { "404", "missing" }));
    }

    [Fact]
    public void NoTermsMatchNothing()
    {
        Assert.False(UrlSearch.Matches(Row("http://site/"), new string[0]));
    }

    private static List<URL> SampleList()
    {
        return new List<URL>
        {
            Row("http://site/a"),
            Row("http://site/b-match"),
            Row("http://site/c"),
            Row("http://site/d-match"),
        };
    }

    [Fact]
    public void FindNextReturnsTheFirstMatchAtOrAfterTheStartRow()
    {
        List<URL> urls = SampleList();
        bool wrapped;

        Assert.Equal(1, UrlSearch.FindNext(urls, 0, new[] { "match" }, out wrapped));
        Assert.False(wrapped);
        Assert.Equal(1, UrlSearch.FindNext(urls, 1, new[] { "match" }, out wrapped));
        Assert.Equal(3, UrlSearch.FindNext(urls, 2, new[] { "match" }, out wrapped));
        Assert.False(wrapped);
    }

    [Fact]
    public void FindNextContinuesFromTheTopAfterTheLastRow()
    {
        List<URL> urls = SampleList();
        bool wrapped;

        Assert.Equal(1, UrlSearch.FindNext(urls, 4, new[] { "match" }, out wrapped));
        Assert.True(wrapped);
        Assert.Equal(1, UrlSearch.FindNext(urls, 3 + 1, new[] { "match" }, out wrapped));
        Assert.True(wrapped);
    }

    [Fact]
    public void FindNextCanComeBackToTheOnlyMatch()
    {
        List<URL> urls = SampleList();
        bool wrapped;

        // Starting just after the only match goes all the way round and finds it again.
        Assert.Equal(1, UrlSearch.FindNext(urls, 2, new[] { "b-match" }, out wrapped));
        Assert.True(wrapped);
    }

    [Fact]
    public void FindNextReturnsMinusOneWhenNothingMatchesOrThereIsNothingToSearch()
    {
        bool wrapped;

        Assert.Equal(-1, UrlSearch.FindNext(SampleList(), 0, new[] { "nothing" }, out wrapped));
        Assert.False(wrapped);
        Assert.Equal(-1, UrlSearch.FindNext(SampleList(), 0, new string[0], out wrapped));
        Assert.Equal(-1, UrlSearch.FindNext(new List<URL>(), 0, new[] { "match" }, out wrapped));
    }

    [Fact]
    public void FindNextTreatsANegativeStartAsTheTop()
    {
        bool wrapped;

        Assert.Equal(1, UrlSearch.FindNext(SampleList(), -1, new[] { "match" }, out wrapped));
        Assert.False(wrapped);
    }
}
