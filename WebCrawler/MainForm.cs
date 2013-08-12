using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace WebCrawler
{
	public partial class MainForm : Form
	{
		private bool crawling = false;
		private Hashtable urlLinksTo = new Hashtable();
		private Hashtable urlLinksFrom = new Hashtable();
		private static ChildThread[] childProcesses;
		private static int numProcesses = Settings.Instance.MaxProcesses;
		private URLCollection collection = new URLCollection();
		private BindingSource source = new BindingSource();

		public MainForm()
		{
			InitializeComponent();

			source.DataSource = collection.Collection;
			urlDataGridView.DataSource = source;
		}

		private void toolStripButtonPlay_Click(object sender, EventArgs e)
		{
			if (!crawling && urlTextBox.Text != "")
			{
				crawling = true;

				toolStripButtonPlay.Enabled = false;
				//toolStripButtonPause.Enabled = true;
				toolStripButtonStop.Enabled = true;
				checkButton.Enabled = false;

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
			if (crawling)
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
		}

		private void refreshTimer_Tick(object sender, EventArgs e)
		{
			if (crawling && collection.IsAllDone())
			{
				toolStripButtonStop_Click(this, null);
			}

			this.Text = "Number of URLs: " + collection.Count.ToString();
			if(collection.IsDirty)
			{
				collection.IsDirty = false;

				int rowIndex = urlDataGridView.FirstDisplayedScrollingRowIndex;
				source.ResetBindings(false);
				if (rowIndex != -1)
					urlDataGridView.FirstDisplayedScrollingRowIndex = rowIndex; 
			}
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

				if (url.Notes.StartsWith("Redirected"))
					e.Value = statusImageList.Images["redirect"];
				else if (url.Status == "External")
					e.Value = statusImageList.Images["external"];
				else if (url.Status == "Done")
					e.Value = statusImageList.Images["success"];
				else if(url.Status.ToString() == "Error")
					e.Value = statusImageList.Images["failure"];
			}
		}

		private void urlDataGridView_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
		{
			URL url = collection.Collection[e.RowIndex];
			if(!url.HighlightColor.IsEmpty)
				urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = url.HighlightColor;
			else
				switch(url.Status)
				{
					case "Error":
						urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Red;
						break;
					case "External":
						urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Blue;
						break;
					case "Done":
						urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Green;
						break;
					case "Skipped":
						urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Brown;
						break;
					default:
						urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Orange;
						break;
				}
		}

		private void toolStripButtonShowOnlyErrors_Click(object sender, EventArgs e)
		{
			// Filter to show only errors
			/*if (toolStripButtonShowOnlyErrors.Checked)
				urlDataSet.Urls.DefaultView.RowFilter = "Status = 'Error'";
			else
				urlDataSet.Urls.DefaultView.RowFilter = "";*/
		}

		private void Start()
		{
			childProcesses = new ChildThread[numProcesses];
			for (int i = 0; i < numProcesses; i++)
			{
				childProcesses[i] = new ChildThread(collection);
				childProcesses[i].Start();
				Thread.Sleep(100);
			}
		}

		private void Stop()
		{
			// stopping all children thread
			for (int i = 0; i < childProcesses.Length; i++)
				childProcesses[i].Stop();
		}

		private void urlDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
		{
			string url = urlDataGridView.Rows[e.RowIndex].Cells[4].Value.ToString();
			if (url != "")
			{
				InfoForm.Instance.ShowInfo(url, urlLinksFrom[url] as ArrayList, urlLinksTo[url] as ArrayList);
			}
		}

		private void urlTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter)
			{
				toolStripButtonPlay_Click(sender, e);
				e.Handled = true;
			}
		}
	}
}
