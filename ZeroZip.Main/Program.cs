using System;
using System.Runtime.InteropServices;
using ZeroZip.Core;
using ZeroZip.Main.Services;

namespace ZeroZip.Main;

static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    /// <summary>
    /// Entry point. With no arguments the WinForms GUI launches; with arguments the app
    /// runs as a console-style CLI by attaching to the parent console.
    /// </summary>
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
            return 0;
        }

        AttachConsole(ATTACH_PARENT_PROCESS);
        try
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* ignore on redirected handles */ }
            return Cli.Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Lỗi: " + ex.Message);
            return 1;
        }
    }
}
