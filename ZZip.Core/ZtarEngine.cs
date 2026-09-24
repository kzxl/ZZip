using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Threading;
using ZeroCompression.Core;
using ZeroCompression.Core.Buffers;
using ZeroCompression.Core.Crypto;
using ZeroCompression.Core.Hashing;
using ZeroCompression.Core.Streams;

namespace ZZip.Core
{


    /// <summary>
    /// Core archive engine. Packs a file or directory into a TAR stream, compresses it
    /// with sovereign codecs, and streams the result to any destination stream.
    /// Supports single-pass streaming unpack with on-the-fly CRC32 verification.
    /// </summary>
    public static class ZtarEngine
    {
        private const int CopyBufferSize = 1 << 20; // 1 MiB

        /// <summary>
        /// Streams <paramref name="sourcePath"/> (file or directory) as TAR + codec into
        /// <paramref name="destination"/>. Returns stats describing the written payload.
        /// </summary>
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
                throw new FileNotFoundException("Precomp deep compression is enabled, but precomp.exe was not found.");

            var crcCounter = new ObservableStream(destination, computeCrc: true, leaveOpen: true);
            Stream encLayer = options.IsEncrypted
                ? PayloadCrypto.CreateEncryptor(crcCounter, options.Password!)
                : crcCounter;
            Stream codec = CodecRegistry.WrapCompress(encLayer, options);

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

