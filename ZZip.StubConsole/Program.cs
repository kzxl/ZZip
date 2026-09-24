using System;
using System.IO;
using ZZip.Core;

namespace ZZip.StubConsole;

/// <summary>
/// Cross-platform (Windows/Linux/macOS) headless self-extractor. Reads the ZZip footer from
/// the end of its own executable and extracts the payload. Usage:
///   ZZip.Stub [target-dir] [-p password] [-y]
/// With no target dir it extracts into "&lt;exe-name&gt;_Extracted" next to the executable.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

        string selfPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Không xác định được đường dẫn tệp thực thi.");

        string? target = null;
        string? password = null;
        bool assumeYes = false;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            switch (a.ToLowerInvariant())
            {
                case "-p" or "--password":
                    if (i + 1 >= args.Length) { Console.Error.WriteLine("Thiếu mật khẩu sau -p."); return 2; }
                    password = args[++i];
                    break;
                case "-y" or "--yes":
                    assumeYes = true;
                    break;
                case "-h" or "--help":
                    PrintHelp();
                    return 0;
                default:
                    if (a.StartsWith('-')) { Console.Error.WriteLine($"Tham số không rõ: {a}"); return 2; }
                    target ??= a;
                    break;
            }
        }

        ZtarFooter? footer = SfxComposer.ReadFooter(selfPath);
        if (footer == null)
        {
            Console.Error.WriteLine("Tệp này không chứa dữ liệu nén ZZip.");
            return 1;
        }

        target ??= Path.Combine(
            Path.GetDirectoryName(selfPath) ?? ".",
            Path.GetFileNameWithoutExtension(selfPath) + "_Extracted");

        Console.WriteLine($"ZZip - Trình giải nén [{footer.Method}]"
            + (footer.IsEncrypted ? " (mã hóa)" : "")
            + (footer.IsPrecompressed ? " (precomp)" : ""));
        Console.WriteLine($"Đích: {target}");
        if (footer.OriginalSize > 0)
            Console.WriteLine($"Dung lượng gốc: {FormatSize(footer.OriginalSize)}");

        if (footer.IsMultiPart)
        {
            string baseName = Path.Combine(
                Path.GetDirectoryName(selfPath) ?? ".",
                Path.GetFileNameWithoutExtension(selfPath));
            if (!File.Exists($"{baseName}.001"))
            {
                Console.Error.WriteLine($"Thiếu tệp phân mảnh {Path.GetFileName(baseName)}.001 cạnh tệp thực thi.");
                return 1;
            }
        }

        if (footer.IsEncrypted && string.IsNullOrEmpty(password))
        {
            password = PromptPassword();
            if (string.IsNullOrEmpty(password)) { Console.Error.WriteLine("Cần mật khẩu."); return 1; }
        }

        if (!assumeYes)
        {
            Console.Write("Tiến hành giải nén? [Y/n] ");
            string? ans = Console.ReadLine();
            if (!string.IsNullOrEmpty(ans) && !ans.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Đã hủy.");
                return 0;
            }
        }

        try
        {
            if (footer.IsEncrypted)
            {
                using var probe = SfxComposer.OpenPayload(selfPath, footer);
                if (!PayloadCrypto.CheckPassword(probe, password!))
                {
                    Console.Error.WriteLine("Sai mật khẩu.");
                    return 1;
                }
            }

            var progress = new ConsoleProgress(footer.OriginalSize);
            using var payload = SfxComposer.OpenPayload(selfPath, footer);
            ZtarEngine.UnpackVerified(payload, target, footer.Method,
                footer.IsEncrypted ? password : null, footer.IsPrecompressed,
                progress, footer.WindowLog, expectedCrc: footer.Crc32);
            progress.Done();

            Console.WriteLine("Giải nén hoàn tất.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Lỗi giải nén: " + ex.Message);
            return 1;
        }
    }

    private static string? PromptPassword()
    {
        Console.Write("Mật khẩu: ");
        var sb = new System.Text.StringBuilder();
        // Read without echoing when a real console is attached.
        if (!Console.IsInputRedirected)
        {
            ConsoleKeyInfo key;
            while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; }
                else if (!char.IsControl(key.KeyChar)) sb.Append(key.KeyChar);
            }
            Console.WriteLine();
            return sb.ToString();
        }
        return Console.ReadLine();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
ZZip self-extractor (đa nền tảng)

Cách dùng:
  <exe> [thư-mục-đích] [-p mật_khẩu] [-y]

  thư-mục-đích   nơi giải nén (mặc định: <tên-exe>_Extracted cạnh exe)
  -p, --password mật khẩu cho gói mã hóa
  -y, --yes      không hỏi xác nhận
  -h, --help     trợ giúp
""");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 0) return "?";
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double s = bytes; int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return $"{s:0.##} {u[i]}";
    }

    private sealed class ConsoleProgress : IProgress<long>
    {
        private readonly long _total;
        public ConsoleProgress(long total) => _total = total;

        public void Report(long done)
        {
            if (_total > 0)
            {
                int pct = (int)Math.Min(100, done * 100 / _total);
                Console.Write($"\r  {pct,3}%  {FormatSize(done)} / {FormatSize(_total)}        ");
            }
            else
            {
                Console.Write($"\r  {FormatSize(done)} đã xử lý        ");
            }
        }

        public void Done() => Console.Write("\r" + new string(' ', 50) + "\r");
    }
}
