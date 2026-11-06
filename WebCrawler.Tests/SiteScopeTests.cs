using System;
using Xunit;

namespace WebCrawler.Tests;

public class SiteScopeTests
{
    [Theory]
    // The site itself.
    [InlineData("https://expeditions.com/", "https://expeditions.com/page", true)]
    // Subdomains belong to the site, however deep and whatever the letter case.
    [InlineData("https://expeditions.com/", "https://world.expeditions.com/qwe", true)]
    [InlineData("https://expeditions.com", "https://a.b.expeditions.com/x", true)]
    [InlineData("https://expeditions.com/", "https://WORLD.Expeditions.COM/qwe", true)]
    [InlineData("https://expeditions.com/", "https://www.expeditions.com/", true)]
    [InlineData("http://localhost:5000/", "http://a.localhost:5000/x", true)]
    // Other hosts, including ones that only look similar.
    [InlineData("https://expeditions.com/", "https://notexpeditions.com/", false)]
    [InlineData("https://expeditions.com/", "https://expeditions.com.evil.com/", false)]
    [InlineData("https://expeditions.com/", "https://evil.com/world.expeditions.com/", false)]
    [InlineData("https://expeditions.com/", "https://com/", false)]
    // Only downwards: not the parent domain, not a sibling.
    [InlineData("https://world.expeditions.com/", "https://expeditions.com/", false)]
    [InlineData("https://world.expeditions.com/", "https://www.expeditions.com/", false)]
    // Same scheme and port, as for the host itself.
    [InlineData("https://expeditions.com/", "http://world.expeditions.com/", false)]
    [InlineData("https://expeditions.com/", "https://world.expeditions.com:8443/", false)]
    [InlineData("https://expeditions.com:8443/", "https://world.expeditions.com:8443/", true)]
    // A start address with a path covers that part of the site, on subdomains too.
    [InlineData("https://expeditions.com/blog/", "https://world.expeditions.com/blog/post", true)]
    [InlineData("https://expeditions.com/blog/", "https://world.expeditions.com/qwe", false)]
    [InlineData("https://expeditions.com/blog/", "https://expeditions.com/qwe", false)]
    // An IP address is its own site.
    [InlineData("http://127.0.0.1:5000/", "http://localhost:5000/", false)]
    [InlineData("http://127.0.0.1:5000/", "http://127.0.0.1:5000/page", true)]
    public void SubdomainsOfTheStartingHostBelongToTheSite(string start, string candidate, bool expected)
    {
        Assert.Equal(expected, SiteScope.Contains(new Uri(start), new Uri(candidate)));
    }
}
