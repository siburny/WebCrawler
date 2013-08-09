using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Web;
using System.Windows.Forms;

namespace WebCrawler
{
	[XmlRootAttribute(ElementName="Settings", IsNullable=false)]
	public sealed class Settings
	{
		public static Settings Instance = new Settings();
		private string configPath;
		public int maxProcesses;
		public int maxDepth;
		public string[] excludeRules;

		private Settings()
		{
			configPath = Application.StartupPath;

			//defaul
			maxProcesses = 5;
			maxDepth = 5;
			excludeRules = new string[1] { "http://www.expeditions.com/Request_a_Reservatio77.asp" };
		}

		public void Save()
		{
			XmlSerializer xs = new XmlSerializer( typeof(Settings) );
			TextWriter txt = new StreamWriter(configPath + "\\config.xml");

			xs.Serialize(txt, this);
			txt.Close();
		}

		public bool Load()
		{
			if (File.Exists(configPath + "\\config.xml"))
			{
				XmlSerializer xs = new XmlSerializer(typeof(Settings));
				TextReader txt = new StreamReader(configPath + "\\config.xml");

				Instance = xs.Deserialize(txt) as Settings;
				txt.Close();

				return true;
			}
			else
				return false;
		}
	}
}
