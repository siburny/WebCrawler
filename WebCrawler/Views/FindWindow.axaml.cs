using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using WebCrawler.ViewModels;

namespace WebCrawler.Views;

/// <summary>
/// The Find window: a small window that stays open beside the main window, so Find Next can be pressed again and again.
/// Closing it only hides it, so the search text is still there the next time.
/// </summary>
public partial class FindWindow : Window
{
    public FindWindow()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Close();

        // Ctrl+F (Cmd+F on macOS) in this window goes back to the search text.
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F, KeyModifiers.Control), Command = new FocusCommand(this) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F, KeyModifiers.Meta), Command = new FocusCommand(this) });
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is FindViewModel vm)
            vm.EmptySearch += (_, _) => SearchBox.Focus();
    }

    /// <summary>The first time it opens, it sits at the top right of the main window where it hides little of the list.</summary>
    public void PlaceNextTo(Window owner)
    {
        double scale = owner.RenderScaling;
        int x = owner.Position.X + (int)((owner.Bounds.Width - Width - 30) * scale);
        int y = owner.Position.Y + (int)(110 * scale);
        Position = new PixelPoint(Math.Max(0, x), Math.Max(0, y));
    }

    /// <summary>Shows the window (or brings it forward when it is already open) with the search text selected, ready to type over.</summary>
    public void ShowFind(Window owner)
    {
        if (!IsVisible)
            Show(owner);
        else
            Activate();

        FocusSearchText();
    }

    private void FocusSearchText()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    // Closing by the user only hides the window; when the main window closes, this one closes with it.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (e.CloseReason != WindowCloseReason.OwnerWindowClosing && e.CloseReason != WindowCloseReason.ApplicationShutdown && e.CloseReason != WindowCloseReason.OSShutdown)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    private sealed class FocusCommand : System.Windows.Input.ICommand
    {
        private readonly FindWindow window;

        public FocusCommand(FindWindow window) => this.window = window;

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => window.FocusSearchText();
    }
}
