using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Threading;
using SZip.Core.Streams;

namespace SZip.Core
{
    /// <summary>Preset compression profiles surfaced to the UI / CLI.</summary>
    public enum CompressionProfile
    {
        /// <summary>Fast, low effort. zstd level 3.</summary>
        Fast,
        /// <summary>Balanced. zstd level 12, checksum on.</summary>
        Normal,
        /// <summary>Maximum ratio. zstd level 22 + long-distance matching + multi-threading.</summary>
        Ultra,
    }

    /// <summary>
    /// Tunable compression options. <see cref="FromProfile"/> builds sensible defaults
    /// but every field can be overridden (e.g. by the CLI).
    /// </summary>
    public sealed class CompressionOptions
    {
        /// <summary>Codec applied to the payload.</summary>
        public CompressionMethod Method { get; set; } = CompressionMethod.Zstd;
        /// <summary>zstd level, 1..22.</summary>
        public int Level { get; set; } = 19;
        /// <summary>Worker thread count. 0 = single-threaded. -1 = auto (Environment.ProcessorCount).</summary>
        public int Workers { get; set; }
        /// <summary>Enable long-distance matching (great for large/redundant data).</summary>
        public bool LongDistanceMatching { get; set; }
        /// <summary>
        /// zstd windowLog (history window = 2^windowLog bytes). 0 = library default.
        /// Up to 27 is accepted by any decompressor unchanged; 28..31 requires the decompressor
        /// to raise windowLogMax, which SZip does automatically via the footer's WindowLog.
        /// </summary>
        public int WindowLog { get; set; }
        /// <summary>Append a content checksum to the zstd frame (in addition to our CRC32).</summary>
        public bool ContentChecksum { get; set; } = true;
        /// <summary>Optional password. When set, the payload is AES-256 encrypted.</summary>
        public string? Password { get; set; }
        /// <summary>
        /// When true, precompress the TAR via an external precomp tool before codec compression.
        /// Requires <see cref="PrecompService.IsAvailable"/>. Greatly improves ratio on data that
        /// contains already-compressed streams (games, installers, office docs).
        /// </summary>
        public bool UsePrecomp { get; set; }
        /// <summary>Explicit path to precomp.exe; null = auto-detect.</summary>
        public string? PrecompPath { get; set; }
        /// <summary>Extra command-line args passed to precomp during precompression (e.g. "-intense").</summary>
        public string? PrecompExtraArgs { get; set; }

        /// <summary>Max windowLog accepted by any zstd decompressor without raising windowLogMax.</summary>
        public const int MaxSafeWindowLog = 27;
        /// <summary>Absolute max windowLog (2^31 = 2 GiB window) for long-distance mode.</summary>
        public const int MaxLongWindowLog = 31;

        public bool IsEncrypted => !string.IsNullOrEmpty(Password);

        public static CompressionOptions FromProfile(CompressionProfile profile,
            CompressionMethod method = CompressionMethod.Zstd)
        {
            var o = profile switch
            {
                CompressionProfile.Fast => new CompressionOptions
                {
                    Level = 3, Workers = 0, LongDistanceMatching = false, WindowLog = 0,
                },
                CompressionProfile.Normal => new CompressionOptions
                {
                    Level = 12, Workers = -1, LongDistanceMatching = false, WindowLog = 0,
                },
                CompressionProfile.Ultra => new CompressionOptions
                {
                    Level = 22, Workers = -1, LongDistanceMatching = true, WindowLog = MaxSafeWindowLog,
                },
                _ => new CompressionOptions(),
            };
            o.Method = method;
            return o;
        }

        internal int ResolvedWorkers => Workers < 0 ? Environment.ProcessorCount : Workers;
        internal int ResolvedWindowLog => WindowLog <= 0 ? 0 : Math.Min(WindowLog, MaxLongWindowLog);
    }

    /// <summary>
    /// Core archive engine. Packs a file or directory into a TAR stream, compresses it
    /// with zstd, and streams the result to any destination stream. Decompression is the
    /// reverse. No intermediate temp files are used.
    /// </summary>
    public static class SZipEngine
    {
        private const int CopyBufferSize = 1 << 20; // 1 MiB

