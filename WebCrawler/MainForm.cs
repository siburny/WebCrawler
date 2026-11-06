using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Linq;
using System.IO;

namespace WebCrawler;

public partial class MainForm : Form
{
    private bool crawling = false;
    private Crawler crawler;
    private URLCollection collection;
    private string[] args = null;
    private string reportName = "";
    private Image playImage;
    private Image stopImage;
    private string checkButtonText;

    // The Find window (created the first time it is needed) and the row/search its last successful search ended on,
    // which tells a repeated Find Next from a new search.
    private FindForm findForm;
    private int lastFindRow = -1;
    private string lastFindTerms = "";

    // The grid's row height and column widths are pixel values, which the DataGridView does not scale
    // with the display DPI on its own, so they are kept here in 96-DPI units and scaled in ApplyGridDpiScaling.
    private const int GridRowHeight = 18;
    private static readonly Dictionary<string, int> GridColumnWidths = new Dictionary<string, int>
    {
        { "statusDataGridViewImageColumn", 40 },
        { "statusDataGridViewTextBoxColumn", 75 },
        { "httpStatusDataGridViewTextBoxColumn", 45 },
        { "depthDataGridViewTextBoxColumn", 45 },
        { "mimeTypeDataGridViewTextBoxColumn", 80 },
        { "TimeTaken", 70 },
    };
    private static readonly Dictionary<string, int> GridColumnMinimumWidths = new Dictionary<string, int>
    {
        { "urlDataGridViewTextBoxColumn", 250 },
        { "notesDataGridViewTextBoxColumn", 60 },
    };
    private readonly Dictionary<string, Image> statusImageSources = new Dictionary<string, Image>();
    private readonly Dictionary<string, Image> statusImages = new Dictionary<string, Image>();

    public MainForm() : this(null)
    {
    }

    public MainForm(string[] _args)
    {
        InitializeComponent();
        args = _args;

        foreach (string key in new[] { "success", "failure", "redirect", "external" })
            statusImageSources[key] = statusImageList.Images[key];
        // Shown for rows that are still queued or skipped.
        if (statusDataGridViewImageColumn.Image != null)
            statusImageSources["default"] = new Bitmap(statusDataGridViewImageColumn.Image);

        // Raised after the form has rescaled the grid when it moves to a monitor with a different DPI.
        urlDataGridView.DpiChangedAfterParent += (s, e) => ApplyGridDpiScaling();
    }

