using Xunit;

namespace WebCrawler.Tests;

public class FilterTests
{
    [Theory]
    [InlineData("http://site/logout", "/logout", true)]
    [InlineData("http://site/a/b?session=1", "session=", true)]
    [InlineData("http://site/page", "/logout", false)]
    [InlineData("http://site/Logout", "/logout", false)]   // rules are matched case-sensitively
    public void ExcludeRuleMatchesWhenTheUrlContainsIt(string url, string rule, bool excluded)
    {
        Assert.Equal(excluded, Filter.IsExcluded(url, new[] { "/other", rule }));
    }

    [Fact]
    public void NothingIsExcludedWithoutRules()
    {
        Assert.False(Filter.IsExcluded("http://site/anything", new string[0]));
    }

    [Theory]
    [InlineData("text/html", true)]
    [InlineData("TEXT/HTML", true)]
    [InlineData("application/xhtml+xml", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/json", false)]
    [InlineData("", false)]
    public void OnlyHtmlIsSearchedForLinks(string mime, bool html)
    {
        Assert.Equal(html, Filter.IsHtml(mime));
    }

    [Theory]
    [InlineData("text/plain", true)]
    [InlineData("text/css", true)]
    [InlineData("TEXT/HTML", true)]
    [InlineData("application/json", true)]
    [InlineData("application/javascript", true)]
    [InlineData("application/x-javascript", true)]
    [InlineData("application/xml", true)]
    [InlineData("application/atom+xml", true)]
    [InlineData("application/ld+json", true)]
    [InlineData("image/png", false)]
    [InlineData("application/pdf", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("", false)]
    public void TextualResponsesAreDownloadedAndTheRestIsJudgedByHeaders(string mime, bool textual)
    {
        Assert.Equal(textual, Filter.IsTextual(mime));
    }

    [Theory]
    [InlineData("/page", true)]
    [InlineData("page.html", true)]
    [InlineData("http://site/x", true)]
    [InlineData("#top", false)]
    [InlineData("javascript:void(0)", false)]
    [InlineData("JavaScript:alert(1)", false)]
    [InlineData("mailto:someone@example.com", false)]
    [InlineData("MAILTO:someone@example.com", false)]
    public void OnlyRealLinksAreFollowed(string href, bool valid)
    {
        Assert.Equal(valid, Filter.IsValid(href));
    }
}
