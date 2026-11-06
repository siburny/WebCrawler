using System.Globalization;

namespace WebCrawler;

/// <summary>
/// Searching the list of addresses: what a search text means, and which row it finds next.
/// </summary>
public static class UrlSearch
{
    /// <summary>The words of a search text (separated by white space); empty when there is nothing to search for.</summary>
    public static string[] ParseTerms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// True when every term occurs, ignoring case, in at least one of the row's texts: address, notes,
    /// MIME type, status or HTTP status code. Without terms nothing matches.
    /// </summary>
    public static bool Matches(URL url, string[] terms)
    {
        if (terms.Length == 0)
            return false;

        string[] texts =
        {
            url.Url,
            url.Notes,
            url.MimeType,
            url.Status.ToString(),
            url.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? ""
        };

        foreach (string term in terms)
        {
            if (!texts.Any(text => text.Contains(term, StringComparison.OrdinalIgnoreCase)))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Finds the first matching row at or after <paramref name="startIndex"/>, continuing from the top of the list
    /// when there is none below. Returns its index, or -1 when no row matches.
    /// </summary>
    /// <param name="wrapped">True when the match was found by continuing from the top of the list.</param>
    public static int FindNext(IReadOnlyList<URL> urls, int startIndex, string[] terms, out bool wrapped)
    {
        wrapped = false;
        int count = urls.Count;
        if (count == 0 || terms.Length == 0)
            return -1;

        if (startIndex < 0)
            startIndex = 0;

        for (int i = 0; i < count; i++)
        {
            int position = startIndex + i;
            int index = position % count;
            if (Matches(urls[index], terms))
            {
                wrapped = position >= count;
                return index;
            }
        }
        return -1;
    }
}
