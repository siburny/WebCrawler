using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace WebCrawler
{
	public class URLCollection
	{
		private List<URL> _collection = new List<URL>();
		private AutoResetEvent _event = new AutoResetEvent(true);

		public bool IsDirty = false;

		public URL Add(string url)
		{
			return Add(url, 0);
		}

		public URL Add(string url, int depth)
		{
			return Add(url, depth, "");
		}

		public URL Add(string url, int depth, string status)
		{
			_event.WaitOne();

			URL temp;
			if(!_collection.Exists(x => x.Url == url))
			{
				temp = new URL(url, depth, status);
				_collection.Add(temp);
				IsDirty = true;
			}
			else
			{
				temp = _collection.FirstOrDefault(x => x.Url == url);
			}

			_event.Set();
			return temp;
		}

		public int Count
		{
			get
			{
				return _collection.Count;
			}
		}

		public List<URL> Collection
		{
			get
			{
				/*_event.WaitOne();
				List<URL> temp = _collection.Select(x => x).ToList();
				_event.Set();
				return temp;*/
				return _collection;
			}
		}

		public string GetStatingURL()
		{
			return _collection[0].Url;
		}

		public URL GetNext()
		{
			_event.WaitOne();
			URL temp = _collection.Where(x => x.Status == "").FirstOrDefault();
			if (temp != null)
				temp.Status = "Downloading";
			_event.Set();
			return temp;
		}

		public bool IsAllDone()
		{
			_event.WaitOne();

			foreach(URL url in _collection)
			{
				if(url.Status != "Done" && url.Status != "Skipped" && url.Status != "Error" && url.Status != "External" && url.Status != "Redirected")
				{
					_event.Set();
					return false;
				}
			}

			_event.Set();
			return true;
		}
	}
}
