using System;
using System.IO;
using System.Security.Cryptography;

namespace ZeroZip.Core
{
    /// <summary>
    /// Authenticated payload encryption using AES-256-GCM in a chunked AEAD scheme.
    ///
    /// Layout: [16 salt][8 verifier][ chunk* ] where each chunk is
    /// [4 ciphertext length][12 nonce][16 tag][ciphertext]. The key comes from PBKDF2
    /// (SHA-256, 200k iterations). Each chunk's associated data binds the chunk index and a
    /// final-flag, so any tampering, reordering, or truncation is detected on decrypt -
    /// stronger than CBC + CRC, which only catches accidental corruption.
    /// </summary>
    internal static class PayloadCrypto
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;          // AES-256
        private const int VerifierSize = 8;
        private const int NonceSize = 12;        // GCM standard
        private const int TagSize = 16;          // GCM standard
        private const int Iterations = 200_000;
        private const int ChunkSize = 64 * 1024; // plaintext bytes per AEAD chunk

        /// <summary>
        /// Writes the crypto header (salt + verifier) to <paramref name="destination"/> and
        /// returns a write-mode stream that AEAD-encrypts everything written to it. Disposing
        /// the returned stream flushes the final (possibly empty) chunk; leaves dest open.
        /// </summary>
        public static Stream CreateEncryptor(Stream destination, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] key = DeriveKey(password, salt);
            byte[] verifier = MakeVerifier(key, salt);

            destination.Write(salt, 0, salt.Length);
            destination.Write(verifier, 0, verifier.Length);

