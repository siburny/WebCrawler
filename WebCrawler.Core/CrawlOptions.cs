namespace WebCrawler;

/// <summary>
/// The settings one crawl runs with, copied when the crawl starts. Workers only ever read this copy,
/// so changing the Settings dialog while a crawl is running can never change (or corrupt) what they see.
/// </summary>
public sealed class CrawlOptions
{
    public Uri StartingUri { get; }
    public int MaxDepth { get; }
    public int MaxMeasurements { get; }
    public bool QuickScan { get; }
    public int MaxConnections { get; }
    public IReadOnlyList<string> ExcludeRules { get; }
    public IReadOnlyList<SettingsHighlightRule> HighlightRules { get; }

    /// <summary>Least time between two requests to the same web site, however many connections there are.</summary>
    public TimeSpan RequestDelay { get; }

    /// <summary>The User-Agent header sent with every request.</summary>
    public string UserAgent { get; }

    /// <summary>The Cookie header sent with every request (the predefined cookies of the settings). Empty = none.</summary>
    public string CookieHeader { get; }

    /// <summary>What the program calls itself when the settings name nothing else. Many sites refuse requests that have no User-Agent.</summary>
    public static string DefaultUserAgent { get; } = CreateDefaultUserAgent();

    public CrawlOptions(Uri startingUri)
    {
        ArgumentNullException.ThrowIfNull(startingUri);

        Settings settings = Settings.Instance;
        StartingUri = startingUri;
        MaxDepth = settings.MaxDepth;
        MaxMeasurements = settings.MaxMeasurements;
        QuickScan = settings.QuickScan;
        MaxConnections = Math.Max(1, settings.MaxProcesses);
        ExcludeRules = (settings.ExcludeRules ?? new List<string>()).Where(rule => !string.IsNullOrEmpty(rule)).ToArray();
        HighlightRules = (settings.HighlightRules ?? new List<SettingsHighlightRule>()).ToArray();
        RequestDelay = TimeSpan.FromMilliseconds(Math.Max(0, settings.RequestDelayMs));
        UserAgent = string.IsNullOrWhiteSpace(settings.UserAgent) ? DefaultUserAgent : settings.UserAgent.Trim();
        // The Settings dialog refuses unusable entries, so only a hand-edited config file can have any; those are left out.
        CookieHeader = CookieList.ToHeader(CookieList.Parse(settings.Cookies, out _));
    }

    private static string CreateDefaultUserAgent()
    {
        Version version = typeof(CrawlOptions).Assembly.GetName().Version ?? new Version(1, 0, 0);
        return $"Mozilla/5.0 (compatible; WebCrawler/{version.Major}.{version.Minor}.{Math.Max(0, version.Build)})";
    }
}
