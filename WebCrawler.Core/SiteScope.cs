namespace WebCrawler;

/// <summary>
/// Decides which addresses belong to the site a crawl started from. Those are checked; all others are only listed.
/// A subdomain of the starting host belongs to the site: scanning https://expeditions.com also follows
/// https://world.expeditions.com/page. The parent domain and sibling subdomains do not (scanning
/// https://world.expeditions.com stays on world.expeditions.com), and neither does a host that merely ends
/// in the same letters (notexpeditions.com).
/// </summary>
public static class SiteScope
{
    /// <summary>True when <paramref name="candidate"/> is part of the site that <paramref name="start"/> defines.</summary>
    public static bool Contains(Uri start, Uri candidate)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(candidate);

        if (start.IsBaseOf(candidate))
            return true;
        if (!IsSubdomain(start, candidate))
            return false;

        // The same address as if it were on the starting host, so the path rules (a start address with a
        // path only covers that part of the site) are exactly the ones that apply to the host itself.
        UriBuilder onStartHost = new(candidate) { Host = start.Host };
        return start.IsBaseOf(onStartHost.Uri);
    }

    // A real name below the starting host, on the same scheme and port. IP addresses have no subdomains.
    private static bool IsSubdomain(Uri start, Uri candidate)
    {
        if (start.HostNameType != UriHostNameType.Dns || candidate.HostNameType != UriHostNameType.Dns)
            return false;
        if (candidate.Scheme != start.Scheme || candidate.Port != start.Port)
            return false;

        return candidate.Host.EndsWith("." + start.Host, StringComparison.OrdinalIgnoreCase);
    }
}
