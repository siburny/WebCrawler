using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WebCrawler
{
	public sealed partial class InfoForm : Form
	{
		public readonly static InfoForm Instance = new InfoForm();

		private InfoForm()
		{
			InitializeComponent();
		}

		private void btnClose_Click(object sender, EventArgs e)
		{
			this.Hide();
		}

		public void ShowInfo(string url, ArrayList linkFrom, ArrayList linkTo)
		{
			txtLink.Text = url;

			txtLinkFrom.Text = "";
			if (linkFrom != null)
				txtLinkFrom.Text = String.Join(Environment.NewLine, (string[])linkFrom.ToArray(typeof(string)));

			txtLinkTo.Text = "";
			if (linkTo != null)
				txtLinkTo.Text = String.Join(Environment.NewLine, (string[])linkTo.ToArray(typeof(string)));

			this.ShowDialog();
		}
	}
}
