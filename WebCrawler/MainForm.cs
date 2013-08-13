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

namespace WebCrawler
{
	public partial class MainForm : Form
	{
		private bool crawling = false;
		private Hashtable urlLinksTo = new Hashtable();
		private Hashtable urlLinksFrom = new Hashtable();
		private static ChildThread[] childProcesses;
		private static int numProcesses = Settings.Instance.MaxProcesses;
		private URLCollection collection;

		public MainForm()
		{
			InitializeComponent();

			collection = new URLCollection(urlDataGridView);
			urlDataGridView.DataSource = collection.Collection;
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
		}

		private void refreshTimer_Tick(object sender, EventArgs e)
		{
			if(crawling)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("URLs: " + collection.Count.ToString());
				sb.Append(" | ");
				sb.Append("Processed: " + collection.Collection.Count(x => x.Status >= URLStatus.Done));
				sb.Append(" | ");
				sb.Append("Redirected: " + collection.Collection.Count(x => x.Status == URLStatus.Redirected));
				sb.Append(" | ");
				sb.Append("Warnings: " + collection.Collection.Count(x => x.Status == URLStatus.Warning));
				sb.Append(" | ");
				sb.Append("Errors: " + collection.Collection.Count(x => x.Status == URLStatus.Error));
				toolStripStatusLabelTotalURLs.Text = sb.ToString();
				if(collection.IsAllDone())
					toolStripButtonStop_Click(this, null);
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
				MessageBox.Show("Can't find any more errors");
		}
	}
}