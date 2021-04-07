using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Web;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Drawing;

namespace WebCrawler
{
	[XmlRootAttribute(ElementName="Settings", IsNullable=false)]
	public sealed class Settings
	{
		private string ConfigPath;
		public static Settings Instance = new Settings();

		public int MaxProcesses;
		public int MaxDepth;
        public int MaxMeasurements;
		public bool WarnOnRedirect;
        public bool QuickScan;
		public List<string> ExcludeRules;
		public List<SettingsHighlightRule> HighlightRules;

		private Settings()
		{
			ConfigPath = Application.StartupPath;

			//defaults
			MaxProcesses = 5;
			MaxDepth = 5;
            MaxMeasurements = 1;
            QuickScan = false;
			ExcludeRules = new List<string>();
			WarnOnRedirect = false;
			HighlightRules = new List<SettingsHighlightRule>();
		}

		public void Save()
		{
			XmlSerializer xs = new XmlSerializer(typeof(Settings));
			TextWriter txt = new StreamWriter(ConfigPath + "\\config.xml");

			xs.Serialize(txt, this);
			txt.Close();
		}

		public bool Load()
		{
			if (File.Exists(ConfigPath + "\\config.xml"))
			{
				XmlSerializer xs = new XmlSerializer(typeof(Settings));
				TextReader txt = new StreamReader(ConfigPath + "\\config.xml");

				Instance = xs.Deserialize(txt) as Settings;
				txt.Close();

				return true;
			}
			else
				return false;
		}
	}

	public sealed class SettingsHighlightRule
	{
		private Regex regex;
		public string Expression
		{
			get
			{
				return regex.ToString();
			}
			set
			{
				regex = new Regex(value, RegexOptions.IgnoreCase);
			}
		}
		private Color highlightColor;
		public string HighlightColor
		{
			get
			{
				string t = highlightColor.ToArgb().ToString("x");
				if (t.Length > 6)
					t = t.Substring(2);
				return "0x" + t;
			}
			set
			{
				highlightColor = Color.FromArgb(Convert.ToInt32(value, 16));
			}
		}

		public Color GetHighlightColor()
		{
			return highlightColor;
		}

		public bool IsMatch(string value)
		{
			return regex.IsMatch(value);
		}
	}
}