        /// <summary>
        /// Streams <paramref name="sourcePath"/> (file or directory) as TAR + zstd into
        /// <paramref name="destination"/>. Returns stats describing the written payload.
        /// </summary>
        /// <param name="destination">Where compressed bytes go. Not closed by this method.</param>
        /// <param name="progress">Reports uncompressed bytes processed so far.</param>
        /// <param name="cancel">Cancels the operation mid-stream.</param>
        public static PackResult Pack(string sourcePath, Stream destination, CompressionOptions options,
            IProgress<long>? progress = null, CancellationToken cancel = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(options);

            bool isDir = Directory.Exists(sourcePath);
            bool isFile = File.Exists(sourcePath);
            if (!isDir && !isFile)
                throw new FileNotFoundException("Source path not found.", sourcePath);

            bool precomp = options.UsePrecomp;
            string? precompExe = precomp ? PrecompService.Locate(options.PrecompPath) : null;
            if (precomp && precompExe == null)
                throw new FileNotFoundException("Bật nén sâu nhưng không tìm thấy precomp.exe.");

            // The pipeline: [plaintext producer] -> codec -> (optional AES) -> CRC counter -> destination.
            var crcCounter = new ObservableStream(destination, computeCrc: true, leaveOpen: true);
            Stream encLayer = options.IsEncrypted
                ? PayloadCrypto.CreateEncryptor(crcCounter, options.Password!)
                : crcCounter;
            Stream codec = Codec.WrapCompress(encLayer, options);

            long plaintextSize;
            try
            {
                var meter = new ObservableStream(codec, computeCrc: false, progress: progress, leaveOpen: true, cancel: cancel);
                if (precomp)
                    plaintextSize = ProducePrecompContainer(sourcePath, isDir, precompExe!, options, meter, cancel);
                else
                    plaintextSize = ProduceTar(sourcePath, isDir, meter);
                meter.Flush();
            }
            finally
            {
                codec.Dispose();
                if (!ReferenceEquals(encLayer, crcCounter)) encLayer.Dispose();
                crcCounter.Flush();
            }

            return new PackResult
            {
                CompressedSize = crcCounter.BytesObserved,
                OriginalSize = plaintextSize,
                Crc32 = crcCounter.Crc,
                WindowLog = (byte)options.ResolvedWindowLog,
                Method = options.Method,
                Encrypted = options.IsEncrypted,
                Precompressed = precomp,
            };
        }

        /// <summary>Writes the source as a TAR stream into <paramref name="dest"/>; returns bytes written.</summary>
        private static long ProduceTar(string sourcePath, bool isDir, ObservableStream dest)
        {
            if (isDir)
            {
                TarFile.CreateFromDirectory(sourcePath, dest, includeBaseDirectory: true);
            }
            else
            {
                using var tar = new TarWriter(dest, leaveOpen: true);
                tar.WriteEntry(sourcePath, Path.GetFileName(sourcePath));
            }
            return dest.BytesObserved;
        }

