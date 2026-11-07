using System.Runtime.InteropServices;
using Avalonia;

namespace WebCrawler;

static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    /// <summary>The command-line arguments, read by the main window when it opens.</summary>
    public static string[] Args { get; private set; } = Array.Empty<string>();

    [STAThread]
    public static void Main(string[] args)
    {
        Args = args;

        // On Windows the program is a console program (so "-o console" can print), which opens a console window;
        // it is closed again unless the program was started with arguments. Other systems have nothing to close.
        if (args.Length == 0 && OperatingSystem.IsWindows())
            FreeConsole();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
