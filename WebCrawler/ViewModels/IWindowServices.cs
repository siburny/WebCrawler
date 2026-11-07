namespace WebCrawler.ViewModels;

/// <summary>What the main view model needs from its window: dialogs and things only a window can do.</summary>
public interface IWindowServices
{
    Task ShowMessageAsync(string text);

    /// <summary>Lets the user pick a text file with addresses; null when cancelled.</summary>
    Task<string?> PickUrlListAsync();

    Task ShowSettingsAsync();

    void ShowInfo(URL url);

    void ShowFind();

    /// <summary>Scrolls the grid so the row is visible.</summary>
    void RevealRow(UrlRow row);

    void CloseWindow();
}
