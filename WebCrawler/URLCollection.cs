﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;

namespace WebCrawler
{
    public class URLCollection
    {
        private BindingListInvoked<URL> _collection;
        private AutoResetEvent _event = new AutoResetEvent(true);

        public URLCollection(ISynchronizeInvoke invoke)
        {
            _collection = new BindingListInvoked<URL>(invoke);
        }

        public URL Add(string url)
        {
            return Add(url, 0, "");
        }

        public URL Add(string url, int depth, string parent)
        {
            return Add(url, depth, parent, URLStatus.Default);
        }

        public URL Add(string url, int depth, string parent, URLStatus status)
        {
            _event.WaitOne();

            URL temp;
            if (_collection.SingleOrDefault(x => x.Url == url) == null)
            {
                temp = new URL(url, depth, status);
                temp.LinksFrom.Add(parent);
                _collection.Add(temp);
            }
            else
            {
                temp = _collection.FirstOrDefault(x => x.Url == url);
            }

            _event.Set();
            return temp;
        }

        public int Count()
        {
            return _collection.Count;
        }

        public int Count(Func<URL, bool> predicate)
        {
            _event.WaitOne();
            int count = _collection.Count(predicate);
            _event.Set();
            return count;
        }

        public BindingListInvoked<URL> Collection
        {
            get
            {
                return _collection;
            }
        }

        public string GetStatingURL()
        {
            return _collection[0].Url.IndexOf("?") > 0 ? _collection[0].Url.Substring(0, _collection[0].Url.IndexOf("?")) : _collection[0].Url;
        }

        public URL GetNext()
        {
            _event.WaitOne();
            URL temp = _collection.Where(x => x.Status == URLStatus.Default).FirstOrDefault();
            if (temp != null)
                temp.Status = URLStatus.Downloading;
            _event.Set();
            return temp;
        }

        public bool IsAllDone()
        {
            _event.WaitOne();

            foreach (URL url in _collection)
            {
                if (url.Status < URLStatus.Done)
                {
                    _event.Set();
                    return false;
                }
            }

            _event.Set();
            return true;
        }

        public void Clear()
        {
            _event.WaitOne();
            _collection.Clear();
            _event.Set();
        }
    }

    public class BindingListInvoked<T> : BindingList<T>
    {
        private ISynchronizeInvoke _invoke;
        public BindingListInvoked(ISynchronizeInvoke invoke)
        {
            _invoke = invoke;
        }

        delegate void ListChangedDelegate(ListChangedEventArgs e);
        protected override void OnListChanged(ListChangedEventArgs e)
        {

            if ((_invoke != null) && (_invoke.InvokeRequired))
            {
                IAsyncResult ar = _invoke.BeginInvoke(new ListChangedDelegate(base.OnListChanged), new object[] { e });
            }
            else
            {
                base.OnListChanged(e);
            }
        }
    }
}
