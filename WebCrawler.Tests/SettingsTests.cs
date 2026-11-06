using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Xunit;

namespace WebCrawler.Tests;

/// <summary>
/// Settings live in static state and on disk, so these tests point the program folder and the user-data folder
/// at temporary folders: they never read or write the real settings of whoever runs them.
/// </summary>
[Collection("Settings")]
public sealed class SettingsTests : IDisposable
{
    private readonly string originalProgramDirectory = Settings.ProgramDirectory;
    private readonly string programDirectory;
    private readonly string userDataDirectory;

    public SettingsTests()
    {
        string root = Path.Combine(Path.GetTempPath(), "webcrawler-tests-" + Guid.NewGuid().ToString("N"));
        programDirectory = Path.Combine(root, "program");
        userDataDirectory = Path.Combine(root, "userdata");
        Directory.CreateDirectory(programDirectory);
        Directory.CreateDirectory(userDataDirectory);

        Settings.ProgramDirectory = programDirectory;
        Settings.UserDataDirectory = userDataDirectory;
        Settings.Instance = Settings.CreateDefaults();
    }

    public void Dispose()
    {
        Settings.ProgramDirectory = originalProgramDirectory;
        Settings.UserDataDirectory = null;
        Settings.Instance = Settings.CreateDefaults();
        Directory.Delete(Path.GetDirectoryName(programDirectory), true);
    }

    private string JsonPath { get { return Path.Combine(userDataDirectory, "WebCrawler", "config.json"); } }
    private string PortablePath { get { return Path.Combine(programDirectory, "config.json"); } }
    private string LegacyPath { get { return Path.Combine(programDirectory, "config.xml"); } }

