using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Linq;
using System.IO;

namespace WebCrawler
{
    public partial class MainForm : Form
    {
        private bool crawling = false;
        private static ChildThread[] childProcesses;
        private URLCollection collection;
        private string[] args = null;
        private string reportName = "";

        public MainForm() : this(null)
        {
        }

        public MainForm(string[] _args)
        {
            InitializeComponent();
            args = _args;
        }

        private void toolStripButtonPlay_Click(object sender, EventArgs e)
        {
            if (crawling)
                return;

            // Play resumes the scan that is already in the list (e.g. after Stop), whatever the URL box says.
            // With nothing in the list yet there is nothing to resume, so it starts from the URL box.
            if (collection.Collection.Count == 0)
            {
                StartNewScan();
                return;
            }

            if (collection.GetStartingUri() == null)
            {
                MessageBox.Show("There is no valid URL to crawl. Please use absolute URLs starting with http:// or https://");
                return;
            }

            BeginCrawl();
        }

        // "Check Website": throw away all results and scan the address in the URL box from scratch.
        private void checkButton_Click(object sender, EventArgs e)
        {
            if (crawling)
                return;

            StartNewScan();
        }

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

            if (collection.Collection.Count > 0)
            {
                // A new collection (instead of Clear) so threads still finishing the previous scan
                // cannot add their results to the new one.
                collection = new URLCollection(urlDataGridView);
                urlDataGridView.DataSource = collection.Collection;
            }
            collection.Add(text);

            BeginCrawl();
        }

        private void BeginCrawl()
        {
            crawling = true;

            toolStripButtonPlay.Enabled = false;
            //toolStripButtonPause.Enabled = true;
            toolStripButtonStop.Enabled = true;
            checkButton.Enabled = false;

            try
            {
                Start();
            }
            catch
            {
                // Don't leave the UI stuck in the "crawling" state if the threads could not be started.
                crawling = false;
                toolStripButtonPlay.Enabled = true;
                toolStripButtonStop.Enabled = false;
                checkButton.Enabled = true;
                throw;
            }
        }

        private void toolStripButtonPause_Click(object sender, EventArgs e)
        {
            //toolStripButtonPlay.Enabled = true;
            //toolStripButtonPause.Enabled = false;
            //toolStripButtonStop.Enabled = true;
        }

        private void toolStripButtonStop_Click(object sender, EventArgs e)
        {
            if (crawling)
            {
                crawling = false;
                toolStripButtonPlay.Enabled = true;
                //toolStripButtonPause.Enabled = true;
                toolStripButtonStop.Enabled = false;
                checkButton.Enabled = true;

                Stop();
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (!Settings.Instance.Load())
                Settings.Instance.Save();
            collection = new URLCollection(urlDataGridView);
            urlDataGridView.AutoGenerateColumns = false;
            urlDataGridView.DataSource = collection.Collection;

            ParseArguments(args);

            if (collection.Collection.Count > 0)
                toolStripButtonPlay_Click(this, null);
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
            if (crawling)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("URLs: " + collection.Count().ToString());
                sb.Append(" | ");
                sb.Append("Processed: " + collection.Count(x => x.Status >= URLStatus.Done));
                sb.Append(" | ");
                sb.Append("Redirected: " + collection.Count(x => x.Status == URLStatus.Redirected));
                sb.Append(" | ");
                sb.Append("Warnings: " + collection.Count(x => x.Status == URLStatus.Warning));
                sb.Append(" | ");
                sb.Append("Errors: " + collection.Count(x => x.Status == URLStatus.Error));
                toolStripStatusLabelTotalURLs.Text = sb.ToString();

                if (collection.IsAllDone())
                {
                    toolStripButtonStop_Click(this, null);
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
            if (console)
            {
                Console.WriteLine("URL".PadRight(100, ' ') + "Status".PadRight(10, ' ') + "HTTP".PadRight(6, ' ') + "Time Taken");
                foreach (URL url in collection.Collection)
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
                int measurements = Math.Max(1, collection.Collection.Select(x => x.TimeTakenAttempts).DefaultIfEmpty(0).Max());

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

                    foreach (URL url in collection.Collection)
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
            if (urlDataGridView.Columns[e.ColumnIndex].Name.Equals("statusDataGridViewImageColumn"))
            {
                URL url = collection.Collection[e.RowIndex];

                if (url.Status == URLStatus.Redirected)
                    e.Value = statusImageList.Images["redirect"];
                else if (url.Status == URLStatus.External)
                    e.Value = statusImageList.Images["external"];
                else if (url.Status == URLStatus.Done)
                    e.Value = statusImageList.Images["success"];
                else if (url.Status == URLStatus.Error)
                    e.Value = statusImageList.Images["failure"];
            }
        }

        private void urlDataGridView_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
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
            // Read the setting on every start so a change made in the Settings dialog applies to the next crawl.
            int numProcesses = Math.Max(1, Settings.Instance.MaxProcesses);
            childProcesses = new ChildThread[numProcesses];
            for (int i = 0; i < numProcesses; i++)
            {
                childProcesses[i] = new ChildThread(i, collection);
                childProcesses[i].Start();
                Thread.Sleep(100);
            }
        }

        private void Stop()
        {
            for (int i = 0; i < childProcesses.Length; i++)
                childProcesses[i].Stop();
        }

        private void urlDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex > -1)
            {
                URL url = collection.Collection[e.RowIndex];
                InfoForm.Instance.ShowInfo(url);
            }
        }

        private void urlTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Enter in the URL box behaves like the "Check Website" button.
                checkButton_Click(sender, e);
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
                collection.Clear();

                string[] urls = File.ReadAllLines(openFileDialog.FileName);
                foreach (string url in urls)
                {
                    collection.Add(url);
                }
                if (collection.Collection.Count > 0)
                    toolStripButtonPlay_Click(this, null);
            }
        }
    }
}