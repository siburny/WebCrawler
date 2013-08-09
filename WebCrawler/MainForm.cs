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
		private static int numProcesses = 5;

		public MainForm()
		{
			InitializeComponent();
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

				urlDataSet.Urls.Rows.Add(new object[] { null, "", urlTextBox.Text });

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

		private bool IsDone()
		{
			Monitor.Enter(urlDataSet.Urls);
			for (int i = 0; i < urlDataSet.Urls.Rows.Count; i++)
			{
				if (urlDataSet.Urls.Rows[i][1].ToString() != "Done" && urlDataSet.Urls.Rows[i][1].ToString() != "Skipped" && urlDataSet.Urls.Rows[i][1].ToString() != "Error" && urlDataSet.Urls.Rows[i][1].ToString() != "Excluded")
				{
					Monitor.Exit(urlDataSet.Urls);
					return false;
				}
			}
			Monitor.Exit(urlDataSet.Urls);

			return true;
		}
		
		private void refreshTimer_Tick(object sender, EventArgs e)
		{
			if (crawling && IsDone())
			{
				toolStripButtonStop_Click(this, null);
			}

			this.Text = "Number of URLs: " + urlDataSet.Urls.Rows.Count.ToString();
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
				if (urlDataGridView.Rows[e.RowIndex].Cells[7].Value != null && urlDataGridView.Rows[e.RowIndex].Cells[7].Value.ToString().StartsWith("Redirected"))
					e.Value = statusImageList.Images["redirect"];
				else if (urlDataGridView.Rows[e.RowIndex].Cells[7].Value != null && urlDataGridView.Rows[e.RowIndex].Cells[7].Value.ToString().StartsWith("External"))
					e.Value = statusImageList.Images["external"];
				else if (urlDataGridView.Rows[e.RowIndex].Cells[5].Value != null && urlDataGridView.Rows[e.RowIndex].Cells[6].Value != null && urlDataGridView.Rows[e.RowIndex].Cells[5].Value.ToString() != "" && urlDataGridView.Rows[e.RowIndex].Cells[6].Value.ToString() != "0")
					e.Value = statusImageList.Images["success"];
				else if (urlDataGridView.Rows[e.RowIndex].Cells[2].Value != null && urlDataGridView.Rows[e.RowIndex].Cells[2].Value.ToString() == "Error")
					e.Value = statusImageList.Images["failure"];
			}
		}

		private void urlDataGridView_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
		{
			if (urlDataGridView.Rows[e.RowIndex].Cells[2].Value != null)
			{
				switch (urlDataGridView.Rows[e.RowIndex].Cells[2].Value.ToString())
				{
					case "":
						urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Orange;
						break;
					case "Error":
						urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Red;
						break;
					case "Skipped":
						urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Blue;
						break;
					case "Done":
						urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Green;
						break;
					case "Excluded":
						urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Brown;
						break;
					default:
						urlDataGridView.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Green;
						break;
				}
			}
		}

		private void toolStripButtonShowOnlyErrors_Click(object sender, EventArgs e)
		{
			// Filter to show only errors
			if (toolStripButtonShowOnlyErrors.Checked)
				urlDataSet.Urls.DefaultView.RowFilter = "Status = 'Error'";
			else
				urlDataSet.Urls.DefaultView.RowFilter = "";
		}

		private void Start()
		{
			childProcesses = new ChildThread[numProcesses];
			for (int i = 0; i < numProcesses; i++)
			{
				childProcesses[i] = new ChildThread(this, urlLinksTo, urlLinksFrom);
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

		private delegate void AddDataSkippedCallback(string url, int depth);
		public void AddDataSkipped(string url, int depth)
		{
			if (InvokeRequired)
			{
				this.Invoke(new AddDataSkippedCallback(AddDataSkipped), url, depth);
			}
			else
			{
				Monitor.Enter(urlDataSet.Urls);
				urlDataSet.Urls.Rows.Add(new object[] { null, "Excluded", url, "", 0, "", depth });
				Monitor.Exit(urlDataSet.Urls);
			}
		}

		private delegate void AddDataCallback(string url, bool external, int depth);
		public void AddData(string url, bool external, int depth)
		{
			if (InvokeRequired)
			{
				this.Invoke(new AddDataCallback(AddData), url, external, depth);
			}
			else
			{
				Monitor.Enter(urlDataSet.Urls);
				urlDataSet.Urls.Rows.Add(new object[] { null, external ? "Skipped" : "", url, "", 0, external ? "External" : "", depth });
				Monitor.Exit(urlDataSet.Urls);
			}
		}

		private delegate void ChangeDataCallback(int row, int column, object data);
		public void ChangeData(int row, int column, object data)
		{
			try
			{
				if (InvokeRequired)
				{
					this.Invoke(new ChangeDataCallback(ChangeData), row, column, data);
				}
				else
				{
					Monitor.Enter(urlDataSet.Urls);
					if (urlDataSet.Urls != null && urlDataSet.Urls.Rows[row] != null)
						urlDataSet.Urls.Rows[row][column] = data;
					Monitor.Exit(urlDataSet.Urls);
				}
			}
			catch (ObjectDisposedException)
			{
			}
		}

		public string GetData(int row, int column)
		{
			return urlDataSet.Urls.Rows[row][column].ToString();
		}

		public int FindData(int col, string data)
		{
			Monitor.Enter(urlDataSet.Urls);
			for (int i = 0; i < urlDataSet.Urls.Rows.Count; i++)
			{
				if (urlDataSet.Urls.Rows[i][col].ToString() == data)
				{
					Monitor.Exit(urlDataSet.Urls);
					return i;
				}
			}

			Monitor.Exit(urlDataSet.Urls);
			return -1;
		}

		public int FindDataAndSet(int col, string data, string data2)
		{
			Monitor.Enter(urlDataSet.Urls);
			for (int i = 0; i < urlDataSet.Urls.Rows.Count; i++)
			{
				if (urlDataSet.Urls.Rows[i][col].ToString() == data)
				{
					urlDataSet.Urls.Rows[i][col] = data2;
					Monitor.Exit(urlDataSet.Urls);
					return i;
				}
			}

			Monitor.Exit(urlDataSet.Urls);
			return -1;
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
