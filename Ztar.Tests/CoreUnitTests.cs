using System.IO;
using Ztar.Core;

namespace Ztar.Tests
{
    public class FooterTests
    {
        [Fact]
        public void RoundTrips_AllFields()
        {
            var footer = new ZtarFooter
            {
                Flags = ZtarFlags.Appended | ZtarFlags.Encrypted | ZtarFlags.Precompressed,
                PayloadOffset = 123456789,
                PayloadSize = 987654321,
                OriginalSize = 555_000_000,
                Crc32 = 0xDEADBEEF,
                WindowLog = 27,
                Method = CompressionMethod.Lzma,
                PartCount = 7,
            };

            using var ms = new MemoryStream();
            // Pad so the footer is the tail of a larger stream (as in a real SFX).
            ms.Write(new byte[1000]);
            footer.Write(ms);

            var read = ZtarFooter.ReadFromEnd(ms);
            Assert.NotNull(read);
            Assert.Equal(footer.PayloadOffset, read!.PayloadOffset);
            Assert.Equal(footer.PayloadSize, read.PayloadSize);
            Assert.Equal(footer.OriginalSize, read.OriginalSize);
            Assert.Equal(footer.Crc32, read.Crc32);
            Assert.Equal(footer.WindowLog, read.WindowLog);
            Assert.Equal(footer.Method, read.Method);
            Assert.Equal(footer.PartCount, read.PartCount);
            Assert.True(read.IsAppended);
            Assert.True(read.IsEncrypted);
            Assert.True(read.IsPrecompressed);
            Assert.False(read.IsMultiPart);
        }

        [Fact]
        public void MagicOccupiesFinalEightBytes()
        {
            var footer = new ZtarFooter { Flags = ZtarFlags.Appended };
            using var ms = new MemoryStream();
            footer.Write(ms);
            byte[] all = ms.ToArray();

            Assert.Equal(ZtarFooter.Size, all.Length);
            byte[] tail = all[^8..];
            // 'Z','T','A','R','S','F','X',0x01
            Assert.Equal(new byte[] { 0x5A, 0x54, 0x41, 0x52, 0x53, 0x46, 0x58, 0x01 }, tail);
        }

        [Fact]
        public void ReturnsNull_WhenNoMagic()
        {
            using var ms = new MemoryStream(new byte[200]);
            Assert.Null(ZtarFooter.ReadFromEnd(ms));
        }

        [Fact]
        public void ReturnsNull_WhenStreamTooSmall()
        {
            using var ms = new MemoryStream(new byte[10]);
            Assert.Null(ZtarFooter.ReadFromEnd(ms));
        }
    }

    public class Crc32Tests
    {
        [Fact]
        public void KnownVector_Check123456789()
        {
            // The standard CRC-32 check value for the ASCII string "123456789" is 0xCBF43926.
            byte[] data = System.Text.Encoding.ASCII.GetBytes("123456789");
            Assert.Equal(0xCBF43926u, Crc32.Compute(data));
        }

        [Fact]
        public void EmptyInput_IsZero()
        {
            Assert.Equal(0u, Crc32.Compute(System.Array.Empty<byte>()));
        }

        [Fact]
        public void Incremental_MatchesOneShot()
        {
            byte[] data = new byte[5000];
            new System.Random(7).NextBytes(data);

            var inc = new Crc32();
            inc.Append(data.AsSpan(0, 1000));
            inc.Append(data.AsSpan(1000, 2500));
            inc.Append(data.AsSpan(3500));

            Assert.Equal(Crc32.Compute(data), inc.Value);
        }
    }

    public class EstimatorTests
    {
        [Fact]
        public void HighlyCompressibleText_PredictsLargeSavings()
        {
            using var ws = new TempWorkspace();
            string f = ws.At("text.txt");
            File.WriteAllText(f, string.Concat(System.Linq.Enumerable.Repeat("compress me ", 50_000)));

            var est = CompressionEstimator.Estimate(f);
            Assert.True(est.SavingsPercent > 50, $"Mong giảm >50%, thực tế {est.SavingsPercent:0.#}%");
            Assert.False(est.AlreadyCompressed);
        }

        [Fact]
        public void RandomData_FlaggedAlreadyCompressed()
        {
            using var ws = new TempWorkspace();
            string f = ws.At("rand.bin");
            byte[] data = new byte[500_000];
            new System.Random(99).NextBytes(data);
            File.WriteAllBytes(f, data);

            var est = CompressionEstimator.Estimate(f);
            Assert.True(est.AlreadyCompressed, $"Dữ liệu ngẫu nhiên phải bị đánh dấu không nén được (giảm {est.SavingsPercent:0.#}%)");
        }
    }
}
