using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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

		private long _contentLength;
		public long ContentLength
		{
			get { return _contentLength; }
			set
			{
				_contentLength = value;
				NotifyPropertyChanged("ContentLength");
			}
		}

		private double _timeTaken;
		public double TimeTaken
		{
			get { return _timeTaken; }
			set
			{
				_timeTaken = value;
				NotifyPropertyChanged("TimeTaken");
			}
		}

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

		public List<string> LinksFrom = new List<string>();
		public List<string> LinksTo = new List<string>();
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
			ContentLength = 0;
			TimeTaken = 0;
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