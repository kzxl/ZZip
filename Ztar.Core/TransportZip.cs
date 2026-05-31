using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Ztar.Core
{
    /// <summary>
    /// Wraps a finished SFX (the .exe plus any multi-part volumes) into a single transport .zip.
    /// Useful when mail servers or download gateways block raw .exe attachments. The zip uses
    /// <see cref="CompressionLevel.NoCompression"/> (Store) because the payload is already
    /// compressed — re-compressing wastes CPU for ~0% gain. It is purely an envelope.
    /// </summary>
    public static class TransportZip
    {
        /// <summary>
        /// Bundles <paramref name="sfxExePath"/> and its sibling volumes into <paramref name="zipPath"/>.
        /// Returns the list of file names placed inside the archive.
        /// </summary>
        public static string[] Wrap(string sfxExePath, string zipPath)
        {
            if (!File.Exists(sfxExePath))
                throw new FileNotFoundException("Không tìm thấy tệp SFX để bọc.", sfxExePath);

            string dir = Path.GetDirectoryName(Path.GetFullPath(sfxExePath)) ?? ".";
            string baseName = Path.GetFileNameWithoutExtension(sfxExePath);

            // The .exe plus any matching multi-part volumes (base.001, base.002, base.bin).
            var parts = new System.Collections.Generic.List<string> { sfxExePath };
            foreach (var f in Directory.EnumerateFiles(dir, baseName + ".*"))
            {
                string ext = Path.GetExtension(f).TrimStart('.');
                bool isVolume = ext.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || (ext.Length == 3 && ext.All(char.IsDigit));
                if (isVolume) parts.Add(f);
            }

            if (File.Exists(zipPath)) File.Delete(zipPath);
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var p in parts)
                zip.CreateEntryFromFile(p, Path.GetFileName(p), CompressionLevel.NoCompression);

            return parts.Select(Path.GetFileName).Where(n => n != null).ToArray()!;
        }
    }
}
