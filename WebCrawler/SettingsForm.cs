using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WebCrawler;

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
        // Everything is validated first; the shared settings are only touched once all input is known to be good.
        List<SettingsHighlightRule> highlightRules;
        if (comboBoxDepth.SelectedIndex == -1 || comboBoxThreads.SelectedIndex == -1 || !TryParseHighlightRules(textBoxHighlight.Text, out highlightRules))
        {
            e.Cancel = true;
            return;
        }

        Settings.Instance.MaxDepth = int.Parse(comboBoxDepth.SelectedItem.ToString());
        Settings.Instance.MaxProcesses = int.Parse(comboBoxThreads.SelectedItem.ToString());
        // Whole new lists are assigned (never modified in place), so a crawl that is running keeps seeing a consistent list.
        Settings.Instance.ExcludeRules = textBoxIgnore.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
        Settings.Instance.HighlightRules = highlightRules;

        Settings.Instance.Save();
		}

		// Parses the highlight rules text into a new list; the current settings are left alone (also when the text is invalid).
		private bool TryParseHighlightRules(string text, out List<SettingsHighlightRule> rules)
		{
			rules = new List<SettingsHighlightRule>();

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
				try
				{
					rules.Add(new SettingsHighlightRule() { Expression = parts[0].Trim(), HighlightColor = parts[1].Trim() });
				}
				catch (ArgumentException)
				{
					// The expression part is not a valid regular expression.
					MessageBox.Show("Highlight rule is not a valid regular expression:" + Environment.NewLine + parts[0].Trim());
					return false;
				}
			}
			return true;
		}

		private void SettingsForm_Load(object sender, EventArgs e)
		{
			comboBoxThreads.SelectedIndex = comboBoxThreads.FindStringExact(Settings.Instance.MaxProcesses.ToString());
			comboBoxDepth.SelectedIndex = comboBoxDepth.FindStringExact(Settings.Instance.MaxDepth.ToString());
			textBoxHighlight.Text = String.Join(Environment.NewLine, Settings.Instance.HighlightRules.Select(x => x.Expression + " => " + x.HighlightColor));
        textBoxIgnore.Text = String.Join(Environment.NewLine, Settings.Instance.ExcludeRules);
		}
}
