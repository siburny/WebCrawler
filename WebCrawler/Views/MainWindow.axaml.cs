using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using WebCrawler.ViewModels;

namespace WebCrawler.Views;

public partial class MainWindow : Window, IWindowServices
{
    private FindWindow? findWindow;

    public MainWindow()
    {
        InitializeComponent();
        Grid.DoubleTapped += GridDoubleTapped;
        // The row colours depend on the theme, so they are read again when the system switches between light and dark.
        ActualThemeVariantChanged += (_, _) => ViewModel?.RefreshAllRows();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (ViewModel is { } vm)
            vm.Services = this;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UrlBox.Focus();
        if (ViewModel is { } vm)
            await vm.InitializeAsync();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ViewModel?.Shutdown();
        base.OnClosing(e);
    }

    // Double-clicking a row (not a header or the empty area) shows what links to it and what it links to.
    private void GridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().OfType<DataGridRow>().Any())
            ViewModel?.ShowInfo(ViewModel.SelectedRow);
    }

    // ---- IWindowServices -------------------------------------------------------------------------------------------

    public Task ShowMessageAsync(string text) => MessageWindow.ShowAsync(this, text);

    public async Task<string?> PickUrlListAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load URL List",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Text files") { Patterns = new[] { "*.txt" } },
                FilePickerFileTypes.All,
            },
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task ShowSettingsAsync()
    {
        SettingsWindow window = new() { DataContext = new SettingsViewModel() };
        await window.ShowDialog(this);
    }

    public void ShowInfo(URL url)
    {
        InfoWindow window = new() { DataContext = new InfoViewModel(url) };
        _ = window.ShowDialog(this);
    }

    public void ShowFind()
    {
        if (ViewModel is not { } vm)
            return;
        if (findWindow == null)
        {
            findWindow = new FindWindow { DataContext = vm.Find };
            findWindow.PlaceNextTo(this);
        }
        findWindow.ShowFind(this);
    }

    public void RevealRow(UrlRow row)
    {
        Grid.ScrollIntoView(row, null);
    }

    public void CloseWindow() => Close();
}
