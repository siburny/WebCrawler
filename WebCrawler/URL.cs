using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace WebCrawler
{
	public class URL
	{
		//public int Id;
		public URLStatus Status { get; set; }
		public string Url { get; set; }
		public string MimeType { get; set; }
		public long ContentLength { get; set; }
		public double TimeTaken { get; set; }
		public string Notes { get; set; }
		public int Depth { get; set; }
		public Color HighlightColor { get; set; }
		public List<string> LinksFrom = new List<string>();
		public List<string> LinksTo = new List<string>();

		public URL(string url)
			: this(url, 0, URLStatus.Default)
		{
		}

		public URL(string url, int depth)
			: this(url, depth, URLStatus.Default)
		{
		}

		public URL(string url, int depth, URLStatus status)
		{
			Status = status;
			Url = url;
			MimeType = "";
			ContentLength = 0;
			TimeTaken = 0;
			Notes = "";
			Depth = depth;
		}
	}

	public enum URLStatus
	{
		Default = 0,
		Downloading = 1,
		Parsing = 2,
		Done = 10,
		Warning = 20,
		Error = 30,
		External = 40,
		Skipped = 100
	}
}
