using System;
using System.IO;
using Ztar.Core.Streams;

namespace Ztar.Core
{
    /// <summary>
    /// Assembles and reads self-extracting (SFX) executables using the append model:
    /// <c>[ stub executable bytes ][ compressed payload ][ fixed footer ]</c>.
    ///
    /// In multi-part mode the .exe holds only <c>[ stub ][ footer ]</c> and the payload
    /// lives in external volumes (base.001, base.002 ...) created via
    /// <see cref="ChunkedWriteStream"/> and read back via <see cref="ChunkedReadStream"/>.
    /// </summary>
    public static class SfxComposer
    {
        /// <summary>
        /// Builds an appended SFX: copies the prebuilt stub, streams the compressed payload
        /// directly after it, then writes the footer. Single output file.
        /// </summary>
        /// <param name="stubBytes">The prebuilt extractor executable.</param>
        /// <param name="sourcePath">File or directory to pack.</param>
        /// <param name="destExePath">Output .exe path.</param>
        public static PackResult BuildAppended(byte[] stubBytes, string sourcePath, string destExePath,
            CompressionOptions options, IProgress<long>? progress = null,
            System.Threading.CancellationToken cancel = default)
        {
            ArgumentNullException.ThrowIfNull(stubBytes);

            using var output = new FileStream(destExePath, FileMode.Create, FileAccess.Write, FileShare.None);
            output.Write(stubBytes, 0, stubBytes.Length);
            long payloadOffset = output.Position;

            PackResult result = ZtarEngine.Pack(sourcePath, output, options, progress, cancel);

            var footer = new ZtarFooter
            {
                Flags = ZtarFlags.Appended
                    | (result.Encrypted ? ZtarFlags.Encrypted : ZtarFlags.None)
                    | (result.Precompressed ? ZtarFlags.Precompressed : ZtarFlags.None),
                PayloadOffset = payloadOffset,
                PayloadSize = result.CompressedSize,
                OriginalSize = result.OriginalSize,
                Crc32 = result.Crc32,
                WindowLog = result.WindowLog,
                Method = result.Method,
                PartCount = 0,
            };
            footer.Write(output);
            return result;
        }

        /// <summary>
        /// Builds a multi-part SFX: writes a small <c>[ stub ][ footer ]</c> launcher .exe and
        /// streams the compressed payload into external volumes named
        /// <c>{destBaseName}.001</c>, <c>.002</c>, ... each up to <paramref name="splitSizeBytes"/>.
        /// </summary>
        public static PackResult BuildMultiPart(byte[] stubBytes, string sourcePath, string destExePath,
            long splitSizeBytes, CompressionOptions options, IProgress<long>? progress = null,
            System.Threading.CancellationToken cancel = default)
        {
            ArgumentNullException.ThrowIfNull(stubBytes);
            if (splitSizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(splitSizeBytes));

            string baseName = Path.Combine(
                Path.GetDirectoryName(destExePath) ?? ".",
                Path.GetFileNameWithoutExtension(destExePath));

            PackResult result;
            int partCount;
            using (var chunks = new ChunkedWriteStream(baseName, splitSizeBytes))
            {
                result = ZtarEngine.Pack(sourcePath, chunks, options, progress, cancel);
                chunks.Flush();
                partCount = chunks.PartCount;
            }

            using var output = new FileStream(destExePath, FileMode.Create, FileAccess.Write, FileShare.None);
            output.Write(stubBytes, 0, stubBytes.Length);
            var footer = new ZtarFooter
            {
                Flags = ZtarFlags.MultiPart
                    | (result.Encrypted ? ZtarFlags.Encrypted : ZtarFlags.None)
                    | (result.Precompressed ? ZtarFlags.Precompressed : ZtarFlags.None),
                PayloadOffset = 0,
                PayloadSize = result.CompressedSize,
                OriginalSize = result.OriginalSize,
                Crc32 = result.Crc32,
                WindowLog = result.WindowLog,
                Method = result.Method,
                PartCount = partCount,
            };
            footer.Write(output);
            return result;
        }

        /// <summary>
        /// Opens the compressed payload referenced by <paramref name="footer"/>. For appended
        /// archives this is a window into <paramref name="sfxPath"/>; for multi-part it is the
        /// concatenation of the external volumes next to it.
        /// The returned stream must be disposed by the caller.
        /// </summary>
        public static Stream OpenPayload(string sfxPath, ZtarFooter footer)
        {
            if (footer.IsMultiPart)
            {
                string baseName = Path.Combine(
                    Path.GetDirectoryName(sfxPath) ?? ".",
                    Path.GetFileNameWithoutExtension(sfxPath));
                return new ChunkedReadStream(baseName);
            }

            var fs = new FileStream(sfxPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new SubStream(fs, footer.PayloadOffset, footer.PayloadSize, leaveOpen: false);
        }

        /// <summary>
        /// Reads the Ztar footer from an SFX executable, or returns null when the file is
        /// not an Ztar SFX (no trailing magic).
        /// </summary>
        public static ZtarFooter? ReadFooter(string sfxPath)
        {
            using var fs = new FileStream(sfxPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ZtarFooter.ReadFromEnd(fs);
        }
    }
}