        /// <summary>
        /// Builds a self-contained precomp container and writes it into <paramref name="dest"/>:
        /// <c>[8: precomp.exe length][precomp.exe][.pcf]</c>. Embedding precomp.exe lets the SFX
        /// restore on any machine without it installed. The TAR is precompressed via precomp -cn.
        /// Returns the container size (plaintext fed to the codec).
        /// </summary>
        private static long ProducePrecompContainer(string sourcePath, bool isDir, string precompExe,
            CompressionOptions options, ObservableStream dest, CancellationToken cancel)
        {
            string work = Path.Combine(Path.GetTempPath(), "szip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            string tarPath = Path.Combine(work, "data.tar");
            string pcfPath = Path.Combine(work, "data.pcf");
            try
            {
                // 1) TAR the source to a temp file (precomp needs a real file, can't stream).
                using (var tarFs = new FileStream(tarPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var tarMeter = new ObservableStream(tarFs, computeCrc: false, leaveOpen: true, cancel: cancel))
                {
                    ProduceTar(sourcePath, isDir, tarMeter);
                    tarMeter.Flush();
                }

                cancel.ThrowIfCancellationRequested();
                // 2) precomp the TAR (no internal compression; our codec does the squeezing).
                PrecompService.Precompress(precompExe, tarPath, pcfPath, options.PrecompExtraArgs);

                // 3) Emit container: [exe length][exe bytes][pcf bytes] into the codec pipeline.
                byte[] exeLenBuf = BitConverter.GetBytes((long)new FileInfo(precompExe).Length);
                dest.Write(exeLenBuf, 0, exeLenBuf.Length);
                using (var exeFs = new FileStream(precompExe, FileMode.Open, FileAccess.Read, FileShare.Read))
                    exeFs.CopyTo(dest, CopyBufferSize);
                using (var pcfFs = new FileStream(pcfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    pcfFs.CopyTo(dest, CopyBufferSize);

                return dest.BytesObserved;
            }
            finally
            {
                TryDeleteDir(work);
            }
        }

        /// <summary>
        /// Decompresses a payload stream and extracts the embedded TAR into
        /// <paramref name="destinationDir"/>. <paramref name="compressedSource"/> should yield
        /// exactly the on-disk payload bytes (e.g. a <see cref="SubStream"/>).
        /// </summary>
        /// <param name="method">Codec used at pack time.</param>
        /// <param name="password">Required when the payload was encrypted; otherwise null.</param>
        /// <param name="precompressed">When true, the decompressed payload is a precomp container.</param>
        /// <param name="windowLog">Footer windowLog; lets the zstd decompressor accept large windows (&gt;27).</param>
        /// <param name="cancel">Cancels the operation mid-stream.</param>
        public static void Unpack(Stream compressedSource, string destinationDir, CompressionMethod method,
            string? password = null, bool precompressed = false, IProgress<long>? progress = null,
            int windowLog = 0, CancellationToken cancel = default)
        {
            Directory.CreateDirectory(destinationDir);

            Stream decLayer = password != null
                ? PayloadCrypto.CreateDecryptor(compressedSource, password)
                : compressedSource;
            try
            {
                using Stream codec = Codec.WrapDecompress(decLayer, method, windowLog);
                using var meter = new ObservableStream(codec, computeCrc: false, progress: progress, leaveOpen: true, cancel: cancel);

                if (precompressed)
                    RestorePrecompContainer(meter, destinationDir);
                else
                    TarFile.ExtractToDirectory(meter, destinationDir, overwriteFiles: true);
            }
            finally
            {
                if (password != null) decLayer.Dispose();
            }
        }

        /// <summary>
        /// Reads a precomp container <c>[8: exe length][precomp.exe][.pcf]</c> from
        /// <paramref name="containerStream"/>, restores the original TAR with the embedded precomp,
        /// and extracts it into <paramref name="destinationDir"/>.
        /// </summary>
        private static void RestorePrecompContainer(Stream containerStream, string destinationDir)
        {
            string work = Path.Combine(Path.GetTempPath(), "szip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            string tarPath = Path.Combine(work, "data.tar");
            try
            {
                RestorePrecompToTar(containerStream, tarPath);
                TarFile.ExtractToDirectory(tarPath, destinationDir, overwriteFiles: true);
            }
            finally
            {
                TryDeleteDir(work);
            }
        }

        /// <summary>
        /// Reads a precomp container <c>[8: exe length][precomp.exe][.pcf]</c> from
        /// <paramref name="containerStream"/> and restores the original TAR to <paramref name="tarPath"/>
        /// using the embedded precomp executable.
        /// </summary>
        private static void RestorePrecompToTar(Stream containerStream, string tarPath)
        {
            string work = Path.GetDirectoryName(tarPath)!;
            string exePath = Path.Combine(work, OperatingSystem.IsWindows() ? "precomp.exe" : "precomp");
            string pcfPath = Path.Combine(work, "data.pcf");

            byte[] lenBuf = ReadExactly(containerStream, 8);
            long exeLen = BitConverter.ToInt64(lenBuf, 0);

            // Extract the embedded precomp.exe.
            using (var exeFs = new FileStream(exePath, FileMode.Create, FileAccess.Write, FileShare.None))
                CopyExact(containerStream, exeFs, exeLen);
            // The remainder is the .pcf container.
            using (var pcfFs = new FileStream(pcfPath, FileMode.Create, FileAccess.Write, FileShare.None))
                containerStream.CopyTo(pcfFs, CopyBufferSize);

            PrecompService.Restore(exePath, pcfPath, tarPath);
        }

        private static void CopyExact(Stream src, Stream dst, long count)
        {
            byte[] buf = new byte[CopyBufferSize];
            long remaining = count;
            while (remaining > 0)
            {
                int want = (int)Math.Min(buf.Length, remaining);
                int n = src.Read(buf, 0, want);
                if (n == 0) throw new EndOfStreamException("Container precomp bị cắt cụt.");
                dst.Write(buf, 0, n);
                remaining -= n;
            }
        }

        private static byte[] ReadExactly(Stream s, int count)
        {
            byte[] buf = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buf, read, count - read);
                if (n == 0) throw new EndOfStreamException("Dữ liệu bị cắt cụt.");
                read += n;
            }
            return buf;
        }

        private static void TryDeleteDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* best-effort temp cleanup */ }
        }

        /// <summary>Verifies that the bytes in <paramref name="payload"/> match the expected CRC32.</summary>
        public static bool VerifyCrc(Stream payload, uint expectedCrc)
        {
            var crc = new Crc32();
            byte[] buffer = new byte[CopyBufferSize];
            int read;
            while ((read = payload.Read(buffer, 0, buffer.Length)) > 0)
                crc.Append(buffer.AsSpan(0, read));
            return crc.Value == expectedCrc;
        }

        /// <summary>
        /// Enumerates the entries inside an archive without extracting to disk. For plain archives
        /// this streams the TAR directory; precompressed archives are restored to a temp file first
        /// (precomp output cannot be parsed incrementally).
        /// </summary>
        public static IReadOnlyList<ArchiveEntry> ListEntries(Stream compressedSource, CompressionMethod method,
            string? password = null, bool precompressed = false, int windowLog = 0,
            CancellationToken cancel = default)
        {
            Stream decLayer = password != null
                ? PayloadCrypto.CreateDecryptor(compressedSource, password)
                : compressedSource;
            try
            {
                using Stream codec = Codec.WrapDecompress(decLayer, method, windowLog);
                using var meter = new ObservableStream(codec, computeCrc: false, leaveOpen: true, cancel: cancel);

                if (precompressed)
                {
                    string work = Path.Combine(Path.GetTempPath(), "szip_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(work);
                    string tarPath = Path.Combine(work, "data.tar");
                    try
                    {
                        RestorePrecompToTar(meter, tarPath);
                        using var tarFs = new FileStream(tarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return ReadTarEntries(tarFs);
                    }
                    finally { TryDeleteDir(work); }
                }

                return ReadTarEntries(meter);
            }
            finally
            {
                if (password != null) decLayer.Dispose();
            }
        }

        /// <summary>
        /// Tests archive integrity by fully decompressing (and, if encrypted, authenticating) the
        /// payload to a discard sink. Returns true when everything decodes cleanly. Combine with
        /// <see cref="VerifyCrc"/> for a complete check.
        /// </summary>
        public static bool TestArchive(Stream compressedSource, CompressionMethod method,
            string? password = null, bool precompressed = false, int windowLog = 0,
            IProgress<long>? progress = null, CancellationToken cancel = default)
        {
            Stream decLayer = password != null
                ? PayloadCrypto.CreateDecryptor(compressedSource, password)
                : compressedSource;
            try
            {
                using Stream codec = Codec.WrapDecompress(decLayer, method, windowLog);
                using var meter = new ObservableStream(codec, computeCrc: false, progress: progress, leaveOpen: true, cancel: cancel);

                if (precompressed)
                {
                    string work = Path.Combine(Path.GetTempPath(), "szip_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(work);
                    string tarPath = Path.Combine(work, "data.tar");
                    try
                    {
                        RestorePrecompToTar(meter, tarPath);
                        // Walk the restored TAR to confirm it parses.
                        using var tarFs = new FileStream(tarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        ReadTarEntries(tarFs);
                    }
                    finally { TryDeleteDir(work); }
                }
                else
                {
                    meter.CopyTo(Stream.Null, CopyBufferSize);
                }
                return true;
            }
            finally
            {
                if (password != null) decLayer.Dispose();
            }
        }

        private static List<ArchiveEntry> ReadTarEntries(Stream tarStream)
        {
            var entries = new List<ArchiveEntry>();
            using var reader = new TarReader(tarStream, leaveOpen: true);
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) != null)
            {
                bool isDir = entry.EntryType is TarEntryType.Directory;
                entries.Add(new ArchiveEntry(entry.Name, isDir ? -1 : entry.Length, isDir));
            }
            return entries;
        }
    }

    /// <summary>Outcome of a <see cref="SZipEngine.Pack"/> call.</summary>
    public sealed class PackResult
    {
        public long CompressedSize { get; init; }
        public long OriginalSize { get; init; }
        public uint Crc32 { get; init; }
        public byte WindowLog { get; init; }
        public CompressionMethod Method { get; init; }
        public bool Encrypted { get; init; }
        public bool Precompressed { get; init; }

        public double Ratio => OriginalSize > 0 ? (double)CompressedSize / OriginalSize : 0;
    }

    /// <summary>One file or directory entry inside an archive, as reported by <see cref="SZipEngine.ListEntries"/>.</summary>
    /// <param name="Name">Path of the entry within the archive.</param>
    /// <param name="Size">Uncompressed size in bytes; -1 for directories.</param>
    /// <param name="IsDirectory">True when the entry is a directory.</param>
    public readonly record struct ArchiveEntry(string Name, long Size, bool IsDirectory);
}
