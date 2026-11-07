using Avalonia.Controls;
using WebCrawler.ViewModels;

namespace WebCrawler.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        SaveButton.Click += SaveClicked;
        CancelButton.Click += (_, _) => Close();
    }

    // Everything is validated first; the window only closes (and the settings are only changed) when all input is good.
    private async void SaveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
            return;

        string? error = vm.TrySave();
        if (error != null)
        {
            await MessageWindow.ShowAsync(this, error);
            return;
        }
        Close();
    }
}
