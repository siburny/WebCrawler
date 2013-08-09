﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebCrawler
{
	public class URL
	{
		//public int Id;
		public string Status { get; set; }
		public string Url { get; set; }
		public string MimeType { get; set; }
		public long ContentLength { get; set; }
		public string Notes { get; set; }
		public int Depth { get; set; }

		public URL(string url) : this(url, 0)
		{
		}

		public URL(string url, int depth)
		{
			Status = "";
			Url = url;
			MimeType = "";
			ContentLength = 0;
			Notes = "";
			Depth = depth;
		}
	}
}
