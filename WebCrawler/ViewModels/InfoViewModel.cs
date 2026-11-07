namespace WebCrawler.ViewModels;

/// <summary>What the Link Information window shows about one address.</summary>
public sealed class InfoViewModel
{
    public InfoViewModel(URL url)
    {
        Link = url.Url;
        LinkedFrom = string.Join(Environment.NewLine, url.GetLinksFrom());
        LinksTo = string.Join(Environment.NewLine, url.GetLinksTo());
    }

    public string Link { get; }
    public string LinkedFrom { get; }
    public string LinksTo { get; }
}
