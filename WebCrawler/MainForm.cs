using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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
		private Hashtable urlLinksTo = new Hashtable();
		private Hashtable urlLinksFrom = new Hashtable();
		private static ChildThread[] childProcesses;
		private static int numProcesses;
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
			if(!crawling && urlTextBox.Text != "")
			{
				crawling = true;

				toolStripButtonPlay.Enabled = false;
				//toolStripButtonPause.Enabled = true;
				toolStripButtonStop.Enabled = true;
				checkButton.Enabled = false;

				if(collection.Collection.Count == 0)
					collection.Add(urlTextBox.Text);

				Start();
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
			if(crawling)
			{
				crawling = false;
				toolStripButtonPlay.Enabled = true;
				//toolStripButtonPause.Enabled = true;
				toolStripButtonStop.Enabled = false;

				Stop();
			}
		}

		private void MainForm_Load(object sender, EventArgs e)
		{
			if(!Settings.Instance.Load())
				Settings.Instance.Save();
			collection = new URLCollection(urlDataGridView);
			urlDataGridView.AutoGenerateColumns = false;
			urlDataGridView.DataSource = collection.Collection;
			numProcesses = Settings.Instance.MaxProcesses;
			
			ParseArguments(args);

			if (collection.Collection.Count > 0)
				toolStripButtonPlay_Click(this, null);
		}

		private void ParseArguments(string[] args)
		{
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

					default:
						MessageBox.Show("Unknown option: " + args[i]);
						return;
				}
				i++;
			}
		}

		private void refreshTimer_Tick(object sender, EventArgs e)
		{
			if(crawling)
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
					this.toolStripButtonStop_Click(this, null);
					if (!string.IsNullOrEmpty(this.reportName))
					{
						this.SaveReport();
						this.Close();
					}
				}
			}
		}

		private void SaveReport()
		{
			using (StreamWriter file = new StreamWriter(this.reportName))
			{
				file.WriteLine("URL\tTime Taken");
				foreach (URL url in collection.Collection)
				{
					file.WriteLine(url.Url.ToString() + "\t" + url.TimeTakenAll);
				}
				file.Close();
			}
		}

		private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if(crawling)
			{
				Stop();
			}
		}

		private void urlDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
		{
			if(urlDataGridView.Columns[e.ColumnIndex].Name.Equals("statusDataGridViewImageColumn"))
			{
				URL url = collection.Collection[e.RowIndex];

				if(url.Status == URLStatus.Redirected)
					e.Value = statusImageList.Images["redirect"];
				else if(url.Status == URLStatus.External)
					e.Value = statusImageList.Images["external"];
				else if(url.Status == URLStatus.Done)
					e.Value = statusImageList.Images["success"];
				else if(url.Status == URLStatus.Error)
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
			childProcesses = new ChildThread[numProcesses];
			for(int i = 0; i < numProcesses; i++)
			{
				childProcesses[i] = new ChildThread(i, collection);
				childProcesses[i].Start();
				Thread.Sleep(100);
			}
		}

		private void Stop()
		{
			for(int i = 0; i < childProcesses.Length; i++)
				childProcesses[i].Stop();
		}

		private void urlDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
		{
			if(e.RowIndex > -1)
			{
				URL url = collection.Collection[e.RowIndex];
				InfoForm.Instance.ShowInfo(url);
			}
		}

		private void urlTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if(e.KeyCode == Keys.Enter)
			{
				toolStripButtonPlay_Click(sender, e);
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
			if(urlDataGridView.CurrentCell != null)
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
	}
}