using System;
using System.IO;

namespace Ztar.Core.Streams
{
    public class ChunkedReadStream : Stream
    {
        private readonly string _baseFilePath;
        private int _currentChunkIndex = 1;
        private FileStream? _currentStream;
        private long _totalPosition = 0;
        private bool _isLegacyNoChunking = false;

        public ChunkedReadStream(string baseFilePath)
        {
            _baseFilePath = baseFilePath;
            if (!OpenNextChunk())
            {
                // Fallback nếu người dùng lưu file thành .bin thay vì .001
                string legacyPath = $"{_baseFilePath}.bin";
                if (File.Exists(legacyPath))
                {
                    _isLegacyNoChunking = true;
                    _currentStream = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                else
                {
                    throw new FileNotFoundException($"Không tìm thấy tệp nén khởi điểm: {_baseFilePath}.001 hoặc .bin");
                }
            }
        }

        private bool OpenNextChunk()
        {
            if (_isLegacyNoChunking) return false;

            if (_currentStream != null)
            {
                _currentStream.Dispose();
                _currentStream = null;
            }

            string ext = _currentChunkIndex.ToString("D3");
            string nextPath = $"{_baseFilePath}.{ext}";

            if (File.Exists(nextPath))
            {
                _currentStream = new FileStream(nextPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _currentChunkIndex++;
                return true;
            }
            return false;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            int currentOffset = offset;
            int bytesToRead = count;

            while (bytesToRead > 0)
            {
                if (_currentStream == null) break; // EOF

                int read = _currentStream.Read(buffer, currentOffset, bytesToRead);
                if (read == 0) // Hết file hiện tại
                {
                    if (!OpenNextChunk()) // Mở file kế tiếp (.002, .003)
                    {
                        break; // Hết toàn bộ chuỗi
                    }
                    continue;
                }

                currentOffset += read;
                bytesRead += read;
                bytesToRead -= read;
                _totalPosition += read;
            }

            return bytesRead;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _totalPosition; set => throw new NotSupportedException(); }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

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
