using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WebCrawler
{
	static class Program
	{
		[MTAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
		}
	}
}