        private static long ProducePrecompContainer(string sourcePath, bool isDir, string precompExe,
            CompressionOptions options, ObservableStream dest, CancellationToken cancel)
        {
            string work = Path.Combine(Path.GetTempPath(), "ztar_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            string tarPath = Path.Combine(work, "data.tar");
            string pcfPath = Path.Combine(work, "data.pcf");
            try
            {
                using (var tarFs = new FileStream(tarPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var tarMeter = new ObservableStream(tarFs, computeCrc: false, leaveOpen: true, cancel: cancel))
                {
                    ProduceTar(sourcePath, isDir, tarMeter);
                    tarMeter.Flush();
                }

                cancel.ThrowIfCancellationRequested();
                PrecompService.Precompress(precompExe, tarPath, pcfPath, options.PrecompExtraArgs);

                byte[] exeLenBuf = BitConverter.GetBytes((long)new FileInfo(precompExe).Length);
                dest.Write(exeLenBuf, 0, exeLenBuf.Length);

                using var pooled = BufferPool.Scope(CopyBufferSize);
                byte[] buffer = pooled.Array;

                using (var exeFs = new FileStream(precompExe, FileMode.Open, FileAccess.Read, FileShare.Read))
                    CopyStream(exeFs, dest, buffer);
                using (var pcfFs = new FileStream(pcfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    CopyStream(pcfFs, dest, buffer);

                return dest.BytesObserved;
            }
            finally
            {
                TryDeleteDir(work);
            }
        }

        /// <summary>
        /// Decompresses a payload stream and extracts the embedded TAR into
        /// <paramref name="destinationDir"/>.
        /// </summary>
        public static void Unpack(Stream compressedSource, string destinationDir, CompressionMethod method,
            string? password = null, bool precompressed = false, IProgress<long>? progress = null,
            int windowLog = 0, CancellationToken cancel = default)
            => UnpackVerified(compressedSource, destinationDir, method, password, precompressed, progress, windowLog, expectedCrc: null, cancel);

        /// <summary>
        /// Decompresses a payload stream and extracts the embedded TAR into
        /// <paramref name="destinationDir"/>. Supports single-pass CRC32 verification on the fly.
        /// </summary>
        public static void UnpackVerified(Stream compressedSource, string destinationDir, CompressionMethod method,
            string? password = null, bool precompressed = false, IProgress<long>? progress = null,
            int windowLog = 0, uint? expectedCrc = null, CancellationToken cancel = default)
        {
            Directory.CreateDirectory(destinationDir);

            // In Single-Pass mode, we observe the compressed bytes and compute CRC32 on-the-fly.
            var crcMeter = new ObservableStream(compressedSource, computeCrc: expectedCrc.HasValue, leaveOpen: true, cancel: cancel);
            Stream decLayer = password != null
                ? PayloadCrypto.CreateDecryptor(crcMeter, password)
                : crcMeter;
            try
            {
                using Stream codec = CodecRegistry.WrapDecompress(decLayer, method, windowLog);
                using var meter = new ObservableStream(codec, computeCrc: false, progress: progress, leaveOpen: true, cancel: cancel);

                if (precompressed)
                    RestorePrecompContainer(meter, destinationDir);
                else
                    TarFile.ExtractToDirectory(meter, destinationDir, overwriteFiles: true);

                if (expectedCrc.HasValue && crcMeter.Crc != expectedCrc.Value)
                {
                    throw new InvalidDataException(
                        $"Compressed payload is corrupted (CRC32 mismatch: received 0x{crcMeter.Crc:X8}, expected 0x{expectedCrc.Value:X8}). Archive may be corrupted.");
                }
            }
            finally
            {
                if (password != null) decLayer.Dispose();
            }
        }

        /// <summary>
        /// Decompresses a payload stream and extracts either all files or a specified subset of entries into
        /// <paramref name="destinationDir"/>.
        /// </summary>
        public static void ExtractSelected(Stream compressedSource, string destinationDir, ISet<string>? selectedNames,
            CompressionMethod method, string? password = null, bool precompressed = false,
            IProgress<long>? progress = null, int windowLog = 0, CancellationToken cancel = default)
        {
            Directory.CreateDirectory(destinationDir);

            Stream decLayer = password != null
                ? PayloadCrypto.CreateDecryptor(compressedSource, password)
                : compressedSource;
            try
            {
                using Stream codec = CodecRegistry.WrapDecompress(decLayer, method, windowLog);
                using var meter = new ObservableStream(codec, computeCrc: false, progress: progress, leaveOpen: true, cancel: cancel);

                if (precompressed)
                {
                    string work = Path.Combine(Path.GetTempPath(), "ztar_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(work);
                    string tarPath = Path.Combine(work, "data.tar");
                    try
                    {
                        RestorePrecompToTar(meter, tarPath);
                        using var tarFs = new FileStream(tarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        ExtractTarEntries(tarFs, destinationDir, selectedNames, cancel);
                    }
                    finally { TryDeleteDir(work); }
                }
                else
                {
                    ExtractTarEntries(meter, destinationDir, selectedNames, cancel);
                }
            }
            finally
            {
                if (password != null) decLayer.Dispose();
            }
        }

        private static void ExtractTarEntries(Stream tarStream, string destinationDir, ISet<string>? selectedNames, CancellationToken cancel)
        {
            using var reader = new TarReader(tarStream, leaveOpen: true);
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) != null)
            {
                cancel.ThrowIfCancellationRequested();
                if (selectedNames == null || selectedNames.Contains(entry.Name) || selectedNames.Any(s => entry.Name.StartsWith(s.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    string fullPath = Path.Combine(destinationDir, entry.Name);
                    if (entry.EntryType is TarEntryType.Directory)
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                    else
                    {
                        string? dir = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        entry.ExtractToFile(fullPath, overwrite: true);
                    }
                }
            }
        }

        private static void RestorePrecompContainer(Stream containerStream, string destinationDir)
        {
            string work = Path.Combine(Path.GetTempPath(), "ztar_" + Guid.NewGuid().ToString("N"));
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

        private static void RestorePrecompToTar(Stream containerStream, string tarPath)
        {
            string work = Path.GetDirectoryName(tarPath)!;
            string exePath = Path.Combine(work, OperatingSystem.IsWindows() ? "precomp.exe" : "precomp");
            string pcfPath = Path.Combine(work, "data.pcf");

            byte[] lenBuf = ReadExactly(containerStream, 8);
            long exeLen = BitConverter.ToInt64(lenBuf, 0);

            using (var exeFs = new FileStream(exePath, FileMode.Create, FileAccess.Write, FileShare.None))
                CopyExact(containerStream, exeFs, exeLen);

            using var pooled = BufferPool.Scope(CopyBufferSize);
            using (var pcfFs = new FileStream(pcfPath, FileMode.Create, FileAccess.Write, FileShare.None))
                CopyStream(containerStream, pcfFs, pooled.Array);

            PrecompService.Restore(exePath, pcfPath, tarPath);
        }

        private static void CopyExact(Stream src, Stream dst, long count)
        {
            using var pooled = BufferPool.Scope(CopyBufferSize);
            byte[] buf = pooled.Array;
            long remaining = count;
            while (remaining > 0)
            {
                int want = (int)Math.Min(buf.Length, remaining);
                int n = src.Read(buf, 0, want);
                if (n == 0) throw new EndOfStreamException("Precomp container is truncated.");
                dst.Write(buf, 0, n);
                remaining -= n;
            }
        }

        private static void CopyStream(Stream src, Stream dst, byte[] buffer)
        {
            int read;
            while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
            {
                dst.Write(buffer, 0, read);
            }
        }

        private static byte[] ReadExactly(Stream s, int count)
        {
            byte[] buf = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buf, read, count - read);
                if (n == 0) throw new EndOfStreamException("Data is truncated.");
                read += n;
            }
            return buf;
        }

        private static void TryDeleteDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* best-effort temp cleanup */ }
        }

        public static bool VerifyCrc(Stream payload, uint expectedCrc)
        {
            var crc = new Crc32();
            using var pooled = BufferPool.Scope(CopyBufferSize);
            byte[] buffer = pooled.Array;
            int read;
            while ((read = payload.Read(buffer, 0, buffer.Length)) > 0)
                crc.Append(buffer.AsSpan(0, read));
            return crc.Value == expectedCrc;
        }

        public static IReadOnlyList<ArchiveEntry> ListEntries(Stream compressedSource, CompressionMethod method,
            string? password = null, bool precompressed = false, int windowLog = 0,
            CancellationToken cancel = default)
        {
            Stream decLayer = password != null
                ? PayloadCrypto.CreateDecryptor(compressedSource, password)
                : compressedSource;
            try
            {
                using Stream codec = CodecRegistry.WrapDecompress(decLayer, method, windowLog);
                using var meter = new ObservableStream(codec, computeCrc: false, leaveOpen: true, cancel: cancel);

                if (precompressed)
                {
                    string work = Path.Combine(Path.GetTempPath(), "ztar_" + Guid.NewGuid().ToString("N"));
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

        public static bool TestArchive(Stream compressedSource, CompressionMethod method,
            string? password = null, bool precompressed = false, int windowLog = 0,
            IProgress<long>? progress = null, CancellationToken cancel = default)
        {
            Stream decLayer = password != null
                ? PayloadCrypto.CreateDecryptor(compressedSource, password)
                : compressedSource;
            try
            {
                using Stream codec = CodecRegistry.WrapDecompress(decLayer, method, windowLog);
                using var meter = new ObservableStream(codec, computeCrc: false, progress: progress, leaveOpen: true, cancel: cancel);

                if (precompressed)
                {
                    string work = Path.Combine(Path.GetTempPath(), "ztar_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(work);
                    string tarPath = Path.Combine(work, "data.tar");
                    try
                    {
                        RestorePrecompToTar(meter, tarPath);
                        using var tarFs = new FileStream(tarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        ReadTarEntries(tarFs);
                    }
                    finally { TryDeleteDir(work); }
                }
                else
                {
                    using var pooled = BufferPool.Scope(CopyBufferSize);
                    byte[] buf = pooled.Array;
                    int r;
                    while ((r = meter.Read(buf, 0, buf.Length)) > 0) { }
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
                entries.Add(new ArchiveEntry(entry.Name, isDir ? -1 : entry.Length, isDir, entry.ModificationTime));
            }
            return entries;
        }
    }

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

    public readonly record struct ArchiveEntry(string Name, long Size, bool IsDirectory, DateTimeOffset ModificationTime = default);
}
