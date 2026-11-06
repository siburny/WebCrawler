namespace WebCrawler;

/// <summary>
/// The predefined cookies of the settings: text with one NAME=VALUE per line. A cookie string pasted from a
/// browser ("a=1; b=2") works as well, because several cookies on one line are separated by semicolons.
/// Only plain ASCII can be sent in a header, so anything else is refused.
/// </summary>
public static class CookieList
{
    // Characters that cannot be part of a cookie name (RFC 6265 "token" separators).
    private const string NameSeparators = "()<>@,;:\\\"/[]?={}";

    /// <summary>
    /// Reads the cookies from the text. Returns every usable cookie in order. <paramref name="error"/> describes the
    /// first entry that cannot be used (such entries are left out), or is null when the whole text was fine.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Parse(string? text, out string? error)
    {
        List<KeyValuePair<string, string>> cookies = new();
        error = null;
        if (string.IsNullOrWhiteSpace(text))
            return cookies;

        string[] lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            foreach (string part in lines[i].Split(';'))
            {
                string entry = part.Trim();
                if (entry.Length == 0)
                    continue;

                int equals = entry.IndexOf('=');
                if (equals <= 0)
                {
                    error ??= $"Line {i + 1}: \"{Shorten(entry)}\" is not in NAME=VALUE format.";
                    continue;
                }

                // Only the first "=" separates: values (base64, for one) may contain more of them.
                string name = entry[..equals].Trim();
                string value = entry[(equals + 1)..].Trim();
                if (!IsValidName(name) || !IsValidValue(value))
                {
                    error ??= $"Line {i + 1}: cookie \"{Shorten(name)}\" contains characters that cannot be sent (plain ASCII only, and no spaces or separators in the name).";
                    continue;
                }

                cookies.Add(new KeyValuePair<string, string>(name, value));
            }
        }
        return cookies;
    }

    /// <summary>The value of a Cookie header for these cookies: "a=1; b=2". Empty when there are none.</summary>
    public static string ToHeader(IEnumerable<KeyValuePair<string, string>> cookies)
    {
        return string.Join("; ", cookies.Select(cookie => cookie.Key + "=" + cookie.Value));
    }

    private static bool IsValidName(string name)
    {
        foreach (char c in name)
        {
            if (c <= ' ' || c > '~' || NameSeparators.Contains(c))
                return false;
        }
        return name.Length > 0;
    }

    private static bool IsValidValue(string value)
    {
        foreach (char c in value)
        {
            if (c < ' ' || c > '~')
                return false;
        }
        return true;
    }

    private static string Shorten(string text)
    {
        return text.Length <= 40 ? text : text[..40] + "...";
    }
}
