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
		private int currentDepth;
		private Uri startingUri;
		private URLCollection collection;

		public bool isRunning
		{
			get
			{
				return childThread != null && childThread.ThreadState == ThreadState.Running;
			}
		}

		public ChildThread(URLCollection _collection)
		{
			collection = _collection;
			startingUri = new Uri(collection.GetStatingURL());
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

		public void ChildThreadProcess()
		{
			Uri urlCheck;
			string buffer;

			while (true)
			{
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
					WebRequest request = WebRequest.Create(urlCheck);
					request.Timeout = 15000;

					WebResponse response;
					response = request.GetResponse();

					url.MimeType = response.ContentType;
					url.ContentLength = response.ContentLength;

					if(response.ResponseUri.ToString() != urlCheck.ToString())
					{
						url.Notes = "Redirected to " + response.ResponseUri.ToString();
						if(startingUri.IsBaseOf(response.ResponseUri))
							collection.Add(response.ResponseUri.ToString(), currentDepth + 1, "Redirected");
						else
							collection.Add(response.ResponseUri.ToString(), currentDepth + 1, "External");
					}
					else if(Filter.IsExcluded(response.ResponseUri.ToString()))
					{
						collection.Add(response.ResponseUri.ToString(), currentDepth + 1, "Skipped");
					}
					else if(Filter.IsMimeSupported(response.ContentType) && currentDepth < maximumDepth)
					{
						StreamReader readStream = new StreamReader(response.GetResponseStream());
						buffer = readStream.ReadToEnd();
						readStream.Close();

						url.Status = "Parsing";
						collection.IsDirty = true;

						ArrayList links = Parser.Parse(buffer);

						//add new links
						for(int i = 0; i < links.Count; i++)
						{
							string tempurl = HttpUtility.HtmlDecode(links[i].ToString());

							if(!Filter.IsValid(tempurl))
								continue;

							Uri temp = new Uri(tempurl, UriKind.RelativeOrAbsolute);

							if(temp.IsAbsoluteUri)
							{
								if(startingUri.IsBaseOf(temp))
									collection.Add(tempurl, currentDepth + 1);
								else
									collection.Add(tempurl, currentDepth + 1, "External");
							}
							else
							{
								Uri.TryCreate(startingUri, temp, out temp);

								if(temp != null && startingUri.IsBaseOf(temp))
									collection.Add(temp.AbsoluteUri.ToString(), currentDepth + 1);
								else
									collection.Add(tempurl, currentDepth + 1, "Error");
							}
						}
					}

					// close connection
					response.Close();
				}
				catch(WebException e)
				{
					url.Status = "Error";
					url.Notes = e.Message + Environment.NewLine + "-----------------------------------------------------------" + buffer;
					collection.IsDirty = true;

					Thread.Sleep(100);
					continue;
				}
				catch(Exception e)
				{
					url.Status = "Error";
					url.Notes = e.Message + Environment.NewLine + "-----------------------------------------------------------" + buffer;
					collection.IsDirty = true;

					Thread.Sleep(100);
					continue;
				}

				//Update UI
				url.Status = "Done";
				collection.IsDirty = true;

				Application.DoEvents();
				//Thread.Sleep(5000);
			}
		}
	}

	static class Filter
	{
		public static bool IsExcluded(string url)
		{
			for (int i = 0; i < Settings.Instance.excludeRules.Count; i++)
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

			/*Uri uri;
			if(!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri))
				return false;*/

			return true;
		}
	}
}
