using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WebCrawler;

static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool FreeConsole();

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            FreeConsole();
        }

        // Visual styles, text rendering and high-DPI mode come from the project file (see WebCrawler.csproj).
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args));
    }
}
