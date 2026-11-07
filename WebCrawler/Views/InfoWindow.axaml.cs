using Avalonia.Controls;

namespace WebCrawler.Views;

public partial class InfoWindow : Window
{
    public InfoWindow()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Close();
    }
}
