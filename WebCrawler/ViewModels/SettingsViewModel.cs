using CommunityToolkit.Mvvm.ComponentModel;

namespace WebCrawler.ViewModels;

/// <summary>The Settings window's data. <see cref="TrySave"/> validates everything before the shared settings are touched.</summary>
public partial class SettingsViewModel : ObservableObject
{
    public IReadOnlyList<int> ThreadChoices { get; } = Enumerable.Range(1, 20).ToArray();
    public IReadOnlyList<int> DepthChoices { get; } = Enumerable.Range(0, 10).ToArray();

    [ObservableProperty]
    private int? threads;

    [ObservableProperty]
    private int? depth;

    [ObservableProperty]
    private string ignoreText = "";

    [ObservableProperty]
    private string highlightText = "";

    [ObservableProperty]
    private string cookiesText = "";

    public SettingsViewModel()
    {
        Settings settings = Settings.Instance;
        Threads = ThreadChoices.Contains(settings.MaxProcesses) ? settings.MaxProcesses : null;
        Depth = DepthChoices.Contains(settings.MaxDepth) ? settings.MaxDepth : null;
        HighlightText = string.Join(Environment.NewLine, settings.HighlightRules.Select(x => x.Expression + " => " + x.HighlightColor));
        IgnoreText = string.Join(Environment.NewLine, settings.ExcludeRules);
        CookiesText = settings.Cookies;
    }

    /// <summary>
    /// Validates the input and, when it is all good, stores it in the shared settings and saves them.
    /// Returns null on success, otherwise the message to show (the window then stays open).
    /// </summary>
    public string? TrySave()
    {
        if (Depth == null || Threads == null)
            return "Choose the number of threads and the depth of scanning.";

        string? error = TryParseHighlightRules(HighlightText, out List<SettingsHighlightRule> highlightRules);
        if (error != null)
            return error;

        CookieList.Parse(CookiesText, out string? cookieError);
        if (cookieError != null)
            return "Make sure format of Cookies is valid (one NAME=VALUE per line)." + Environment.NewLine + cookieError;

        Settings settings = Settings.Instance;
        settings.MaxDepth = Depth.Value;
        settings.MaxProcesses = Threads.Value;
        // Whole new lists are assigned (never modified in place), so a crawl that is running keeps seeing a consistent list.
        settings.ExcludeRules = SplitLines(IgnoreText).ToList();
        settings.HighlightRules = highlightRules;
        settings.Cookies = CookiesText.Trim();

        try
        {
            settings.Save();
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return "The settings could not be saved: " + e.Message;
        }
        return null;
    }

    private static string[] SplitLines(string text)
    {
        // The text box may hold either kind of line break, whatever system the program runs on.
        return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    }

    // Parses the highlight rules text into a new list; the current settings are left alone (also when the text is invalid).
    private static string? TryParseHighlightRules(string text, out List<SettingsHighlightRule> rules)
    {
        rules = new List<SettingsHighlightRule>();

        foreach (string line in SplitLines(text))
        {
            string[] parts = line.Split(new[] { "=>" }, StringSplitOptions.None);
            if (parts.Length != 2)
                return "Make sure format of Highlight rules is valid.";

            try
            {
                Convert.ToInt32(parts[1].Trim(), 16);
            }
            catch (Exception)
            {
                return "Make sure format of Highlight rules is valid.";
            }

            try
            {
                rules.Add(new SettingsHighlightRule { Expression = parts[0].Trim(), HighlightColor = parts[1].Trim() });
            }
            catch (ArgumentException)
            {
                // The expression part is not a valid regular expression.
                return "Highlight rule is not a valid regular expression:" + Environment.NewLine + parts[0].Trim();
            }
        }
        return null;
    }
}
