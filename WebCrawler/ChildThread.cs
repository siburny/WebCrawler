using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Web;
using CsQuery;
using System.Linq;

namespace WebCrawler
{
	class ChildThread
	{
		private int maximumDepth = Settings.Instance.MaxDepth;
		private Thread childThread;
		private int currentDepth;
		private Uri startingUri;
		private URLCollection collection;
		private bool stopping = false;
		private int index;

		public bool isRunning
		{
			get
			{
				return childThread != null && childThread.ThreadState == ThreadState.Running;
			}
		}

		public ChildThread(int _index, URLCollection _collection)
		{
			collection = _collection;
			index = _index;
			startingUri = new Uri(collection.GetStatingURL());
			this.childThread = new Thread(new ThreadStart(ChildThreadProcess));
			this.childThread.Name = "Child Process #" + (index + 1).ToString();
		}

		public void Start()
		{
			this.childThread.Start();
		}

		public void Stop()
		{
			//if (this.childThread.ThreadState == ThreadState.Running)
			this.stopping = true;
			//ThreadPool.QueueUserWorkItem(new WaitCallback(delegate { this.childThread.Join(15000); }));
		}

		public void ChildThreadProcess()
		{
			Uri urlCheck;
			string buffer;

			while (true)
			{
				if (this.stopping)
					break;

				// Reset vars
				buffer = "";
				currentDepth = -1;
				URL url = collection.GetNext();

				// if nothing to check, wait
				if (url == null)
				{
					Thread.Sleep(1000);
					continue;
				}

				urlCheck = new Uri(url.Url);
				currentDepth = url.Depth;

				try
				{
					HttpWebRequest request = HttpWebRequest.Create(urlCheck) as HttpWebRequest;
					request.AllowAutoRedirect = true;
					request.Timeout = 60000;

					DateTime start = DateTime.Now;

					WebResponse response = request.GetResponse();
					url.MimeType = response.ContentType.Split(new char[] { ';' })[0].Trim();
					url.ContentLength = response.ContentLength;
					StreamReader readStream = new StreamReader(response.GetResponseStream());
					buffer = readStream.ReadToEnd();
					readStream.Close();

					url.TimeTaken = (DateTime.Now - start).TotalMilliseconds;
					foreach(SettingsHighlightRule rule in Settings.Instance.HighlightRules)
						if (rule.IsMatch(buffer))
						{
							url.HighlightColor = rule.GetHighlightColor();
							url.Notes = "Highlight rule matched";
						}

					if(response.ResponseUri.ToString() != urlCheck.ToString())
					{
						url.Notes = "Redirected to " + response.ResponseUri.ToString();
						url.Status = URLStatus.Redirected;
						if(startingUri.IsBaseOf(response.ResponseUri))
							collection.Add(response.ResponseUri.ToString(), currentDepth, url.Url);
						else
							collection.Add(response.ResponseUri.ToString(), currentDepth, url.Url, URLStatus.External);
					}
					else if(Filter.IsExcluded(response.ResponseUri.ToString()))
					{
						collection.Add(response.ResponseUri.ToString(), currentDepth + 1, url.Url, URLStatus.Skipped);
					}
					else if(Filter.IsMimeSupported(response.ContentType) && currentDepth < maximumDepth)
					{
						url.Status = URLStatus.Parsing;

						List<IDomObject> links = CQ.Create(buffer)["a"].ToList();
						foreach(IDomObject link in links)
						{
							if (link.Attributes["href"] == null)
								continue;

							string tempurl = HttpUtility.HtmlDecode(link.Attributes["href"].ToString());
							if(!Filter.IsValid(tempurl))
								continue;

							Uri temp = new Uri(tempurl, UriKind.RelativeOrAbsolute);
							if(temp.IsAbsoluteUri)
							{
								if (startingUri.IsBaseOf(temp))
									collection.Add(tempurl, currentDepth + 1, url.Url);
								else
									collection.Add(tempurl, currentDepth + 1, url.Url, URLStatus.External);
							}
							else
							{
								Uri.TryCreate(startingUri, temp, out temp);

								if(temp != null && startingUri.IsBaseOf(temp))
									collection.Add(temp.AbsoluteUri.ToString(), currentDepth + 1, url.Url);
								else
									collection.Add(tempurl, currentDepth + 1, url.Url, URLStatus.Error);
							}
							url.LinksTo.Add(temp.AbsoluteUri);
						}
					}

					response.Close();
				}
				catch(WebException e)
				{
					url.Status = URLStatus.Error;
					url.Notes = e.Message + Environment.NewLine + "-----------------------------------------------------------" + buffer;

					Thread.Sleep(250);
					continue;
				}
				catch(Exception e)
				{
					url.Status = URLStatus.Error;
					url.Notes = e.Message + Environment.NewLine + "-----------------------------------------------------------" + buffer;

					Thread.Sleep(250);
					continue;
				}

				if (!(url.Status == URLStatus.Error || url.Status == URLStatus.External || url.Status == URLStatus.Redirected || url.Status == URLStatus.Skipped || url.Status == URLStatus.Warning))
				{
					if (url.TimeTakenAttempts >= 5)
						url.Status = URLStatus.Done;
					else
						url.Status = URLStatus.Default;
				}
				Application.DoEvents();
				Thread.Sleep(250);
			}
		}
	}

	static class Filter
	{
		public static bool IsExcluded(string url)
		{
			for (int i = 0; i < Settings.Instance.ExcludeRules.Count; i++)
			{
				if (url.StartsWith(Settings.Instance.ExcludeRules[i]))
					return true;
			}
			return false;
		}

		public static bool IsMimeSupported(string mime)
		{
			if (mime.StartsWith("text/"))
				return true;
			else if (mime == "application/x-javascript")
				return false;
			else
				return false;
		}

		internal static bool IsValid(string url)
		{
			if (url.StartsWith("#"))
				return false;
			else if (url.ToLower().StartsWith("javascript:") || url.ToLower().StartsWith("mailto:"))
				return false;

			/*Uri uri;
			if(!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri))
				return false;*/

			return true;
		}
	}
}
