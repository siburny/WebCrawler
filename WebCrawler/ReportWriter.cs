using System.Globalization;
using System.Text;

namespace WebCrawler;

/// <summary>Writes the finished scan as a report: CSV, tab-separated text, or a table on the console.</summary>
public static class ReportWriter
{
    /// <summary>Prints the report to the console.</summary>
    public static void WriteConsole(IReadOnlyList<URL> urls, TextWriter output)
    {
        output.WriteLine("URL".PadRight(100, ' ') + "Status".PadRight(10, ' ') + "HTTP".PadRight(6, ' ') + "Time Taken");
        foreach (URL url in urls)
        {
            output.WriteLine(url.Url.PadRight(100, ' ').Substring(0, 98) + "  " + url.Status.ToString().PadRight(10, ' ') + FormatStatusCode(url).PadRight(6, ' ') + url.TimeTakenAll(" | "));
        }
    }

    /// <summary>
    /// Saves the report to a file. A ".csv" report is real CSV (comma separated, fields quoted where needed);
    /// any other file name keeps the tab-separated layout.
    /// </summary>
    public static void WriteFile(IReadOnlyList<URL> urls, string path)
    {
        bool csv = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        string separator = csv ? "," : "\t";

        // One "Time Taken" column per measurement, so every row has the same number of columns
        // and Status / Highlight / HTTP Status always sit in the same place.
        int measurements = Math.Max(1, urls.Select(x => x.TimeTakenAttempts).DefaultIfEmpty(0).Max());

        using StreamWriter file = new StreamWriter(path, false, new UTF8Encoding(false));
        List<string> header = new List<string> { "URL" };
        for (int i = 1; i <= measurements; i++)
            header.Add(measurements == 1 ? "Time Taken" : "Time Taken " + i.ToString(CultureInfo.InvariantCulture));
        header.Add("Status");
        header.Add("Highlight");
        header.Add("HTTP Status");
        file.WriteLine(string.Join(separator, header.Select(f => csv ? CsvField(f) : f)));

        foreach (URL url in urls)
        {
            List<string> fields = new List<string> { url.Url };
            fields.AddRange(url.TimeTakenValues(measurements));
            fields.Add(url.Status.ToString());
            fields.Add(url.HighlightColor.IsEmpty ? "" : url.HighlightColor.Name);
            fields.Add(FormatStatusCode(url));
            file.WriteLine(string.Join(separator, fields.Select(f => csv ? CsvField(f) : f)));
        }
    }

    private static string FormatStatusCode(URL url)
    {
        return url.StatusCode.HasValue ? url.StatusCode.Value.ToString(CultureInfo.InvariantCulture) : "";
    }

    private static string CsvField(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
