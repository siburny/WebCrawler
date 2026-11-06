using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WebCrawler
{
    public sealed class Settings
    {
        private const string FileName = "config.json";
        private const string LegacyFileName = "config.xml";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public static Settings Instance = new Settings();

        public int MaxProcesses { get; set; }
        public int MaxDepth { get; set; }
        public int MaxMeasurements { get; set; }
        public bool WarnOnRedirect { get; set; }
        public bool QuickScan { get; set; }
        public List<string> ExcludeRules { get; set; }
        public List<SettingsHighlightRule> HighlightRules { get; set; }

        [JsonConstructor]
        private Settings()
        {
            //defaults
            MaxProcesses = 5;
            MaxDepth = 5;
            MaxMeasurements = 1;
            QuickScan = false;
            ExcludeRules = new List<string>();
            WarnOnRedirect = false;
            HighlightRules = new List<SettingsHighlightRule>();
        }

        /// <summary>
        /// Where the settings are read from and written to: a config.json next to the program if there is one
        /// (a portable copy keeps its own settings), otherwise the user's application-data folder, which is always
        /// writable (the program's own folder often is not, e.g. under Program Files).
        /// </summary>
        public static string ConfigPath
        {
            get
            {
                string portable = Path.Combine(AppContext.BaseDirectory, FileName);
                if (File.Exists(portable))
                    return portable;

                // Option.Create: without it the folder name comes back empty when the folder does not exist yet.
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create);
                if (string.IsNullOrEmpty(appData))
                    return portable;
                return Path.Combine(appData, "WebCrawler", FileName);
            }
        }

        public void Save()
        {
            string path = ConfigPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Written to a temporary file first and then moved over the old one, so a crash or a full disk
            // can never leave a half-written config behind.
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(temp, path, true);
        }

        /// <summary>
        /// Loads the saved settings into <see cref="Instance"/>. Returns false when there is nothing usable to load
        /// (first run, or a broken file), in which case the defaults stay in place. Settings saved by older versions
        /// (config.xml next to the program) are imported once and then kept in the new place.
        /// </summary>
        public bool Load()
        {
            string path = ConfigPath;
            if (File.Exists(path))
            {
                Settings loaded = null;
                try
                {
                    loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), JsonOptions);
                }
                catch (Exception e) when (!(e is IOException) && !(e is UnauthorizedAccessException))
                {
                    // Not valid JSON, or a rule that cannot be used (e.g. a broken regular expression).
                    // I/O problems are different: the file may be perfectly fine, so they are not swallowed.
                }

                if (loaded == null)
                {
                    SetAsideBrokenFile(path);
                    return false;
                }

                loaded.ExcludeRules = loaded.ExcludeRules ?? new List<string>();
                loaded.HighlightRules = loaded.HighlightRules ?? new List<SettingsHighlightRule>();
                Instance = loaded;
                return true;
            }

            string legacyPath = Path.Combine(AppContext.BaseDirectory, LegacyFileName);
            if (File.Exists(legacyPath))
            {
                Settings imported = TryImportLegacy(legacyPath);
                if (imported != null)
                {
                    Instance = imported;
                    try
                    {
                        imported.Save();
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        // The imported values are used for this run; the import is retried next time.
                    }
                    return true;
                }
            }

            return false;
        }

        // A file that cannot be read is renamed (config.json.bad) rather than overwritten by the defaults that get saved next.
        private static void SetAsideBrokenFile(string path)
        {
            try
            {
                File.Move(path, path + ".bad", true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
        }

        // Reads the config.xml format of earlier versions (what XmlSerializer wrote for this class).
        private static Settings TryImportLegacy(string path)
        {
            try
            {
                XElement root = XDocument.Load(path).Root;
                if (root == null)
                    return null;

                Settings settings = new Settings();
                settings.MaxProcesses = ReadInt(root, "MaxProcesses", settings.MaxProcesses);
                settings.MaxDepth = ReadInt(root, "MaxDepth", settings.MaxDepth);
                settings.MaxMeasurements = ReadInt(root, "MaxMeasurements", settings.MaxMeasurements);
                settings.WarnOnRedirect = ReadBool(root, "WarnOnRedirect", settings.WarnOnRedirect);
                settings.QuickScan = ReadBool(root, "QuickScan", settings.QuickScan);

                XElement exclude = root.Element("ExcludeRules");
                if (exclude != null)
                    settings.ExcludeRules = exclude.Elements("string").Select(x => x.Value).ToList();

                XElement highlight = root.Element("HighlightRules");
                if (highlight != null)
                {
                    foreach (XElement rule in highlight.Elements("SettingsHighlightRule"))
                    {
                        string expression = (string)rule.Element("Expression");
                        string color = (string)rule.Element("HighlightColor");
                        if (expression == null || color == null)
                            continue;
                        try
                        {
                            settings.HighlightRules.Add(new SettingsHighlightRule() { Expression = expression, HighlightColor = color });
                        }
                        catch (Exception e) when (e is ArgumentException || e is FormatException || e is OverflowException)
                        {
                            // One unusable rule must not cost the user the rest of their settings.
                        }
                    }
                }

                return settings;
            }
            catch (System.Xml.XmlException)
            {
                return null;
            }
        }

        private static int ReadInt(XElement root, string name, int fallback)
        {
            int value;
            return int.TryParse((string)root.Element(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static bool ReadBool(XElement root, string name, bool fallback)
        {
            bool value;
            return bool.TryParse((string)root.Element(name), out value) ? value : fallback;
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
