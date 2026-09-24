using System;
using System.IO;
using System.IO.Compression;

namespace ZZip.Core
{
    /// <summary>
    /// Fast standard PKZIP compressor creating cross-platform .zip archives.
    /// Compatible with Windows Explorer, macOS, Linux, Android, iOS, WinRAR, and 7-Zip.
    /// </summary>
    public static class StandardZip
    {
        /// <summary>
        /// Compresses a file or directory into a standard .zip archive.
        /// </summary>
        public static void CompressToZip(string sourcePath, string destZipPath, IProgress<long>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentNullException(nameof(sourcePath));

            string fullSource = Path.GetFullPath(sourcePath);
            if (File.Exists(destZipPath))
                File.Delete(destZipPath);

            string? parentDir = Path.GetDirectoryName(Path.GetFullPath(destZipPath));
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);

            if (Directory.Exists(fullSource))
            {
                ZipFile.CreateFromDirectory(fullSource, destZipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            }
            else if (File.Exists(fullSource))
            {
                using var zip = ZipFile.Open(destZipPath, ZipArchiveMode.Create);
                zip.CreateEntryFromFile(fullSource, Path.GetFileName(fullSource), CompressionLevel.Optimal);
            }
            else
            {
                throw new FileNotFoundException("Không tìm thấy đường dẫn nguồn cần nén ZIP.", sourcePath);
            }

            if (File.Exists(destZipPath))
            {
                progress?.Report(new FileInfo(destZipPath).Length);
            }
        }
    }
}