    private void WriteUserJson(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(JsonPath));
        File.WriteAllText(JsonPath, json);
    }

    [Fact]
    public void FirstRunHasNothingToLoadAndSaveCreatesTheFile()
    {
        Assert.False(Settings.Instance.Load());
        Settings.Instance.Save();

        Assert.True(File.Exists(JsonPath));
        Assert.False(File.Exists(JsonPath + ".tmp"));
        Assert.True(Settings.Instance.Load());
        Assert.Equal(5, Settings.Instance.MaxProcesses);
        Assert.Equal(5, Settings.Instance.MaxDepth);
    }

    [Fact]
    public void SavedSettingsComeBackUnchanged()
    {
        Settings s = Settings.Instance;
        s.MaxProcesses = 7;
        s.MaxDepth = 3;
        s.MaxMeasurements = 2;
        s.QuickScan = true;
        s.ExcludeRules = new List<string> { "/logout", "café \"quoted\"" };
        s.HighlightRules = new List<SettingsHighlightRule> { new SettingsHighlightRule { Expression = "foo.*bar", HighlightColor = "0x00ff00" } };
        s.Save();

        Settings.Instance = Settings.CreateDefaults();
        Assert.True(Settings.Instance.Load());

        Settings loaded = Settings.Instance;
        Assert.Equal(7, loaded.MaxProcesses);
        Assert.Equal(3, loaded.MaxDepth);
        Assert.Equal(2, loaded.MaxMeasurements);
        Assert.True(loaded.QuickScan);
        Assert.Equal(new[] { "/logout", "café \"quoted\"" }, loaded.ExcludeRules);
        Assert.Single(loaded.HighlightRules);
        Assert.True(loaded.HighlightRules[0].IsMatch("xxFOOyyBARzz"));
        Assert.Equal(Color.FromArgb(0x00ff00).ToArgb(), loaded.HighlightRules[0].GetHighlightColor().ToArgb());
    }

    [Fact]
    public void SettingsOfEarlierVersionsAreImportedAndKeptInTheNewPlace()
    {
        // A config.xml exactly as an earlier version left it next to the program.
        File.WriteAllText(LegacyPath,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Settings xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
            "  <MaxProcesses>10</MaxProcesses>\n  <MaxDepth>1</MaxDepth>\n  <MaxMeasurements>1</MaxMeasurements>\n" +
            "  <WarnOnRedirect>false</WarnOnRedirect>\n  <QuickScan>false</QuickScan>\n  <ExcludeRules />\n  <HighlightRules />\n</Settings>");

        Assert.True(Settings.Instance.Load());
        Assert.Equal(10, Settings.Instance.MaxProcesses);
        Assert.Equal(1, Settings.Instance.MaxDepth);
        Assert.True(File.Exists(JsonPath));
        Assert.True(File.Exists(LegacyPath), "the old file is left alone");

        // The next start reads the new file, even without the old one.
        File.Delete(LegacyPath);
        Settings.Instance = Settings.CreateDefaults();
        Assert.True(Settings.Instance.Load());
        Assert.Equal(10, Settings.Instance.MaxProcesses);
    }

    [Fact]
    public void ImportKeepsGoodRulesAndSkipsUnusableOnes()
    {
        File.WriteAllText(LegacyPath,
            "<Settings>\n  <QuickScan>true</QuickScan>\n" +
            "  <ExcludeRules><string>/admin</string><string>a&amp;b &lt;c&gt;</string></ExcludeRules>\n" +
            "  <HighlightRules>\n" +
            "    <SettingsHighlightRule><Expression>error|fail</Expression><HighlightColor>0xff0000</HighlightColor></SettingsHighlightRule>\n" +
            "    <SettingsHighlightRule><Expression>(</Expression><HighlightColor>0x0000ff</HighlightColor></SettingsHighlightRule>\n" +
            "    <SettingsHighlightRule><Expression>ok</Expression><HighlightColor>zzz</HighlightColor></SettingsHighlightRule>\n" +
            "  </HighlightRules>\n</Settings>");

        Assert.True(Settings.Instance.Load());
        Assert.True(Settings.Instance.QuickScan);
        Assert.Equal(5, Settings.Instance.MaxDepth);   // not in the file: stays at its default
        Assert.Equal(new[] { "/admin", "a&b <c>" }, Settings.Instance.ExcludeRules);
        Assert.Single(Settings.Instance.HighlightRules);
        Assert.True(Settings.Instance.HighlightRules[0].IsMatch("it FAILED"));
    }

    [Fact]
    public void BrokenOldFileIsIgnored()
    {
        File.WriteAllText(LegacyPath, "<Settings><MaxDepth>");
        Assert.False(Settings.Instance.Load());
        Assert.Equal(5, Settings.Instance.MaxDepth);
    }

    [Fact]
    public void NewFileWinsOverTheOldOne()
    {
        File.WriteAllText(LegacyPath, "<Settings><MaxDepth>9</MaxDepth></Settings>");
        WriteUserJson("{\"MaxDepth\":2}");

        Assert.True(Settings.Instance.Load());
        Assert.Equal(2, Settings.Instance.MaxDepth);
    }

    [Fact]
    public void CorruptFileIsSetAsideInsteadOfOverwritten()
    {
        WriteUserJson("{ this is not json");

        Assert.False(Settings.Instance.Load());
        Assert.True(File.Exists(JsonPath + ".bad"));
        Assert.False(File.Exists(JsonPath));
        Assert.Equal(5, Settings.Instance.MaxProcesses);
    }

    [Fact]
    public void FileWithAnUnusableRuleIsSetAsideToo()
    {
        WriteUserJson("{\"MaxDepth\":9,\"HighlightRules\":[{\"Expression\":\"(\",\"HighlightColor\":\"0xff0000\"}]}");

        Assert.False(Settings.Instance.Load());
        Assert.True(File.Exists(JsonPath + ".bad"));
        Assert.Equal(5, Settings.Instance.MaxDepth);
    }

    [Fact]
    public void MissingFieldsKeepTheirDefaultsAndNullListsBecomeEmpty()
    {
        WriteUserJson("{\"MaxDepth\":4,\"ExcludeRules\":null,\"HighlightRules\":null}");

        Assert.True(Settings.Instance.Load());
        Assert.Equal(4, Settings.Instance.MaxDepth);
        Assert.Equal(5, Settings.Instance.MaxProcesses);
        Assert.Empty(Settings.Instance.ExcludeRules);
        Assert.Empty(Settings.Instance.HighlightRules);
    }

    [Fact]
    public void FileNextToTheProgramMakesItPortable()
    {
        WriteUserJson("{\"MaxDepth\":2}");
        File.WriteAllText(PortablePath, "{\"MaxDepth\":9}");

        Assert.True(Settings.Instance.Load());
        Assert.Equal(9, Settings.Instance.MaxDepth);

        Settings.Instance.MaxDepth = 8;
        Settings.Instance.Save();
        Assert.Contains("\"MaxDepth\": 8", File.ReadAllText(PortablePath));
        Assert.Equal("{\"MaxDepth\":2}", File.ReadAllText(JsonPath));
    }
}
