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

namespace WebCrawler
{
	class ChildThread
	{
		private const int maximumDepth = 5;
		private Thread childThread;
		private int urlRowNumber;
		private int currentDepth;
		private Uri startingUri;
		private MainForm frm;

		private Hashtable urlLinksTo;
		private Hashtable urlLinksFrom;

		public bool isRunning
		{
			get
			{
				return childThread != null && childThread.ThreadState == ThreadState.Running;
			}
		}

		public ChildThread(MainForm _frm, Hashtable _urlLinksTo, Hashtable _urlLinksFrom)
		{
			urlLinksTo = _urlLinksTo;
			urlLinksFrom = _urlLinksFrom;
			frm = _frm;
			startingUri = new Uri(frm.GetData(0, 2));
			this.childThread = new Thread(new ThreadStart(ChildThreadProcess));
			this.childThread.Name = "Child Process";
		}

		public void Start()
		{
			this.childThread.Start();
		}

		public void Stop()
		{
			//if (this.childThread.ThreadState == ThreadState.Running)
			this.childThread.Abort();
		}

		private void AddUrlSkipped(string url)
		{
			if (frm.FindData(2, url) == -1)
			{
				frm.AddDataSkipped(url, currentDepth + 1);

				if (!urlLinksTo.ContainsKey(frm.GetData(urlRowNumber, 2)))
					urlLinksTo[frm.GetData(urlRowNumber, 2)] = new ArrayList();
				(urlLinksTo[frm.GetData(urlRowNumber, 2)] as ArrayList).Add(url);

				if (!urlLinksFrom.ContainsKey(url))
					urlLinksFrom[url] = new ArrayList();
				(urlLinksFrom[url] as ArrayList).Add(frm.GetData(urlRowNumber, 2));
			}
		}

		private void AddUrl(string url, bool external)
		{
			AddUrl(url, external, true);
		}

		private void AddUrl(string url, bool external, bool godeeper)
		{
			if (frm.FindData(2, url) == -1)
			{
				frm.AddData(url, external, currentDepth + (godeeper ? 1 : 0));
			}

			if (!urlLinksTo.ContainsKey(frm.GetData(urlRowNumber, 2)))
				urlLinksTo[frm.GetData(urlRowNumber, 2)] = new ArrayList();
			(urlLinksTo[frm.GetData(urlRowNumber, 2)] as ArrayList).Add(url);

			if (!urlLinksFrom.ContainsKey(url))
				urlLinksFrom[url] = new ArrayList();
			(urlLinksFrom[url] as ArrayList).Add(frm.GetData(urlRowNumber, 2));

		}

		public void ChildThreadProcess()
		{
			Uri urlCheck;
			string buffer;

			while (true)
			{
				// Reset vars
				buffer = "";
				currentDepth = -1;
				urlRowNumber = frm.FindDataAndSet(1, "", "Downloading");

				//System.Diagnostics.Trace.WriteLine("Found: " + urlRowNumber.ToString());

				// if nothing to check, wait
				if (urlRowNumber == -1)
				{
					Thread.Sleep(1000);
					continue;
				}

				urlCheck = new Uri(frm.GetData(urlRowNumber, 2));

				// set current depth
				currentDepth = Int32.Parse(frm.GetData(urlRowNumber, 6));

				try
				{
					WebRequest request = WebRequest.Create(urlCheck);
					request.Timeout = 15000;

					WebResponse response;
					response = request.GetResponse();

					frm.ChangeData(urlRowNumber, 3, response.ContentType);
					frm.ChangeData(urlRowNumber, 4, response.ContentLength);

					if (response.ResponseUri.ToString() != urlCheck.ToString())
					{
						frm.ChangeData(urlRowNumber, 1, "Redirected to " + response.ResponseUri.ToString());
						if (startingUri.IsBaseOf(response.ResponseUri))
							AddUrl(response.ResponseUri.ToString(), false, false);
						else
							AddUrl(response.ResponseUri.ToString(), true, false);
					}
					else if (Filter.IsExcluded(response.ResponseUri.ToString()))
					{
						frm.AddDataSkipped(response.ResponseUri.ToString(), currentDepth + 1);
					}
					else if (Filter.IsMimeSupported(response.ContentType) && currentDepth < maximumDepth)
					{
						StreamReader readStream = new StreamReader(response.GetResponseStream());
						buffer = readStream.ReadToEnd();
						readStream.Close();

						frm.ChangeData(urlRowNumber, 1, "Parsing");

						ArrayList links = Parser.Parse(buffer);

						//add new links
						for (int i = 0; i < links.Count; i++)
						{
							string tempurl = HttpUtility.HtmlDecode(links[i].ToString());

							//System.Diagnostics.Trace.WriteLineIf(currentDepth == 0, tempurl);

							if (!Filter.IsValid(tempurl))
								continue;

							Uri temp = new Uri(tempurl, UriKind.RelativeOrAbsolute);

							if (temp.IsAbsoluteUri)
							{
								if (startingUri.IsBaseOf(temp))
									AddUrl(tempurl, false);
								else
									AddUrl(tempurl, true);
							}
							else
							{
								Uri.TryCreate(startingUri, temp, out temp);

								if (temp != null && startingUri.IsBaseOf(temp))
								{
									AddUrl(temp.AbsoluteUri.ToString(), false);
								}
							}
						}
					}

					// close connection
					response.Close();
				}
				catch (WebException e)
				{
					frm.ChangeData(urlRowNumber, 1, "Error");
					frm.ChangeData(urlRowNumber, 5, e.Message + Environment.NewLine + "-----------------------------------------------------------" + buffer);

					Thread.Sleep(100);
					continue;
				}
				catch (Exception e)
				{
					frm.ChangeData(urlRowNumber, 1, "Error");
					frm.ChangeData(urlRowNumber, 5, e.Message + Environment.NewLine + "-----------------------------------------------------------" + buffer);

					Thread.Sleep(100);
					continue;
				}

				//Update UI
				frm.ChangeData(urlRowNumber, 1, "Done");

				Application.DoEvents();
				//Thread.Sleep(5000);
			}
		}
	}

	static class Filter
	{
		public static bool IsExcluded(string url)
		{
			for (int i = 0; i < Settings.Instance.excludeRules.Length; i++)
			{
				if (url.StartsWith(Settings.Instance.excludeRules[i]))
					return true;
			}
			return false;
		}

		public static bool IsMimeSupported(string mime)
		{
			if (mime.StartsWith("text/"))
				return true;
			else if (mime == "application/x-javascript")
				return true;
			else
				return false;
		}

		internal static bool IsValid(string url)
		{
			if (url.StartsWith("#"))
				return false;
			else if (url.ToLower().StartsWith("javascript:") || url.ToLower().StartsWith("mailto:"))
				return false;

			return true;
		}
	}
}
