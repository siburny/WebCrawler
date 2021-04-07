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
			Hide();
		}

		public void ShowInfo(URL url)
		{
			txtLink.Text = url.Url;

			txtLinkFrom.Text = "";
			txtLinkFrom.Text = String.Join(Environment.NewLine, url.LinksFrom);

			txtLinkTo.Text = "";
			txtLinkTo.Text = String.Join(Environment.NewLine, url.LinksTo);

			ShowDialog();
		}
	}
}
