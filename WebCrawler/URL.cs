using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WebCrawler
{
	public class URL : INotifyPropertyChanged
	{
		//public int Id;
		URLStatus _status;
		public URLStatus Status
		{
			get { return _status; }
			set
			{
				_status = value;
				NotifyPropertyChanged("Status");
			}
		}

		private string _url;
		public string Url
		{
			get { return _url; }
			set
			{
				_url = value;
				NotifyPropertyChanged("Url");
			}
		}

		private string _mimeType;
		public string MimeType
		{
			get { return _mimeType; }
			set
			{
				_mimeType = value;
				NotifyPropertyChanged("MimeType");
			}
		}

		private long? _contentLength;
		/// <summary>Size of the response body in bytes, or null when unknown (no Content-Length header and no body read).</summary>
		public long? ContentLength
		{
			get { return _contentLength; }
			set
			{
				_contentLength = value;
				NotifyPropertyChanged("ContentLength");
			}
		}

		private List<double> _timeTaken = new List<double>();
		public double TimeTaken
		{
			get { return _timeTaken.Count == 0 ? 0.0 : _timeTaken.Average(); }
			set
			{
				TimeTakenAttempts++;
				_timeTaken.Add(value);
				NotifyPropertyChanged("TimeTaken");
			}
		}
		public string TimeTakenAll(string separator = "\t")
		{
			return string.Join(separator, _timeTaken.Select(x => x.ToString(CultureInfo.InvariantCulture)));
		}

		/// <summary>
		/// One text value per measurement (invariant culture, so the decimal separator never depends on the
		/// machine's regional settings), padded with empty strings up to <paramref name="padTo"/> entries.
		/// </summary>
		public string[] TimeTakenValues(int padTo)
		{
			List<string> values = _timeTaken.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();
			while (values.Count < padTo)
				values.Add("");
			return values.ToArray();
		}
		public int TimeTakenAttempts = 0;

		private string _notes;
		public string Notes
		{
			get { return _notes; }
			set
			{
				_notes = value;
				NotifyPropertyChanged("Notes");
			}
		}

		private int _depth;
		public int Depth
		{
			get { return _depth; }
			set
			{
				_depth = value;
				NotifyPropertyChanged("Depth");
			}
		}

		private Color _highlightColor;
		public Color HighlightColor
		{
			get { return _highlightColor; }
			set
			{
				_highlightColor = value;
				NotifyPropertyChanged("HighlightColor");
			}
		}

		private int? _statusCode;
		/// <summary>HTTP status code of the response, or null when no HTTP response was received.</summary>
		public int? StatusCode
		{
			get { return _statusCode; }
			set
			{
				_statusCode = value;
				NotifyPropertyChanged("StatusCode");
			}
		}

		// Link lists are written by worker threads and read by the UI thread, so access goes through the lock.
		private readonly object _linksLock = new object();
		private readonly List<string> _linksFrom = new List<string>();
		private readonly List<string> _linksTo = new List<string>();
		private readonly HashSet<string> _linksFromSet = new HashSet<string>();
		private readonly HashSet<string> _linksToSet = new HashSet<string>();

		/// <summary>Records a page that links to this URL (duplicates and empty values are ignored).</summary>
		public void AddLinkFrom(string url)
		{
			if (string.IsNullOrEmpty(url))
				return;
			lock (_linksLock)
			{
				if (_linksFromSet.Add(url))
					_linksFrom.Add(url);
			}
		}

		/// <summary>Records a URL that this page links to (duplicates and empty values are ignored).</summary>
		public void AddLinkTo(string url)
		{
			if (string.IsNullOrEmpty(url))
				return;
			lock (_linksLock)
			{
				if (_linksToSet.Add(url))
					_linksTo.Add(url);
			}
		}

		public string[] GetLinksFrom()
		{
			lock (_linksLock)
			{
				return _linksFrom.ToArray();
			}
		}

		public string[] GetLinksTo()
		{
			lock (_linksLock)
			{
				return _linksTo.ToArray();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

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
			Notes = "";
			Depth = depth;
		}

		private void NotifyPropertyChanged(string name)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	public enum URLStatus
	{
		Default = 0,
		Downloading = 1,
		Parsing = 2,
		Done = 10,
		Redirected = 11,
		Warning = 20,
		Error = 30,
		External = 40,
		Skipped = 100
	}
}