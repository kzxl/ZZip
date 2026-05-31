using System;
using System.IO;

namespace Ztar.Core.Streams
{
    public class ChunkedWriteStream : Stream
    {
        private readonly string _baseFilePath;
        private readonly long _maxChunkSize;
        private int _currentChunkIndex = 1;
        private FileStream _currentStream = null!;
        private long _bytesWrittenToCurrentChunk = 0;
        private long _totalPosition = 0;

        public ChunkedWriteStream(string baseFilePath, long maxChunkSize)
        {
            _baseFilePath = baseFilePath;
            _maxChunkSize = maxChunkSize;
            OpenNextChunk();
        }

        /// <summary>Number of volume files created so far (.001, .002, ...).</summary>
        public int PartCount => _currentChunkIndex - 1;

        private void OpenNextChunk()
        {
            if (_currentStream != null)
            {
                _currentStream.Flush();
                _currentStream.Close();
                _currentStream.Dispose();
            }

            string ext = _currentChunkIndex.ToString("D3"); // .001, .002
            string newPath = $"{_baseFilePath}.{ext}";
            if (_maxChunkSize <= 0) newPath = $"{_baseFilePath}.bin"; // Không chia nhỏ

            _currentStream = new FileStream(newPath, FileMode.Create, FileAccess.Write, FileShare.None);
            _bytesWrittenToCurrentChunk = 0;
            _currentChunkIndex++;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_maxChunkSize <= 0)
            {
                _currentStream.Write(buffer, offset, count);
                _totalPosition += count;
                return;
            }

            int bytesToWrite = count;
            int currentOffset = offset;

            while (bytesToWrite > 0)
            {
                long spaceLeftInChunk = _maxChunkSize - _bytesWrittenToCurrentChunk;
                if (spaceLeftInChunk == 0)
                {
                    OpenNextChunk();
                    spaceLeftInChunk = _maxChunkSize;
                }

                int toWrite = (int)Math.Min(bytesToWrite, spaceLeftInChunk);
                _currentStream.Write(buffer, currentOffset, toWrite);
                
                _bytesWrittenToCurrentChunk += toWrite;
                _totalPosition += toWrite;
                
                currentOffset += toWrite;
                bytesToWrite -= toWrite;
            }
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _totalPosition;
        public override long Position { get => _totalPosition; set => throw new NotSupportedException(); }

        public override void Flush() => _currentStream?.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _currentStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
