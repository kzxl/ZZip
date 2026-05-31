using System;
using System.IO;
using SZip.Core;
using SZip.Main.Services;

namespace SZip.Main;

internal static class Cli
{
    public static int Run(string[] args)
    {
        string verb = args[0].ToLowerInvariant();
        return verb switch
        {
            "c" or "compress" or "-c" => RunCompress(args),
            "x" or "extract" or "-x" => RunExtract(args),
            "i" or "info" or "-i" => RunInfo(args),
            "e" or "estimate" or "--estimate" => RunEstimate(args),
            "h" or "help" or "-h" or "--help" or "/?" => PrintHelp(),
            _ => Fail($"Lệnh không hợp lệ: {verb}"),
        };
    }

    private static int RunCompress(string[] args)
    {
        string? source = null;
        string? output = null;
        long splitSize = 0;
        var profile = CompressionProfile.Ultra;
        var method = CompressionMethod.Zstd;
        string? password = null;
        bool wrapZip = false;
        bool usePrecomp = false;
        string? precompPath = null;
        string? precompArgs = null;
        bool longMode = false;
        int level = 0;
        int windowLog = 0;
        int workers = -2; // -2 = unset

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            switch (a.ToLowerInvariant())
            {
                case "-o" or "--output":
                    output = NextArg(args, ref i, a);
                    break;
                case "--split":
                    splitSize = ParseSize(NextArg(args, ref i, a));
                    break;
                case "--method" or "-m":
                    method = ParseMethod(NextArg(args, ref i, a));
                    break;
                case "--password" or "-p":
                    password = NextArg(args, ref i, a);
                    break;
                case "--zip":
                    wrapZip = true;
                    break;
                case "--precomp":
                    usePrecomp = true;
                    break;
                case "--precomp-path":
                    precompPath = NextArg(args, ref i, a);
                    usePrecomp = true;
                    break;
                case "--precomp-args":
                    precompArgs = NextArg(args, ref i, a);
                    break;
                case "--long":
                    longMode = true;
                    break;
                case "--fast":
                    profile = CompressionProfile.Fast;
                    break;
                case "--normal":
                    profile = CompressionProfile.Normal;
                    break;
                case "--ultra":
                    profile = CompressionProfile.Ultra;
                    break;
                case "--level":
                    level = int.Parse(NextArg(args, ref i, a));
                    break;
                case "--window":
                    windowLog = int.Parse(NextArg(args, ref i, a));
                    break;
                case "--threads":
                    workers = int.Parse(NextArg(args, ref i, a));
                    break;
                default:
                    if (a.StartsWith('-')) return Fail($"Tham số không rõ: {a}");
                    source ??= a;
                    break;
            }
        }

        if (source == null) return Fail("Thiếu đường dẫn nguồn.");
        if (output == null)
            output = (Directory.Exists(source) ? source.TrimEnd('\\', '/') : Path.ChangeExtension(source, null)) + "_SFX.exe";

        var options = CompressionOptions.FromProfile(profile, method);
        options.Password = password;
        options.UsePrecomp = usePrecomp;
        options.PrecompPath = precompPath;
        options.PrecompExtraArgs = precompArgs;
        if (level > 0) options.Level = level;
        if (windowLog > 0) options.WindowLog = windowLog;
        if (workers != -2) options.Workers = workers;
        if (longMode)
        {
            // Long-distance mode: 2 GiB window + LDM for maximum dedup on huge inputs.
            options.LongDistanceMatching = true;
            if (options.WindowLog < CompressionOptions.MaxLongWindowLog)
                options.WindowLog = CompressionOptions.MaxLongWindowLog;
        }

        if (usePrecomp)
        {
            string? exe = PrecompService.Locate(precompPath);
            if (exe == null)
                return Fail("Bật --precomp nhưng không tìm thấy precomp.exe (đặt cạnh SZip.exe, trong tools\\, hoặc PATH).");
            Console.WriteLine($"  precomp: {exe}");
        }