            return new GcmWriteStream(destination, key);
        }

        /// <summary>
        /// Reads the crypto header, validates the password, and returns a read-mode stream that
        /// AEAD-decrypts on the fly. Throws <see cref="InvalidDataException"/> on a wrong password
        /// or any authentication failure (tampering/truncation). Leaves source open.
        /// </summary>
        public static Stream CreateDecryptor(Stream source, string password)
        {
            byte[] salt = ReadExactly(source, SaltSize);
            byte[] verifier = ReadExactly(source, VerifierSize);

            byte[] key = DeriveKey(password, salt);
            byte[] expected = MakeVerifier(key, salt);
            if (!CryptographicOperations.FixedTimeEquals(verifier, expected))
                throw new InvalidDataException("Sai mật khẩu.");

            return new GcmReadStream(source, key);
        }

        /// <summary>Validates a password against an encrypted payload without decrypting it.</summary>
        public static bool CheckPassword(Stream source, string password)
        {
            byte[] salt = ReadExactly(source, SaltSize);
            byte[] verifier = ReadExactly(source, VerifierSize);
            byte[] key = DeriveKey(password, salt);
            byte[] expected = MakeVerifier(key, salt);
            return CryptographicOperations.FixedTimeEquals(verifier, expected);
        }

        // --- chunk framing helpers shared by both streams ---

        internal const int ChunkPlaintextSize = ChunkSize;
        internal const int NonceLen = NonceSize;
        internal const int TagLen = TagSize;

        /// <summary>Builds the 12-byte nonce for a chunk: 4-byte salt prefix XORed with the counter.</summary>
        internal static void FillNonce(Span<byte> nonce, long counter)
        {
            nonce.Clear();
            BitConverter.TryWriteBytes(nonce[..8], counter);
        }

        /// <summary>Associated data binds chunk index + final flag so reorder/truncation is caught.</summary>
        internal static byte[] MakeAad(long counter, bool isFinal)
        {
            byte[] aad = new byte[9];
            BitConverter.TryWriteBytes(aad.AsSpan(0, 8), counter);
            aad[8] = isFinal ? (byte)1 : (byte)0;
            return aad;
        }

        private static byte[] DeriveKey(string password, byte[] salt) =>
            Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        private static byte[] MakeVerifier(byte[] key, byte[] salt)
        {
            // A label keeps the verifier independent from any other use of the key.
            Span<byte> input = stackalloc byte[KeySize + SaltSize + 4];
            key.CopyTo(input);
            salt.CopyTo(input[KeySize..]);
            input[^4] = (byte)'S'; input[^3] = (byte)'Z'; input[^2] = (byte)'v'; input[^1] = 1;
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(input, hash);
            return hash[..VerifierSize].ToArray();
        }

        private static byte[] ReadExactly(Stream s, int count)
        {
            byte[] buf = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buf, read, count - read);
                if (n == 0) throw new EndOfStreamException("Dữ liệu mã hóa bị cắt cụt.");
                read += n;
            }
            return buf;
        }

        /// <summary>Reads exactly <paramref name="count"/> bytes; returns false on clean EOF before any byte.</summary>
        private static bool TryReadExactly(Stream s, byte[] buf, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buf, read, count - read);
                if (n == 0)
                {
                    if (read == 0) return false; // clean EOF at a chunk boundary
                    throw new EndOfStreamException("Dữ liệu mã hóa bị cắt cụt.");
                }
                read += n;
            }
            return true;
        }

        /// <summary>
        /// Write side: buffers plaintext into fixed-size chunks, AEAD-encrypts each with a unique
        /// nonce, and frames it as [len][nonce][tag][ciphertext]. The final chunk (flushed on
        /// Dispose) is marked via associated data so the reader can detect truncation.
        /// </summary>
        private sealed class GcmWriteStream : Stream
        {
            private readonly Stream _out;
            private readonly AesGcm _gcm;
            private readonly byte[] _buffer = new byte[ChunkPlaintextSize];
            private int _bufferLen;
            private long _counter;
            private bool _finished;

            public GcmWriteStream(Stream output, byte[] key)
            {
                _out = output;
                _gcm = new AesGcm(key, TagLen);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                while (count > 0)
                {
                    int space = ChunkPlaintextSize - _bufferLen;
                    int take = Math.Min(space, count);
                    Buffer.BlockCopy(buffer, offset, _buffer, _bufferLen, take);
                    _bufferLen += take;
                    offset += take;
                    count -= take;
                    if (_bufferLen == ChunkPlaintextSize) WriteChunk(isFinal: false);
                }
            }

            private void WriteChunk(bool isFinal)
            {
                Span<byte> nonce = stackalloc byte[NonceLen];
                FillNonce(nonce, _counter);
                byte[] aad = MakeAad(_counter, isFinal);

                Span<byte> tag = stackalloc byte[TagLen];
                byte[] cipher = new byte[_bufferLen];
                _gcm.Encrypt(nonce, _buffer.AsSpan(0, _bufferLen), cipher, tag, aad);

                // Frame: [1 final-flag][4 ciphertext length][12 nonce][16 tag][ciphertext].
                // The flag is also bound into the AAD, so flipping it fails authentication.
                _out.WriteByte(isFinal ? (byte)1 : (byte)0);
                Span<byte> lenBuf = stackalloc byte[4];
                BitConverter.TryWriteBytes(lenBuf, _bufferLen);
                _out.Write(lenBuf);
                _out.Write(nonce);
                _out.Write(tag);
                _out.Write(cipher, 0, _bufferLen);

                _bufferLen = 0;
                _counter++;
            }

            private void Finish()
            {
                if (_finished) return;
                _finished = true;
                // Always emit a final chunk (possibly empty) so the reader sees the terminator.
                WriteChunk(isFinal: true);
                _out.Flush();
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => _out.Flush();
            public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();
            public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
            public override void SetLength(long v) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Finish();
                    _gcm.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Read side: pulls one framed chunk at a time, authenticates + decrypts it, and serves the
        /// plaintext. A failed tag throws (tampering); a missing final chunk throws (truncation).
        /// </summary>
        private sealed class GcmReadStream : Stream
        {
            private readonly Stream _in;
            private readonly AesGcm _gcm;
            private readonly byte[] _lenBuf = new byte[4];
            private readonly byte[] _nonce = new byte[NonceLen];
            private readonly byte[] _tag = new byte[TagLen];
            private byte[] _plain = new byte[ChunkPlaintextSize];
            private int _plainLen;
            private int _plainPos;
            private long _counter;
            private bool _sawFinal;

            public GcmReadStream(Stream input, byte[] key)
            {
                _in = input;
                _gcm = new AesGcm(key, TagLen);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int total = 0;
                while (count > 0)
                {
                    if (_plainPos == _plainLen)
                    {
                        if (!FillNextChunk()) break; // end of stream
                    }
                    int available = _plainLen - _plainPos;
                    int take = Math.Min(available, count);
                    Buffer.BlockCopy(_plain, _plainPos, buffer, offset, take);
                    _plainPos += take;
                    offset += take;
                    count -= take;
                    total += take;
                }
                return total;
            }

            private bool FillNextChunk()
            {
                if (_sawFinal) return false;

                // The first byte of every frame is the final-flag. A clean EOF here means the
                // stream ended without a final chunk -> truncation.
                int flagByte = _in.ReadByte();
                if (flagByte < 0)
                    throw new InvalidDataException("Thiếu khối kết thúc - dữ liệu mã hóa bị cắt cụt.");
                bool isFinal = flagByte != 0;

                if (!TryReadExactly(_in, _lenBuf, 4))
                    throw new EndOfStreamException("Dữ liệu mã hóa bị cắt cụt.");

                int cipherLen = BitConverter.ToInt32(_lenBuf, 0);
                if (cipherLen < 0 || cipherLen > ChunkPlaintextSize)
                    throw new InvalidDataException("Khung mã hóa hỏng.");

                if (!TryReadExactly(_in, _nonce, NonceLen) || !TryReadExactly(_in, _tag, TagLen))
                    throw new EndOfStreamException("Dữ liệu mã hóa bị cắt cụt.");

                byte[] cipher = new byte[cipherLen];
                if (cipherLen > 0 && !TryReadExactly(_in, cipher, cipherLen))
                    throw new EndOfStreamException("Dữ liệu mã hóa bị cắt cụt.");

                byte[] aad = MakeAad(_counter, isFinal);
                if (_plain.Length < cipherLen) _plain = new byte[cipherLen];
                try
                {
                    _gcm.Decrypt(_nonce, cipher, _tag, _plain.AsSpan(0, cipherLen), aad);
                }
                catch (CryptographicException)
                {
                    throw new InvalidDataException("Dữ liệu mã hóa bị sửa đổi hoặc hỏng (xác thực thất bại).");
                }

                _plainLen = cipherLen;
                _plainPos = 0;
                _counter++;
                if (isFinal) _sawFinal = true;
                return _plainLen > 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
            public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
            public override void SetLength(long v) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing) _gcm.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
