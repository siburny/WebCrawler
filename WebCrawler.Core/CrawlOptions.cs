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
    }
}
