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
		public static Settings Instance = new Settings();
		private string ConfigPath;
		public int MaxProcesses;
		public int MaxDepth;
		public bool WarnOnRedirect;
		public List<string> ExcludeRules;
		public List<SettingsHighlightRule> HighlightRules = new List<SettingsHighlightRule>() { new SettingsHighlightRule { Expression = "repSubNav", HighlightColor = "0x" + Color.Pink.ToArgb().ToString("x") } };

		private Settings()
		{
			ConfigPath = Application.StartupPath;

			//default
			MaxProcesses = 5;
			MaxDepth = 5;
			ExcludeRules = new List<string>();
			WarnOnRedirect = false;
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
				return "0x" + highlightColor.ToArgb().ToString("x");
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
