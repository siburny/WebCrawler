using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WebCrawler.ViewModels;

/// <summary>
/// One line of the results grid: wraps a crawled <see cref="URL"/> (which workers fill in on other threads and which
/// has no change notification of its own) and tells the grid to re-read it when <see cref="Refresh"/> is called
/// on the UI thread.
/// </summary>
public sealed class UrlRow : ObservableObject
{
    // Status icons: a coloured disc with a white mark (96-DPI-independent vector shapes, so they are sharp at any scaling).
    private static readonly Geometry CheckMark = Geometry.Parse("M 4,8.5 L 7,11.5 L 12,5");
    private static readonly Geometry CrossMark = Geometry.Parse("M 5,5 L 11,11 M 11,5 L 5,11");
    private static readonly Geometry ArrowMark = Geometry.Parse("M 4,8 L 11,8 M 8,5 L 11,8 L 8,11");
    private static readonly Geometry ExternalMark = Geometry.Parse("M 5,11 L 11,5 M 6,5 L 11,5 L 11,10");
    private static readonly Geometry NoMark = Geometry.Parse("M 8,8 L 8,8");

    // Row text colours: darker shades on a light background, lighter ones on a dark background.
    private sealed record Palette(IBrush Green, IBrush Red, IBrush Blue, IBrush Brown, IBrush Orange, IBrush Gray);

    private static IBrush Brush(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b)).ToImmutable();

    private static readonly Palette LightPalette = new(
        Brush(0, 128, 0), Brush(220, 0, 0), Brush(0, 0, 255), Brush(165, 42, 42), Brush(255, 140, 0), Brush(160, 160, 160));

    private static readonly Palette DarkPalette = new(
        Brush(90, 210, 100), Brush(255, 105, 105), Brush(110, 170, 255), Brush(215, 165, 120), Brush(255, 175, 70), Brush(130, 130, 130));

    // The palette of the theme the app is showing right now (follows the system's light/dark setting).
    private static Palette CurrentPalette =>
        Application.Current?.ActualThemeVariant == ThemeVariant.Dark ? DarkPalette : LightPalette;

    public UrlRow(URL url)
    {
        Url = url;
    }

    public URL Url { get; }

    /// <summary>Tells the grid that the values of this row may have changed (UI thread only).</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);

    public string Address => Url.Url;
    public string Status => Url.Status.ToString();
    public string HttpCode => Url.StatusCode?.ToString(CultureInfo.CurrentCulture) ?? "";
    public int Depth => Url.Depth;
    public string MimeType => Url.MimeType;
    public string ContentLength => Url.ContentLength?.ToString("N0", CultureInfo.CurrentCulture) ?? "";
    public string TimeTaken => Url.TimeTaken.ToString("N0", CultureInfo.CurrentCulture);
    public string Notes => Url.Notes.ReplaceLineEndings(" ");

    /// <summary>Text colour of the row: the colour of the matching highlight rule, otherwise one that tells the status.</summary>
    public IBrush Foreground
    {
        get
        {
            System.Drawing.Color highlight = Url.HighlightColor;
            if (!highlight.IsEmpty)
                return new SolidColorBrush(Color.FromRgb(highlight.R, highlight.G, highlight.B));

            Palette palette = CurrentPalette;
            return Url.Status switch
            {
                URLStatus.Error => palette.Red,
                URLStatus.External or URLStatus.Redirected => palette.Blue,
                URLStatus.Done => palette.Green,
                URLStatus.Skipped => palette.Brown,
                _ => palette.Orange,
            };
        }
    }

    public Geometry IconMark => Url.Status switch
    {
        URLStatus.Done => CheckMark,
        URLStatus.Error => CrossMark,
        URLStatus.Redirected => ArrowMark,
        URLStatus.External => ExternalMark,
        _ => NoMark,
    };

    public IBrush IconFill
    {
        get
        {
            Palette palette = CurrentPalette;
            return Url.Status switch
            {
                URLStatus.Done => palette.Green,
                URLStatus.Error => palette.Red,
                URLStatus.Redirected or URLStatus.External => palette.Blue,
                _ => palette.Gray,
            };
        }
    }
}