    private static Image ScaleImage(Image source, int height)
    {
        int width = Math.Max(1, (int)Math.Round(source.Width * (double)height / source.Height));
        Bitmap result = new Bitmap(width, height);
        using (Graphics g = Graphics.FromImage(result))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, width, height));
        }
        return result;
    }

    // Scales row height, column widths and the status icons to the DPI the grid is currently shown at.
    // Always computed from the 96-DPI values, so calling it repeatedly gives the same result.
    private void ApplyGridDpiScaling()
    {
        int rowHeight = urlDataGridView.LogicalToDeviceUnits(GridRowHeight);
        urlDataGridView.RowTemplate.Height = rowHeight;

        // Url and Notes fill the remaining width (3:1) and Content Length sizes itself, so only the other columns get a fixed width.
        foreach (DataGridViewColumn column in urlDataGridView.Columns)
        {
            int width;
            if (GridColumnWidths.TryGetValue(column.Name, out width))
                column.Width = urlDataGridView.LogicalToDeviceUnits(width);

            int minimumWidth;
            if (GridColumnMinimumWidths.TryGetValue(column.Name, out minimumWidth))
                column.MinimumWidth = urlDataGridView.LogicalToDeviceUnits(minimumWidth);
        }

        // The status icons fill the row height (16 px at 100%), scaled from the original images with high-quality resampling.
        int iconSize = rowHeight - urlDataGridView.LogicalToDeviceUnits(2);
        Image[] oldImages = statusImages.Values.ToArray();
        statusImages.Clear();
        foreach (KeyValuePair<string, Image> source in statusImageSources)
            statusImages[source.Key] = ScaleImage(source.Value, iconSize);
        urlDataGridView.Invalidate();
        foreach (Image old in oldImages)
            old.Dispose();

        // Rows that already exist keep the height they were created with.
        foreach (DataGridViewRow row in urlDataGridView.Rows)
        {
            if (row.Height != rowHeight)
                row.Height = rowHeight;
        }
    }

    // Play and "Check Website" are the same control: start a fresh scan of the URL box,
    // or stop the scan that is running.
    private void startStopButton_Click(object sender, EventArgs e)
    {
        if (crawling)
            StopCrawl();
        else
            StartNewScan();
    }

    // Throws away all results and scans the address in the URL box from scratch.
    private void StartNewScan()
    {
        string text = urlTextBox.Text.Trim();
        if (text == "")
            return;

        Uri parsed;
        if (!URLCollection.TryParseCrawlUri(text, out parsed))
        {
            MessageBox.Show("Please enter an absolute URL starting with http:// or https://");
            return;
        }

        if (collection.Count() > 0)
            ResetCollection();
        collection.Add(text);

        BeginCrawl();
    }

    // Scans the URLs that are already in the list (command-line arguments or a loaded URL file) without clearing it.
    private void StartLoadedList()
    {
        if (crawling)
            return;

        if (collection.GetStartingUri() == null)
        {
            MessageBox.Show("There is no valid URL to crawl. Please use absolute URLs starting with http:// or https://");
            return;
        }

        BeginCrawl();
    }

    // Replaces the results with a new, empty collection. A new collection (instead of Clear) means threads
    // still finishing the previous scan cannot add their results to the new one.
    private void ResetCollection()
    {
        // Unbind the grid from the old results *first*: while it is still bound, rebinding makes it
        // re-format its current cell, and that lookup must not hit the new (empty) collection.
        urlDataGridView.DataSource = null;
        // Threads of the old scan that are still winding down get no more work and their results are dropped.
        if (collection != null)
            collection.Detach();
        collection = new URLCollection();
        urlDataGridView.DataSource = collection.Collection;
        lastFindRow = -1;
    }

    private void BeginCrawl()
    {
        crawling = true;
        SetRunningState(true);

        try
        {
            Start();
        }
        catch
        {
            // Don't leave the UI stuck in the "crawling" state if the threads could not be started.
            crawling = false;
            SetRunningState(false);
            throw;
        }
    }

    private void StopCrawl()
    {
        if (!crawling)
            return;

        crawling = false;
        SetRunningState(false);
        Stop();
    }

    // While a scan runs, the Play button turns into Stop and the Check Website button reads "Stop".
    private void SetRunningState(bool running)
    {
        if (stopImage == null)
        {
            playImage = toolStripButtonPlay.Image;
            stopImage = CreateStopImage(playImage != null ? playImage.Size : new Size(16, 16));
            checkButtonText = checkButton.Text;
        }

        toolStripButtonPlay.Image = running ? stopImage : playImage;
        toolStripButtonPlay.Text = running ? "Stop" : "Play";
        checkButton.Text = running ? "Stop" : checkButtonText;
    }

    private static Image CreateStopImage(Size size)
    {
        Bitmap bitmap = new Bitmap(size.Width, size.Height);
        using (Graphics g = Graphics.FromImage(bitmap))
        using (Brush brush = new SolidBrush(Color.Firebrick))
        {
            int inset = Math.Max(2, size.Width / 5);
            g.FillRectangle(brush, inset, inset, size.Width - 2 * inset, size.Height - 2 * inset);
        }
        return bitmap;
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
        ApplyGridDpiScaling();

        if (!Settings.Instance.Load())
            Settings.Instance.Save();
        collection = new URLCollection();
        urlDataGridView.AutoGenerateColumns = false;
        urlDataGridView.DataSource = collection.Collection;

        ParseArguments(args);

        if (collection.Count() > 0)
            StartLoadedList();
    }

    private void ParseArguments(string[] args)
    {
        string url_arg = "";

        if (args == null)
            return;

        int i = 0;
        while (i < args.Length)
        {
            switch (args[i])
            {
                case "-i":
                case "-I":
                    i++;
                    if (i >= args.Length)
                    {
                        MessageBox.Show("No file specified for parameter '-i'");
                        return;
                    }

                    string[] urls = File.ReadAllLines(args[i]);
                    foreach (string url in urls)
                    {
                        collection.Add(url);
                    }
                    break;

                case "-d":
                case "-D":
                    i++;
                    if (i >= args.Length)
                    {
                        MessageBox.Show("No depth specified for parameter '-d'");
                        return;
                    }
                    int depth = -1;
                    if (int.TryParse(args[i], out depth))
                        Settings.Instance.MaxDepth = depth;
                    else
                    {
                        MessageBox.Show("Cannot accept depth specified for parameter '-d': " + args[i] + "");
                        return;
                    }
                    break;

                case "-n":
                case "-N":
                    i++;
                    if (i >= args.Length)
                    {
                        MessageBox.Show("No number specified for parameter '-n'");
                        return;
                    }
                    int maxMeasurements = -1;
                    if (int.TryParse(args[i], out maxMeasurements))
                        Settings.Instance.MaxMeasurements = maxMeasurements;
                    else
                    {
                        MessageBox.Show("Cannot accept number specified for parameter '-n': " + args[i] + "");
                        return;
                    }
                    break;

                case "-o":
                case "-O":
                    i++;
                    if (i >= args.Length)
                    {
                        MessageBox.Show("No filename specified for parameter '-o'");
                        return;
                    }
                    reportName = args[i].Replace("%d", DateTime.Now.ToString("yyyyMMdd"));
                    break;

                case "-q":
                case "-Q":
                    Settings.Instance.QuickScan = true;
                    break;

                default:
                    if (url_arg == "")
                    {
                        url_arg = args[i];
                        collection.Add(url_arg);
                        break;
                    }
                    MessageBox.Show("Unknown option: " + args[i]);
                    return;
            }
            i++;
        }
    }

    private void refreshTimer_Tick(object sender, EventArgs e)
    {
        if (collection == null)
            return;

        // Worker threads never touch the grid; this is where their results reach it (a few ms of work per tick).
        // It keeps running after the scan ends so the last updates still show up.
        collection.SyncView();

        if (crawling)
        {
            URLCounts counts = collection.GetCounts();
            StringBuilder sb = new StringBuilder();
            sb.Append("URLs: " + counts.Total.ToString());
            sb.Append(" | ");
            sb.Append("Processed: " + counts.Processed.ToString());
            sb.Append(" | ");
            sb.Append("Redirected: " + counts.Redirected.ToString());
            sb.Append(" | ");
            sb.Append("Warnings: " + counts.Warnings.ToString());
            sb.Append(" | ");
            sb.Append("Errors: " + counts.Errors.ToString());
            toolStripStatusLabelTotalURLs.Text = sb.ToString();

            if (collection.IsAllDone())
            {
                StopCrawl();
                if (!string.IsNullOrEmpty(reportName))
                {
                    SaveReport(reportName == "console");
                    Close();
                }
            }
        }
    }

    private void SaveReport(bool console = false)
    {
        // The report is built from a snapshot of the real data, not from the grid's list (which can trail slightly).
        URL[] urls = collection.Snapshot();

        if (console)
        {
            Console.WriteLine("URL".PadRight(100, ' ') + "Status".PadRight(10, ' ') + "HTTP".PadRight(6, ' ') + "Time Taken");
            foreach (URL url in urls)
            {
                Console.WriteLine(url.Url.ToString().PadRight(100, ' ').Substring(0, 98) + "  " + url.Status.ToString().PadRight(10, ' ') + FormatStatusCode(url).PadRight(6, ' ') + url.TimeTakenAll(" | "));
            }
        }
        else
        {
            // A ".csv" report is real CSV (comma separated, fields quoted where needed). Any other
            // file name keeps the tab-separated layout.
            bool csv = reportName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
            string separator = csv ? "," : "\t";

            // One "Time Taken" column per measurement, so every row has the same number of columns
            // and Status / Highlight / HTTP Status always sit in the same place.
            int measurements = Math.Max(1, urls.Select(x => x.TimeTakenAttempts).DefaultIfEmpty(0).Max());

            using (StreamWriter file = new StreamWriter(this.reportName))
            {
                List<string> header = new List<string>();
                header.Add("URL");
                for (int i = 1; i <= measurements; i++)
                    header.Add(measurements == 1 ? "Time Taken" : "Time Taken " + i.ToString(CultureInfo.InvariantCulture));
                header.Add("Status");
                header.Add("Highlight");
                header.Add("HTTP Status");
                file.WriteLine(string.Join(separator, header.Select(f => csv ? CsvField(f) : f)));

                foreach (URL url in urls)
                {
                    List<string> fields = new List<string>();
                    fields.Add(url.Url.ToString());
                    fields.AddRange(url.TimeTakenValues(measurements));
                    fields.Add(url.Status.ToString());
                    fields.Add(url.HighlightColor.IsEmpty ? "" : url.HighlightColor.Name);
                    fields.Add(FormatStatusCode(url));
                    file.WriteLine(string.Join(separator, fields.Select(f => csv ? CsvField(f) : f)));
                }
                file.Close();
            }
        }
    }

    private static string FormatStatusCode(URL url)
    {
        return url.StatusCode.HasValue ? url.StatusCode.Value.ToString(CultureInfo.InvariantCulture) : "";
    }

    private static string CsvField(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.IndexOfAny(new char[] { ',', '"', '\r', '\n' }) >= 0)
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (crawling)
        {
            Stop();
        }
    }

    private void urlDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        // The grid can briefly hold rows the list no longer has (list swapped/cleared, change events still queued).
        if (e.RowIndex < 0 || e.RowIndex >= collection.Collection.Count)
            return;

        if (urlDataGridView.Columns[e.ColumnIndex].Name.Equals("statusDataGridViewImageColumn"))
        {
            URL url = collection.Collection[e.RowIndex];

            if (url.Status == URLStatus.Redirected)
                e.Value = statusImages["redirect"];
            else if (url.Status == URLStatus.External)
                e.Value = statusImages["external"];
            else if (url.Status == URLStatus.Done)
                e.Value = statusImages["success"];
            else if (url.Status == URLStatus.Error)
                e.Value = statusImages["failure"];
            else if (statusImages.ContainsKey("default"))
                e.Value = statusImages["default"];
        }
    }

    private void urlDataGridView_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= collection.Collection.Count)
            return;

        if ((urlDataGridView.Rows[e.RowIndex].State & DataGridViewElementStates.Selected) == 0)
        {
            URL url = collection.Collection[e.RowIndex];
            if (url.HighlightColor.IsEmpty)
            {
                switch (url.Status)
                {
                    case URLStatus.Error:
                        urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Red;
                        break;
                    case URLStatus.External:
                    case URLStatus.Redirected:
                        urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Blue;
                        break;
                    case URLStatus.Done:
                        urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Green;
                        break;
                    case URLStatus.Skipped:
                        urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Brown;
                        break;
                    default:
                        urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Orange;
                        break;
                }
            }
            else
                urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = url.HighlightColor;
        }
    }

    private void Start()
    {
        Uri startingUri = collection.GetStartingUri();
        if (startingUri == null)
            throw new InvalidOperationException("There is no valid http/https URL to start crawling from.");

        // The settings are copied once per crawl (a change made in the Settings dialog applies to the next crawl)
        // and shared read-only by all workers. Workers wait for work on their own, so no staggered start.
        crawler = new Crawler(collection, new CrawlOptions(startingUri));
        crawler.Start();
    }

    private void Stop()
    {
        // Cancels the requests in flight and releases the idle workers; nothing here waits for them.
        crawler?.Dispose();
        crawler = null;
    }

    private void urlDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex > -1 && e.RowIndex < collection.Collection.Count)
        {
            URL url = collection.Collection[e.RowIndex];
            InfoForm.Instance.ShowInfo(url);
        }
    }

    private void urlTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            // Enter in the URL box starts a scan like the Play / Check Website buttons (it never stops one).
            if (!crawling)
                StartNewScan();
            e.Handled = true;
        }
    }

    private void urlDataGridView_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
    {
        //urlDataGridView.ClearSelection();
    }

    private void toolStripButtonShowNextError_Click(object sender, EventArgs e)
    {
        int top = 0;
        if (urlDataGridView.CurrentCell != null)
            top = urlDataGridView.CurrentCell.RowIndex;

        var scroll = collection.Collection.Select((v, i) => new { url = v, index = i }).FirstOrDefault(x => x.index > top && x.url.Status == URLStatus.Error);
        if (scroll != null)
        {
            //urlDataGridView.ClearSelection();
            urlDataGridView.CurrentCell = urlDataGridView.Rows[scroll.index].Cells[0];
            if (scroll.index < 2)
                urlDataGridView.FirstDisplayedScrollingRowIndex = 0;
            else
                urlDataGridView.FirstDisplayedScrollingRowIndex = scroll.index - 2;
        }
        else
            MessageBox.Show("Can't find any more errors.");
    }

    // Ctrl+F opens the Find window, wherever the focus is in this window.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F))
        {
            ShowFindWindow();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void toolStripButtonFind_Click(object sender, EventArgs e)
    {
        ShowFindWindow();
    }

    private void ShowFindWindow()
    {
        if (findForm == null)
        {
            findForm = new FindForm();
            findForm.Searcher = FindNext;
        }
        findForm.ShowFind(this);
    }

    // One Find Next: selects the next row that matches the search text and scrolls to it. Returns the text
    // the Find window shows. The list keeps growing while a scan runs, so every search looks at the rows there are right now.
    private string FindNext(string text)
    {
        string[] terms = UrlSearch.ParseTerms(text);
        string termsKey = string.Join(" ", terms);
        IReadOnlyList<URL> urls = collection.Collection;

        int current = urlDataGridView.CurrentCell != null ? urlDataGridView.CurrentCell.RowIndex : -1;
        // Repeating a search moves on from the row it just found; a new search, or a row picked by hand, starts at the selected row itself.
        bool repeated = current >= 0 && current == lastFindRow && string.Equals(termsKey, lastFindTerms, StringComparison.OrdinalIgnoreCase);
        int start = current < 0 ? 0 : (repeated ? current + 1 : current);

        bool wrapped;
        int found = UrlSearch.FindNext(urls, start, terms, out wrapped);
        if (found < 0 || found >= urlDataGridView.RowCount)
        {
            lastFindRow = -1;
            return "No matches found.";
        }

        SelectRow(found);
        lastFindRow = found;
        lastFindTerms = termsKey;
        return "Row " + (found + 1).ToString(CultureInfo.CurrentCulture) + " of " + urls.Count.ToString(CultureInfo.CurrentCulture)
            + (wrapped ? " (continued from the top)" : "");
    }

    // Makes the row the current, highlighted one; the list only scrolls when the row is not already fully in view.
    private void SelectRow(int index)
    {
        int first = urlDataGridView.FirstDisplayedScrollingRowIndex;
        if (first < 0 || index < first || index >= first + urlDataGridView.DisplayedRowCount(true))
            urlDataGridView.FirstDisplayedScrollingRowIndex = Math.Max(0, index - 2);

        urlDataGridView.CurrentCell = urlDataGridView.Rows[index].Cells[urlDataGridViewTextBoxColumn.Index];
    }

    private void toolStripButtonSettings_Click(object sender, EventArgs e)
    {
        SettingsForm frm = new SettingsForm();
        frm.ShowDialog(this);
        frm.Dispose();
    }

    private void toolStripButtonHighlight_Click(object sender, EventArgs e)
    {
        int top = 0;
        if (urlDataGridView.CurrentCell != null)
            top = urlDataGridView.CurrentCell.RowIndex;

        var scroll = collection.Collection.Select((v, i) => new { url = v, index = i }).FirstOrDefault(x => x.index > top && !x.url.HighlightColor.IsEmpty);
        if (scroll != null)
        {
            //urlDataGridView.ClearSelection();
            urlDataGridView.CurrentCell = urlDataGridView.Rows[scroll.index].Cells[0];
            if (scroll.index < 2)
                urlDataGridView.FirstDisplayedScrollingRowIndex = 0;
            else
                urlDataGridView.FirstDisplayedScrollingRowIndex = scroll.index - 2;
        }
        else
            MessageBox.Show("Can't find any more highlighted rows.");
    }

    private void toolStripButtonLoadURLList_Click(object sender, EventArgs e)
    {
        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            // Loading a list replaces whatever is in the grid, including a scan that is still running.
            StopCrawl();
            ResetCollection();

            string[] urls = File.ReadAllLines(openFileDialog.FileName);
            foreach (string url in urls)
            {
                collection.Add(url);
            }
            if (collection.Count() > 0)
                StartLoadedList();
        }
    }
}
