namespace WebCrawler;

internal static class Filter
{
    /// <summary>True when the URL contains one of the exclude rules (a plain, case-sensitive text match).</summary>
    public static bool IsExcluded(string url, IReadOnlyList<string> rules)
    {
        foreach (string rule in rules)
        {
            if (url.Contains(rule, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>Pages that are searched for links to follow.</summary>
    public static bool IsHtml(string mime)
    {
        return mime.Equals("text/html", StringComparison.OrdinalIgnoreCase)
            || mime.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Responses whose body is text, i.e. worth downloading to evaluate highlight rules (and, for HTML, links).</summary>
    public static bool IsTextual(string mime)
    {
        if (mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return true;

        switch (mime.ToLowerInvariant())
        {
            case "application/xhtml+xml":
            case "application/xml":
            case "application/json":
            case "application/javascript":
            case "application/x-javascript":
            case "application/ecmascript":
                return true;
        }

        return mime.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)
            || mime.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>False for links that are not addresses to check: page anchors, javascript: and mailto: links.</summary>
    internal static bool IsValid(string url)
    {
        return !url.StartsWith('#')
            && !url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
    }
}
