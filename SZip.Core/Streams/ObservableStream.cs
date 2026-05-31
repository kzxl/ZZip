using System;
using System.IO;

namespace SZip.Core.Streams
{
    /// <summary>
    /// Pass-through stream that observes every byte written to (or read from) the
    /// inner stream: it counts the total, optionally computes a CRC32, and reports
    /// progress. Enables real progress bars and integrity checks without buffering.
    /// </summary>
    public sealed class ObservableStream : Stream
    {
        private readonly Stream _inner;
        private readonly Crc32? _crc;
        private readonly IProgress<long>? _progress;
        private readonly bool _leaveOpen;
        private long _total;
        private long _lastReported;
        private readonly long _reportEvery;

        /// <param name="computeCrc">When true, a running CRC32 is maintained over the bytes.</param>
        /// <param name="progress">Receives cumulative byte counts, throttled by <paramref name="reportEvery"/>.</param>
        /// <param name="reportEvery">Minimum byte delta between progress callbacks.</param>
        public ObservableStream(Stream inner, bool computeCrc = false, IProgress<long>? progress = null,
            long reportEvery = 1 << 20, bool leaveOpen = true)
        {
            _inner = inner;
            _crc = computeCrc ? new Crc32() : null;
            _progress = progress;
            _reportEvery = reportEvery <= 0 ? 1 : reportEvery;
            _leaveOpen = leaveOpen;
        }

        public long BytesObserved => _total;
        public uint Crc => _crc?.Value ?? 0;

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            Observe(buffer.AsSpan(offset, count));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            if (read > 0) Observe(buffer.AsSpan(offset, read));
            return read;
        }

        private void Observe(ReadOnlySpan<byte> data)
        {
            _crc?.Append(data);
            _total += data.Length;
            if (_progress != null && _total - _lastReported >= _reportEvery)
            {
                _lastReported = _total;
                _progress.Report(_total);
            }
        }

        /// <summary>Forces a final progress callback with the current total.</summary>
        public void ReportFinal() => _progress?.Report(_total);

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _total;
        public override long Position { get => _total; set => throw new NotSupportedException(); }

        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
