using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ZeroCompression.Core;
using ZeroZip.Core;
using ZeroZip.Main.Services;

namespace ZeroZip.Main;

static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
            return 0;
        }

        string first = args[0];

        // 1. GUI Progress Mode: Compression
        if (first.Equals("--gui-compress", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
        {
            ApplicationConfiguration.Initialize();
            string source = args[1];
            long split = 0;
            string? dest = null;
            bool isZip = false;
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].Equals("--split", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    split = ParseSize(args[++i]);
                }
                else if ((args[i].Equals("-o", StringComparison.OrdinalIgnoreCase) || args[i].Equals("--output", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    dest = args[++i];
                }
                else if (args[i].Equals("--zip", StringComparison.OrdinalIgnoreCase) || args[i].Equals("-z", StringComparison.OrdinalIgnoreCase))
                {
                    isZip = true;
                }
            }

            if (isZip && string.IsNullOrEmpty(dest))
            {
                string baseDir = Path.GetDirectoryName(Path.GetFullPath(source)) ?? ".";
                string name = Path.GetFileName(source.TrimEnd('\\', '/'));
                dest = Path.Combine(baseDir, name + ".zip");
            }
            else if (string.IsNullOrEmpty(dest))
            {
                string baseDir = Path.GetDirectoryName(Path.GetFullPath(source)) ?? ".";
                string name = Path.GetFileName(source.TrimEnd('\\', '/'));
                dest = Path.Combine(baseDir, name + ".zz");
            }

            long totalBytes = 0;
            if (File.Exists(source)) totalBytes = new FileInfo(source).Length;
            else if (Directory.Exists(source))
            {
                foreach (var f in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    try { totalBytes += new FileInfo(f).Length; } catch { }
                }
            }

            var dialog = new OperationProgressDialog(OperationType.Compress, source, dest, null, null, totalBytes);
            Application.Run(dialog);
            return dialog.IsCompleted ? 0 : 1;
        }

        // 2. GUI Progress Mode: Extraction
        if (first.Equals("--gui-extract", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
        {
            ApplicationConfiguration.Initialize();
            string source = args[1];
            bool toFolder = false;
            bool extractHere = false;

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].Equals("--to-folder", StringComparison.OrdinalIgnoreCase))
                    toFolder = true;
                else if (args[i].Equals("--here", StringComparison.OrdinalIgnoreCase))
                    extractHere = true;
            }

            string? destDir = null;
            if (toFolder)
            {
                destDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source)) ?? ".", Path.GetFileNameWithoutExtension(source));
            }
            else if (extractHere)
            {
                destDir = Path.GetDirectoryName(Path.GetFullPath(source)) ?? ".";
            }
            else
            {
                using var fbd = new FolderBrowserDialog
                {
                    Description = "Chọn thư mục giải nén",
                    UseDescriptionForTitle = true,
                    SelectedPath = Path.GetDirectoryName(Path.GetFullPath(source)) ?? ""
                };
                if (fbd.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    return 0; // User cancelled
                }
                destDir = fbd.SelectedPath;
            }

            long totalBytes = 0;
            try
            {
                var f = SfxComposer.ReadFooter(source);
                if (f != null) totalBytes = f.OriginalSize;
                else if (File.Exists(source)) totalBytes = new FileInfo(source).Length;
            }
            catch { }

            var dialog = new OperationProgressDialog(OperationType.Extract, source, destDir, null, null, totalBytes);
            Application.Run(dialog);
            return dialog.IsCompleted ? 0 : 1;
        }

        // 3. GUI Progress Mode: Test Archive
        if (first.Equals("--gui-test", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
        {
            ApplicationConfiguration.Initialize();
            string source = args[1];
            long totalBytes = 0;
            try
            {
                var f = SfxComposer.ReadFooter(source);
                if (f != null) totalBytes = f.PayloadSize;
            }
            catch { }

            var dialog = new OperationProgressDialog(OperationType.Test, source, null, null, null, totalBytes);
            Application.Run(dialog);
            return dialog.IsCompleted ? 0 : 1;
        }

        // 4. GUI Add to Archive Dialog Mode (WinRAR / 7-Zip context menu "Add to archive...")
        if (first.Equals("--studio", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Dialogs.AddArchiveDialog(args[1]));
            return 0;
        }

        // 5. Open single file directly in Archive Explorer (like WinRAR double click on archive)
        if (args.Length == 1 && (File.Exists(first) || Directory.Exists(first)))
        {
            ApplicationConfiguration.Initialize();
            if (File.Exists(first) && (first.EndsWith(".zz", StringComparison.OrdinalIgnoreCase) ||
                                       first.EndsWith(".ztar", StringComparison.OrdinalIgnoreCase) ||
                                       first.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                                       first.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                Application.Run(new Form1(initialArchivePath: first));
            }
            else
            {
                Application.Run(new Dialogs.AddArchiveDialog(first));
            }
            return 0;
        }

        // 6. Console CLI Mode
        if (AttachConsole(ATTACH_PARENT_PROCESS))
        {
            try
            {
                var stdOut = Console.OpenStandardOutput();
                Console.SetOut(new StreamWriter(stdOut, System.Text.Encoding.UTF8) { AutoFlush = true });
                var stdErr = Console.OpenStandardError();
                Console.SetError(new StreamWriter(stdErr, System.Text.Encoding.UTF8) { AutoFlush = true });
            }
            catch { }
        }

        try
        {
            return Cli.Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Lỗi: " + ex.Message);
            return 1;
        }
    }

    private static long ParseSize(string s)
    {
        s = s.Trim().ToUpperInvariant();
        long mul = 1;
        if (s.EndsWith("GB") || s.EndsWith("G")) mul = 1024L * 1024 * 1024;
        else if (s.EndsWith("MB") || s.EndsWith("M")) mul = 1024L * 1024;
        else if (s.EndsWith("KB") || s.EndsWith("K")) mul = 1024L;

        string num = s.TrimEnd('G', 'B', 'M', 'K');
        return double.TryParse(num, out double val) ? (long)(val * mul) : 0;
    }
}
