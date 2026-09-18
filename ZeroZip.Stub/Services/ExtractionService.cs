using System;
using System.IO;
using ZeroZip.Core;

namespace ZeroZip.Stub.Services
{
    /// <summary>
    /// Runtime extractor for an SFX executable. Reads the ZeroZip footer from the end of the
    /// running .exe, then streams the appended (or multi-part) payload through the shared
    /// <see cref="ZtarEngine"/>. No embedded resources, no temp files.
    /// </summary>
    public class ExtractionService
    {
        private readonly string _selfPath;
        private ZtarFooter? _footer;

        public ExtractionService()
        {
            // In a single-file app this is the only reliable path to our own executable.
            _selfPath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Không xác định được đường dẫn tệp thực thi.");
        }

        /// <summary>The payload metadata read from this executable, if any.</summary>
        public ZtarFooter? Footer => _footer;

        /// <summary>
        /// Detects whether this executable carries an ZeroZip payload and produces a
        /// human-readable status line.
        /// </summary>
        public bool CheckPayload(out string statusMessage)
        {
            try
            {
                _footer = SfxComposer.ReadFooter(_selfPath);
            }
            catch (Exception ex)
            {
                statusMessage = "Không đọc được dữ liệu đính kèm: " + ex.Message;
                return false;
            }

            if (_footer == null)
            {
                statusMessage = "Trống rỗng. Tệp này không chứa dữ liệu nén ZeroZip.";
                return false;
            }

            if (_footer.IsMultiPart)
            {
                string baseName = Path.Combine(
                    Path.GetDirectoryName(_selfPath) ?? ".",
                    Path.GetFileNameWithoutExtension(_selfPath));
                if (!File.Exists($"{baseName}.001"))
                {
                    statusMessage = $"Thiếu tệp dữ liệu phân mảnh ({Path.GetFileName(baseName)}.001). Hãy đặt đủ các phần cạnh tệp .exe.";
                    return false;
                }
                statusMessage = $"Sẵn sàng! Gói phân mảnh {_footer.PartCount} phần [{_footer.Method}]"
                    + (_footer.IsEncrypted ? " (mã hóa)" : "") + ", "
                    + $"{FormatSize(_footer.PayloadSize)} nén / {FormatSize(_footer.OriginalSize)} gốc.";
                return true;
            }

            statusMessage = $"Đã tìm thấy kho nén đính kèm [{_footer.Method}]"
                + (_footer.IsEncrypted ? " (mã hóa)" : "")
                + $" ({FormatSize(_footer.PayloadSize)} nén"
                + (_footer.OriginalSize > 0 ? $" / {FormatSize(_footer.OriginalSize)} gốc" : "")
                + "). Nhấn Giải Nén để bắt đầu.";
            return true;
        }

        /// <summary>
        /// Extracts the payload to <paramref name="destinationPath"/>, verifying CRC32 first.
        /// </summary>
        /// <param name="password">Required when <see cref="ZtarFooter.IsEncrypted"/> is set.</param>
        public void ExtractPayload(string destinationPath, string? password = null, IProgress<long>? progress = null)
        {
            _footer ??= SfxComposer.ReadFooter(_selfPath)
                ?? throw new InvalidOperationException("Không có dữ liệu ZeroZip để giải nén.");

            if (_footer.IsEncrypted)
            {
                if (string.IsNullOrEmpty(password))
                    throw new InvalidOperationException("Gói dữ liệu được mã hóa. Cần nhập mật khẩu.");

                // Instant password verification before starting extraction
                using var probe = SfxComposer.OpenPayload(_selfPath, _footer);
                if (!PayloadCrypto.CheckPassword(probe, password))
                    throw new InvalidDataException("Sai mật khẩu.");
            }

            // Single-pass streaming extract with on-the-fly CRC32 verification (Zero redundant I/O)
            using var payload = SfxComposer.OpenPayload(_selfPath, _footer);
            ZtarEngine.UnpackVerified(payload, destinationPath, _footer.Method,
                _footer.IsEncrypted ? password : null, _footer.IsPrecompressed, progress, _footer.WindowLog,
                expectedCrc: _footer.Crc32);
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
    }
}