        Console.WriteLine($"SZip: nén \"{source}\"");
        Console.WriteLine($"  -> \"{output}\"  (method={method}, profile={profile}, level={options.Level}, " +
            $"ldm={options.LongDistanceMatching}, window={(options.WindowLog == 0 ? "auto" : options.WindowLog.ToString())}, " +
            $"threads={(options.Workers < 0 ? "auto" : options.Workers.ToString())}" +
            (usePrecomp ? ", precomp" : "") +
            (password != null ? ", encrypted" : "") + ")");

        var builder = new SfxBuilderService();
        var progress = new ConsoleProgress();
        PackResult result = builder.BuildSfx(source, output, splitSize, options, progress);
        progress.Done();

        Console.WriteLine();
        Console.WriteLine($"Hoàn tất. Gốc {FormatSize(result.OriginalSize)} -> nén {FormatSize(result.CompressedSize)} " +
            $"({result.Ratio:P1}), CRC32={result.Crc32:X8}");

        if (wrapZip)
        {
            string zip = builder.WrapForTransport(output);
            Console.WriteLine($"Đã bọc ZIP để gửi: \"{zip}\"");
        }
        return 0;
    }

    private static int RunEstimate(string[] args)
    {
        if (args.Length < 2) return Fail("Thiếu đường dẫn nguồn.");
        string source = args[1];
        Console.WriteLine($"SZip: ước tính tỉ lệ nén cho \"{source}\"...");
        var est = CompressionEstimator.Estimate(source);
        Console.WriteLine(est.Summary());
        Console.WriteLine($"  (mẫu {FormatSize(est.SampledBytes)} / tổng {FormatSize(est.TotalSize)}, " +
            $"tỉ lệ dự đoán {est.PredictedRatio:P1})");
        return 0;
    }

    private static int RunExtract(string[] args)
    {
        string? source = null;
        string? dest = null;
        string? password = null;
        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            switch (a.ToLowerInvariant())
            {
                case "-o" or "--output":
                    dest = NextArg(args, ref i, a);
                    break;
                case "--password" or "-p":
                    password = NextArg(args, ref i, a);
                    break;
                default:
                    if (a.StartsWith('-')) return Fail($"Tham số không rõ: {a}");
                    source ??= a;
                    break;
            }
        }

        if (source == null) return Fail("Thiếu đường dẫn tệp SFX.");
        dest ??= Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source)) ?? ".",
            Path.GetFileNameWithoutExtension(source) + "_Extracted");

        SZipFooter? footer = SfxComposer.ReadFooter(source);
        if (footer == null) return Fail("Tệp không phải SFX SZip hợp lệ.");

        if (footer.IsEncrypted && string.IsNullOrEmpty(password))
            return Fail("Gói được mã hóa. Dùng -p <mật_khẩu>.");

        Console.WriteLine($"SZip: giải nén \"{source}\" -> \"{dest}\"  [{footer.Method}]"
            + (footer.IsEncrypted ? " (mã hóa)" : ""));

        using (var verify = SfxComposer.OpenPayload(source, footer))
        {
            if (!SZipEngine.VerifyCrc(verify, footer.Crc32))
                return Fail("CRC32 không khớp, dữ liệu bị hỏng.");
        }

        var progress = new ConsoleProgress(footer.OriginalSize);
        using (var payload = SfxComposer.OpenPayload(source, footer))
        {
            SZipEngine.Unpack(payload, dest, footer.Method,
                footer.IsEncrypted ? password : null, footer.IsPrecompressed, progress, footer.WindowLog);
        }
        progress.Done();

        Console.WriteLine();
        Console.WriteLine("Giải nén hoàn tất.");
        return 0;
    }

    private static int RunInfo(string[] args)
    {
        if (args.Length < 2) return Fail("Thiếu đường dẫn tệp SFX.");
        string source = args[1];
        SZipFooter? f = SfxComposer.ReadFooter(source);
        if (f == null) return Fail("Tệp không phải SFX SZip hợp lệ.");

        Console.WriteLine($"Tệp:        {source}");
        Console.WriteLine($"Phiên bản:  {f.Version}");
        Console.WriteLine($"Thuật toán: {f.Method}" + (f.IsEncrypted ? "  (mã hóa AES-256)" : ""));
        Console.WriteLine($"Nén sâu:    {(f.IsPrecompressed ? "Có (precomp)" : "Không")}");
        Console.WriteLine($"Chế độ:     {(f.IsMultiPart ? $"Phân mảnh ({f.PartCount} phần)" : "Đính kèm")}");
        Console.WriteLine($"Nén:        {FormatSize(f.PayloadSize)}");
        Console.WriteLine($"Gốc:        {FormatSize(f.OriginalSize)}");
        if (f.OriginalSize > 0)
            Console.WriteLine($"Tỉ lệ:      {(double)f.PayloadSize / f.OriginalSize:P1}");
        Console.WriteLine($"WindowLog:  {(f.WindowLog == 0 ? "auto" : f.WindowLog.ToString())}");
        Console.WriteLine($"CRC32:      {f.Crc32:X8}");
        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
SZip - Trình siêu nén SFX

Cách dùng:
  SZip c <nguồn> [-o ra.exe] [-m zstd|lzma|brotli|store]
                 [--ultra|--normal|--fast] [--level N] [--window N]
                 [--threads N] [--split KÍCH_THƯỚC] [-p mật_khẩu] [--zip]
  SZip x <sfx.exe> [-o thư_mục] [-p mật_khẩu]
  SZip i <sfx.exe>
  SZip e <nguồn>                 (ước tính nhanh tỉ lệ nén)
  SZip h

Ví dụ:
  SZip c "C:\Data" -o Data.exe --ultra
  SZip c game.iso -m lzma --level 22 --split 2GB
  SZip c secret\ -o s.exe -p "MatKhau123" --zip
  SZip e "C:\Data"
  SZip x Data.exe -o C:\Out

Thuật toán (-m):
  zstd        nhanh, đa luồng, LDM (mặc định)
  lzma        nén sâu nhất (chậm hơn)
  brotli      tốt cho text/web
  store       không nén (chỉ đóng gói)

Tham số:
  --ultra/--normal/--fast   mức nỗ lực nén
  --level N                 ghi đè mức nén (zstd 1..22)
  --window N                windowLog (tối đa 31)
  --long                    nén tầm xa: cửa sổ 2GB + LDM (file rất lớn, zstd)
  --threads N               số luồng (0 = đơn luồng)
  --split SZ                cắt mảnh: 2GB, 4GB, 700MB...
  -p, --password            mã hóa AES-256
  --zip                     bọc kết quả vào .zip để né bộ lọc .exe
  --precomp                 nén sâu kiểu repack (cần precomp.exe, tốt cho game/installer)
  --precomp-path <đường>    chỉ định precomp.exe thủ công
  --precomp-args <args>     tham số thêm cho precomp (vd: -intense)
""");
        return 0;
    }

    private static CompressionMethod ParseMethod(string text) => text.Trim().ToLowerInvariant() switch
    {
        "zstd" or "zstandard" or "zst" => CompressionMethod.Zstd,
        "lzma" or "lz" or "xz" => CompressionMethod.Lzma,
        "brotli" or "br" => CompressionMethod.Brotli,
        "store" or "none" or "copy" => CompressionMethod.Store,
        _ => throw new ArgumentException($"Thuật toán không hợp lệ: {text} (zstd|lzma|brotli|store)"),
    };

    private static string NextArg(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"Thiếu giá trị cho {flag}.");
        return args[++i];
    }

    private static long ParseSize(string text)
    {
        text = text.Trim().ToUpperInvariant();
        long mult = 1;
        if (text.EndsWith("TB")) { mult = 1L << 40; text = text[..^2]; }
        else if (text.EndsWith("GB")) { mult = 1L << 30; text = text[..^2]; }
        else if (text.EndsWith("MB")) { mult = 1L << 20; text = text[..^2]; }
        else if (text.EndsWith("KB")) { mult = 1L << 10; text = text[..^2]; }
        else if (text.EndsWith("B")) { text = text[..^1]; }
        return (long)(double.Parse(text.Trim()) * mult);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 0) return "?";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.##} {units[unit]}";
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("Lỗi: " + message);
        return 1;
    }

    /// <summary>Renders an in-place progress line on the console.</summary>
    private sealed class ConsoleProgress : IProgress<long>
    {
        private readonly long _total;
        public ConsoleProgress(long total = -1) => _total = total;

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
