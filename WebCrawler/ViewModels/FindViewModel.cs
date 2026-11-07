using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WebCrawler.ViewModels;

/// <summary>
/// The Find window's data: it only collects the search text; what a search does is up to the function it is given,
/// and the text that returns is shown under the text box.
/// </summary>
public partial class FindViewModel : ObservableObject
{
    private readonly Func<string, string> searcher;

    public FindViewModel(Func<string, string> searcher)
    {
        this.searcher = searcher;
    }

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private string statusText = "";

    /// <summary>Raised when the search text was empty, so the window can put the cursor back into the box.</summary>
    public event EventHandler? EmptySearch;

    partial void OnSearchTextChanged(string value) => StatusText = "";

    [RelayCommand]
    private void FindNext()
    {
        if (UrlSearch.ParseTerms(SearchText).Length == 0)
        {
            StatusText = "Type the text to find.";
            EmptySearch?.Invoke(this, EventArgs.Empty);
            return;
        }

        StatusText = searcher(SearchText);
    }
}
