using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WebCrawler
{
	public partial class SettingsForm : Form
	{
		public SettingsForm()
		{
			InitializeComponent();
		}

		private void buttonClose_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void SettingsForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (comboBoxDepth.SelectedIndex == -1 || comboBoxThreads.SelectedIndex == -1 || !Validate(textBoxHighlight.Text))
				e.Cancel = true;
			
			Settings.Instance.Save();
		}

		private bool Validate(string text)
		{
			Settings.Instance.HighlightRules.Clear();

			string[] lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string line in lines)
			{
				string[] parts = line.Split(new string[] { "=>" }, StringSplitOptions.None);
				if (parts.Length != 2)
				{
					MessageBox.Show("Make sure format of Highlight rules is valid.");
					return false;
				}
				try
				{
					Convert.ToInt32(parts[1].Trim(), 16);
				}
				catch (Exception)
				{
					MessageBox.Show("Make sure format of Highlight rules is valid.");
					return false;
				}
				Settings.Instance.HighlightRules.Add(new SettingsHighlightRule() { Expression = parts[0].Trim(), HighlightColor = parts[1].Trim() });
			}
			return true;
		}

		private void SettingsForm_Load(object sender, EventArgs e)
		{
			comboBoxThreads.SelectedIndex = comboBoxThreads.FindStringExact(Settings.Instance.MaxProcesses.ToString());
			comboBoxDepth.SelectedIndex = comboBoxDepth.FindStringExact(Settings.Instance.MaxDepth.ToString());
			textBoxHighlight.Text = String.Join(Environment.NewLine, Settings.Instance.HighlightRules.Select(x => x.Expression + " => " + x.HighlightColor));
		}
	}
}
