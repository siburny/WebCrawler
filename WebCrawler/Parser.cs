using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;

namespace WebCrawler
{
	public static class Parser
	{
		public static ArrayList Parse(string html)
		{
			// HTML Comments
			string commentPattern = "<!--.*?-->";
			html = Regex.Replace(html, commentPattern, "");

			// HREF links
			string hrefPattern = "(?:\\s)href\\s*=\\s*(?:\"(?<href>[^\"]*)\"|'(?<href>[^']*)'|(?<href>[^\"'>\\s]+))";
			Regex hrefRegex = new Regex(hrefPattern.ToString(), RegexOptions.IgnoreCase);

			Match hrefcheck = hrefRegex.Match(html);
			ArrayList linklist = new ArrayList();

			while (hrefcheck.Success)
			{
				if (!hrefcheck.Groups["href"].Value.Contains("'") && !hrefcheck.Groups["href"].Value.Contains("\"") && !hrefcheck.Groups["href"].Value.Contains("\n"))
					linklist.Add(hrefcheck.Groups["href"].Value);
				hrefcheck = hrefcheck.NextMatch();
			}

			//SRC links
			hrefPattern = "(?:\\s)src\\s*=\\s*(?:\"(?<src>[^\"]*)\"|'(?<src>[^']*)'|(?<src>[^\"'>\\s]+))";
			hrefRegex = new Regex(hrefPattern.ToString(), RegexOptions.IgnoreCase);

			hrefcheck = hrefRegex.Match(html);

			while (hrefcheck.Success)
			{
				if (!hrefcheck.Groups["src"].Value.Contains("'") && !hrefcheck.Groups["src"].Value.Contains("\"") && !hrefcheck.Groups["src"].Value.Contains("\n"))
					linklist.Add(hrefcheck.Groups["src"].Value);
				hrefcheck = hrefcheck.NextMatch();
			}

			return linklist;
		}
	}
}
