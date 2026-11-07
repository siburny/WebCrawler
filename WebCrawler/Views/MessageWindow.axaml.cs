using Avalonia.Controls;
using Avalonia.Input;

namespace WebCrawler.Views;

/// <summary>A small modal message box (Avalonia has none of its own).</summary>
public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
        OkButton.Click += (_, _) => Close();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    public static Task ShowAsync(Window owner, string text)
    {
        MessageWindow window = new();
        window.MessageText.Text = text;
        return window.ShowDialog(owner);
    }
}
