using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WebCrawler.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly string[] args;
    private Crawler? crawler;
    private URLCollection collection = new();
    private string reportName = "";
    private readonly DispatcherTimer refreshTimer;

    // The row/search the last successful Find ended on, which tells a repeated Find Next from a new search.
    private int lastFindRow = -1;
    private string lastFindTerms = "";

    public MainViewModel() : this(Array.Empty<string>())
    {
    }

    public MainViewModel(string[] args)
    {
        this.args = args;
        Find = new FindViewModel(FindNext);
        AttachCollection();

        // Worker threads never touch the grid; this is where their results reach it (a few ms of work per tick).
        // It keeps running after the scan ends so the last updates still show up.
        refreshTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, (_, _) => RefreshTick());
    }

    /// <summary>Set by the window; dialogs and window-only actions go through it.</summary>
    public IWindowServices? Services { get; set; }

    public FindViewModel Find { get; }

    /// <summary>The last starting addresses a scan was started from, newest first (the URL box's dropdown).</summary>
    public ObservableCollection<string> RecentUrls { get; } = new();

    /// <summary>The grid's rows, one per address, in the same order as the crawl's list.</summary>
    public ObservableCollection<UrlRow> Rows { get; } = new();

    [ObservableProperty]
    private UrlRow? selectedRow;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartNewScanCommand))]
    private string urlText = "https://www.expeditions.com/";

    [ObservableProperty]
    private string statusText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartStopText))]
    private bool isCrawling;

    // While a scan runs, the Play button turns into Stop and the Check Website button reads "Stop".
    public string StartStopText => IsCrawling ? "Stop" : "Check Website";

    /// <summary>Called by the window once it is open: loads the settings, applies the command line and starts a scan if asked.</summary>
    public async Task InitializeAsync()
    {
        if (!Settings.Instance.Load())
            Settings.Instance.Save();

        foreach (string url in Settings.Instance.RecentUrls)
            RecentUrls.Add(url);

        await ParseArgumentsAsync(args);
        refreshTimer.Start();

        if (collection.Count() > 0)
            await StartLoadedListAsync();
    }

    /// <summary>Stops a running scan (window closing).</summary>
    public void Shutdown()
    {
        refreshTimer.Stop();
        if (IsCrawling)
            StopCrawl();
        else
            StopWorkers();
    }

    /// <summary>Makes every row read its colours again (the light/dark theme changed).</summary>
    public void RefreshAllRows()
    {
        foreach (UrlRow row in Rows)
            row.Refresh();
    }

    // ---- scan control -------------------------------------------------------------------------------------------

    // Play and "Check Website" are the same control: start a fresh scan of the URL box, or stop the scan that is running.
    [RelayCommand]
    private async Task StartStop()
    {
        if (IsCrawling)
            StopCrawl();
        else
            await StartNewScanAsync();
    }

    // Enter in the URL box starts a scan like the Play / Check Website buttons (it never stops one).
    [RelayCommand]
    private async Task StartNewScan()
    {
        if (!IsCrawling)
            await StartNewScanAsync();
    }

    // Throws away all results and scans the address in the URL box from scratch.
    private async Task StartNewScanAsync()
    {
        string text = UrlText.Trim();
        if (text == "")
            return;

        if (!URLCollection.TryParseCrawlUri(text, out _))
        {
            await ShowMessage("Please enter an absolute URL starting with http:// or https://");
            return;
        }

        RememberUrl(text);

        if (collection.Count() > 0)
            ResetCollection();
        collection.Add(text);

        await BeginCrawlAsync();
    }

    // Scans the URLs that are already in the list (command-line arguments or a loaded URL file) without clearing it.
    private async Task StartLoadedListAsync()
    {
        if (IsCrawling)
            return;

        if (collection.GetStartingUri() == null)
        {
            await ShowMessage("There is no valid URL to crawl. Please use absolute URLs starting with http:// or https://");
            return;
        }

        await BeginCrawlAsync();
    }

    private async Task BeginCrawlAsync()
    {
        IsCrawling = true;
        try
        {
            Uri startingUri = collection.GetStartingUri()
                ?? throw new InvalidOperationException("There is no valid http/https URL to start crawling from.");

            // The settings are copied once per crawl (a change made in the Settings window applies to the next crawl)
            // and shared read-only by all workers. Workers wait for work on their own, so no staggered start.
            crawler = new Crawler(collection, new CrawlOptions(startingUri));
            crawler.Start();
        }
        catch (Exception e)
        {
            // Don't leave the UI stuck in the "crawling" state if the workers could not be started.
            IsCrawling = false;
            await ShowMessage(e.Message);
        }
    }

    private void StopCrawl()
    {
        if (!IsCrawling)
            return;

        IsCrawling = false;
        StopWorkers();
    }

    // Cancels the requests in flight and releases the idle workers; nothing here waits for them.
    private void StopWorkers()
    {
        crawler?.Dispose();
        crawler = null;
    }

    // Replaces the results with a new, empty collection. A new collection (instead of Clear) means threads
    // still finishing the previous scan cannot add their results to the new one.
    private void ResetCollection()
    {
        // Threads of the old scan that are still winding down get no more work and their results are dropped.
        collection.Collection.ListChanged -= CollectionListChanged;
        collection.Detach();
        Rows.Clear();
        SelectedRow = null;
        collection = new URLCollection();
        AttachCollection();
        lastFindRow = -1;
    }

    private void AttachCollection()
    {
        collection.Collection.ListChanged += CollectionListChanged;
    }

    // Raised by URLCollection.SyncView, i.e. on the UI thread: new addresses become rows, changed ones repaint.
    private void CollectionListChanged(object? sender, ListChangedEventArgs e)
    {
        switch (e.ListChangedType)
        {
            case ListChangedType.ItemAdded:
                Rows.Add(new UrlRow(collection.Collection[e.NewIndex]));
                break;
            case ListChangedType.ItemChanged:
                if (e.NewIndex >= 0 && e.NewIndex < Rows.Count)
                    Rows[e.NewIndex].Refresh();
                break;
        }
    }

    private void RefreshTick()
    {
        collection.SyncView();

        if (!IsCrawling)
            return;

        URLCounts counts = collection.GetCounts();
        StatusText = $"URLs: {counts.Total} | Processed: {counts.Processed} | Redirected: {counts.Redirected} | Warnings: {counts.Warnings} | Errors: {counts.Errors}";

        if (collection.IsAllDone())
        {
            StopCrawl();
            if (!string.IsNullOrEmpty(reportName))
            {
                SaveReport();
                Services?.CloseWindow();
            }
        }
    }

    private void SaveReport()
    {
        // The report is built from a snapshot of the real data, not from the grid's list (which can trail slightly).
        URL[] urls = collection.Snapshot();
        if (reportName == "console")
            ReportWriter.WriteConsole(urls, Console.Out);
        else
            ReportWriter.WriteFile(urls, reportName);
    }

    // ---- command line ---------------------------------------------------------------------------------------------

    private async Task ParseArgumentsAsync(string[] arguments)
    {
        string urlArg = "";

        int i = 0;
        while (i < arguments.Length)
        {
            switch (arguments[i])
            {
                case "-i":
                case "-I":
                    i++;
                    if (i >= arguments.Length)
                    {
                        await ShowMessage("No file specified for parameter '-i'");
                        return;
                    }
                    try
                    {
                        AddUrlsFromFile(arguments[i]);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        await ShowMessage("Cannot read the file '" + arguments[i] + "': " + e.Message);
                        return;
                    }
                    break;

                case "-d":
                case "-D":
                    i++;
                    if (i >= arguments.Length)
                    {
                        await ShowMessage("No depth specified for parameter '-d'");
                        return;
                    }
                    if (int.TryParse(arguments[i], out int depth))
                        Settings.Instance.MaxDepth = depth;
                    else
                    {
                        await ShowMessage("Cannot accept depth specified for parameter '-d': " + arguments[i]);
                        return;
                    }
                    break;

                case "-n":
                case "-N":
                    i++;
                    if (i >= arguments.Length)
                    {
                        await ShowMessage("No number specified for parameter '-n'");
                        return;
                    }
                    if (int.TryParse(arguments[i], out int maxMeasurements))
                        Settings.Instance.MaxMeasurements = maxMeasurements;
                    else
                    {
                        await ShowMessage("Cannot accept number specified for parameter '-n': " + arguments[i]);
                        return;
                    }
                    break;

                case "-o":
                case "-O":
                    i++;
                    if (i >= arguments.Length)
                    {
                        await ShowMessage("No filename specified for parameter '-o'");
                        return;
                    }
                    reportName = arguments[i].Replace("%d", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                    break;

                case "-q":
                case "-Q":
                    Settings.Instance.QuickScan = true;
                    break;

                default:
                    if (urlArg == "")
                    {
                        urlArg = arguments[i];
                        collection.Add(urlArg);
                        break;
                    }
                    await ShowMessage("Unknown option: " + arguments[i]);
                    return;
            }
            i++;
        }

        if (urlArg != "")
        {
            UrlText = urlArg;
            if (URLCollection.TryParseCrawlUri(urlArg, out _))
                RememberUrl(urlArg);
        }
    }

    // Keeps the address in the dropdown (newest first, at most Settings.MaxRecentUrls) and in the settings file.
    private void RememberUrl(string url)
    {
        Settings settings = Settings.Instance;
        settings.AddRecentUrl(url);

        // Changed in place (not rebuilt) so the editable URL box keeps its text.
        string newest = settings.RecentUrls[0];
        int index = RecentUrls.IndexOf(newest);
        if (index > 0)
            RecentUrls.Move(index, 0);
        else if (index < 0)
            RecentUrls.Insert(0, newest);
        while (RecentUrls.Count > Settings.MaxRecentUrls)
            RecentUrls.RemoveAt(RecentUrls.Count - 1);

        try
        {
            settings.Save();
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            // Not being able to save the history must not get in the way of the scan.
        }
    }

    private void AddUrlsFromFile(string path)
    {
        foreach (string url in File.ReadAllLines(path))
            collection.Add(url);
    }

    // ---- toolbar ---------------------------------------------------------------------------------------------------

    [RelayCommand]
    private async Task LoadUrlList()
    {
        if (Services == null)
            return;
        string? path = await Services.PickUrlListAsync();
        if (path == null)
            return;

        // Loading a list replaces whatever is in the grid, including a scan that is still running.
        StopCrawl();
        ResetCollection();

        try
        {
            AddUrlsFromFile(path);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            await ShowMessage("Cannot read the file '" + path + "': " + e.Message);
            return;
        }

        if (collection.Count() > 0)
            await StartLoadedListAsync();
    }

    [RelayCommand]
    private async Task ShowNextError()
    {
        if (!SelectNext(row => row.Url.Status == URLStatus.Error))
            await ShowMessage("Can't find any more errors.");
    }

    [RelayCommand]
    private async Task ShowNextHighlight()
    {
        if (!SelectNext(row => !row.Url.HighlightColor.IsEmpty))
            await ShowMessage("Can't find any more highlighted rows.");
    }

    // Selects the first row after the selected one that satisfies the test and scrolls to it.
    private bool SelectNext(Func<UrlRow, bool> test)
    {
        int top = SelectedRow != null ? Rows.IndexOf(SelectedRow) : -1;
        for (int i = top + 1; i < Rows.Count; i++)
        {
            if (test(Rows[i]))
            {
                SelectAndReveal(Rows[i]);
                return true;
            }
        }
        return false;
    }

    private void SelectAndReveal(UrlRow row)
    {
        SelectedRow = row;
        Services?.RevealRow(row);
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        if (Services != null)
            await Services.ShowSettingsAsync();
    }

    [RelayCommand]
    private void OpenFind() => Services?.ShowFind();

    public void ShowInfo(UrlRow? row)
    {
        if (row != null)
            Services?.ShowInfo(row.Url);
    }

    private Task ShowMessage(string text) => Services != null ? Services.ShowMessageAsync(text) : Task.CompletedTask;

    // ---- find --------------------------------------------------------------------------------------------------------

    // One Find Next: selects the next row that matches the search text and scrolls to it. Returns the text
    // the Find window shows. The list keeps growing while a scan runs, so every search looks at the rows there are right now.
    private string FindNext(string text)
    {
        string[] terms = UrlSearch.ParseTerms(text);
        string termsKey = string.Join(" ", terms);
        IReadOnlyList<URL> urls = collection.Collection;

        int current = SelectedRow != null ? Rows.IndexOf(SelectedRow) : -1;
        // Repeating a search moves on from the row it just found; a new search, or a row picked by hand, starts at the selected row itself.
        bool repeated = current >= 0 && current == lastFindRow && string.Equals(termsKey, lastFindTerms, StringComparison.OrdinalIgnoreCase);
        int start = current < 0 ? 0 : (repeated ? current + 1 : current);

        int found = UrlSearch.FindNext(urls, start, terms, out bool wrapped);
        if (found < 0 || found >= Rows.Count)
        {
            lastFindRow = -1;
            return "No matches found.";
        }

        SelectAndReveal(Rows[found]);
        lastFindRow = found;
        lastFindTerms = termsKey;
        return "Row " + (found + 1).ToString(CultureInfo.CurrentCulture) + " of " + urls.Count.ToString(CultureInfo.CurrentCulture)
            + (wrapped ? " (continued from the top)" : "");
    }
}
