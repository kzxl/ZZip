using System;
using System.IO;
using System.IO.Compression;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;
using ZstdSharp;
using ZstdSharp.Unsafe;
using LzmaMode = SharpCompress.Compressors.CompressionMode;

namespace SZip.Core
{
    /// <summary>
    /// Wraps a raw stream with the codec selected in <see cref="CompressionOptions.Method"/>.
    /// Each codec produces a self-describing container so decompression needs only the method id.
    /// </summary>
    internal static class Codec
    {
        /// <summary>
        /// Returns a stream that compresses everything written to it into <paramref name="destination"/>.
        /// Disposing the returned stream finalizes the codec frame but leaves <paramref name="destination"/> open.
        /// </summary>
        public static Stream WrapCompress(Stream destination, CompressionOptions o)
        {
            switch (o.Method)
            {
                case CompressionMethod.Zstd:
                    return BuildZstd(destination, o);

                case CompressionMethod.Lzma:
                    // LZip = LZMA with a self-describing header/footer (stores dict size, CRC of size).
                    return LZipStream.Create(destination, LzmaMode.Compress, leaveOpen: true);

                case CompressionMethod.Brotli:
                    return new BrotliStream(destination, MapBrotliLevel(o.Level), leaveOpen: true);

                case CompressionMethod.Store:
                    return new LeaveOpenWrapper(destination);

                default:
                    throw new NotSupportedException($"Unknown method {o.Method}.");
            }
        }

        /// <summary>
        /// Returns a stream that decompresses bytes pulled from <paramref name="source"/> using
        /// <paramref name="method"/>. Leaves <paramref name="source"/> open on dispose.
        /// </summary>
        public static Stream WrapDecompress(Stream source, CompressionMethod method)
        {
            switch (method)
            {
                case CompressionMethod.Zstd:
                    var dec = new Decompressor();
                    return new DecompressionStream(source, dec, bufferSize: 0,
                        checkEndOfStream: false, preserveDecompressor: false, leaveOpen: true);

                case CompressionMethod.Lzma:
                    return LZipStream.Create(source, LzmaMode.Decompress, leaveOpen: true);

                case CompressionMethod.Brotli:
                    return new BrotliStream(source, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true);

                case CompressionMethod.Store:
                    return new LeaveOpenWrapper(source);

                default:
                    throw new NotSupportedException($"Unknown method {method}.");
            }
        }

        private static Stream BuildZstd(Stream destination, CompressionOptions o)
        {
            int level = Math.Clamp(o.Level, 1, Compressor.MaxCompressionLevel);
            var c = new Compressor(level);

            if (o.LongDistanceMatching)
                c.SetParameter(ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching, 1);
            int wlog = o.ResolvedWindowLog;
            if (wlog > 0)
                c.SetParameter(ZSTD_cParameter.ZSTD_c_windowLog, wlog);
            c.SetParameter(ZSTD_cParameter.ZSTD_c_checksumFlag, o.ContentChecksum ? 1 : 0);
            int workers = o.ResolvedWorkers;
            if (workers > 0)
                c.SetParameter(ZSTD_cParameter.ZSTD_c_nbWorkers, workers);

            // preserveCompressor:false -> the stream disposes the Compressor for us.
            return new CompressionStream(destination, c, bufferSize: 0, preserveCompressor: false, leaveOpen: true);
        }

        /// <summary>Maps a zstd-style level (1..22) onto a .NET CompressionLevel for Brotli.</summary>
        private static CompressionLevel MapBrotliLevel(int level)
        {
            if (level <= 1) return CompressionLevel.Fastest;
            if (level >= 18) return CompressionLevel.SmallestSize; // Brotli quality 11
            return CompressionLevel.Optimal;
        }

        /// <summary>Pass-through used by the Store method; never closes the inner stream.</summary>
        private sealed class LeaveOpenWrapper : Stream
        {
            private readonly Stream _inner;
            public LeaveOpenWrapper(Stream inner) => _inner = inner;
            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] b, int o, int c) => _inner.Read(b, o, c);
            public override void Write(byte[] b, int o, int c) => _inner.Write(b, o, c);
            public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
            public override void SetLength(long v) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { /* leave inner open */ }
        }
    }
}
