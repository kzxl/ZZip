using System;
using System.IO;
using System.Security.Cryptography;

namespace SZip.Core
{
    /// <summary>
    /// AES-256-CBC payload encryption. The key is derived from a password via PBKDF2
    /// (SHA-256, 200k iterations). A small header (salt + IV + password verifier) is written
    /// in front of the ciphertext so wrong passwords are detected before decompression.
    /// </summary>
    internal static class PayloadCrypto
    {
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int KeySize = 32;          // AES-256
        private const int VerifierSize = 8;
        private const int Iterations = 200_000;

        /// <summary>Total bytes of the crypto header written before the ciphertext.</summary>
        public const int HeaderSize = SaltSize + IvSize + VerifierSize; // 40

        /// <summary>
        /// Writes the crypto header to <paramref name="destination"/> and returns a write-mode
        /// CryptoStream. Disposing the returned stream flushes the final AES block but leaves
        /// <paramref name="destination"/> open.
        /// </summary>
        public static CryptoStream CreateEncryptor(Stream destination, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] iv = RandomNumberGenerator.GetBytes(IvSize);
            byte[] key = DeriveKey(password, salt);
            byte[] verifier = MakeVerifier(key, salt);

            destination.Write(salt, 0, salt.Length);
            destination.Write(iv, 0, iv.Length);
            destination.Write(verifier, 0, verifier.Length);

            var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var encryptor = aes.CreateEncryptor();
            return new CryptoStream(destination, encryptor, CryptoStreamMode.Write, leaveOpen: true);
        }

        /// <summary>
        /// Reads the crypto header from <paramref name="source"/>, validates the password, and
        /// returns a read-mode CryptoStream yielding the plaintext. Throws
        /// <see cref="InvalidDataException"/> when the password is wrong.
        /// </summary>
        public static CryptoStream CreateDecryptor(Stream source, string password)
        {
            byte[] salt = ReadExactly(source, SaltSize);
            byte[] iv = ReadExactly(source, IvSize);
            byte[] verifier = ReadExactly(source, VerifierSize);

            byte[] key = DeriveKey(password, salt);
            byte[] expected = MakeVerifier(key, salt);
            if (!CryptographicOperations.FixedTimeEquals(verifier, expected))
                throw new InvalidDataException("Sai mật khẩu.");

            var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var decryptor = aes.CreateDecryptor();
            return new CryptoStream(source, decryptor, CryptoStreamMode.Read, leaveOpen: true);
        }

        /// <summary>Validates a password against an encrypted payload without decrypting it.</summary>
        public static bool CheckPassword(Stream source, string password)
        {
            byte[] salt = ReadExactly(source, SaltSize);
            ReadExactly(source, IvSize); // skip IV
            byte[] verifier = ReadExactly(source, VerifierSize);
            byte[] key = DeriveKey(password, salt);
            byte[] expected = MakeVerifier(key, salt);
            return CryptographicOperations.FixedTimeEquals(verifier, expected);
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
    }
}
