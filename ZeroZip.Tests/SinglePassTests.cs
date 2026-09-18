using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroCompression.Core;
using ZeroZip.Core;

namespace ZeroZip.Tests
{
    public class SinglePassTests
    {
        [Fact]
        public void UnpackVerified_DetectsCorruptedPayload_OnTheFly()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string destTar = ws.At("archive.bin");

            var opt = CompressionOptions.FromProfile(CompressionProfile.Fast, CompressionMethod.Zstd);
            using (var fs = File.Create(destTar))
            {
                var packResult = ZtarEngine.Pack(src, fs, opt);

                // Now attempt unpacking with a deliberate WRONG expected CRC
                fs.Position = 0;
                string extractDir = ws.At("extracted");
                uint fakeCrc = packResult.Crc32 ^ 0xFFFFFFFF;

                var ex = Assert.Throws<InvalidDataException>(() =>
                {
                    ZtarEngine.UnpackVerified(fs, extractDir, CompressionMethod.Zstd,
                        password: null, precompressed: false, progress: null, windowLog: 0,
                        expectedCrc: fakeCrc);
                });

                Assert.Contains("CRC32 mismatch", ex.Message);
            }
        }

        [Fact]
        public void InstantPasswordCheck_ValidatesInMilliseconds()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string destTar = ws.At("encrypted.bin");

            var opt = CompressionOptions.FromProfile(CompressionProfile.Fast, CompressionMethod.Zstd);
            opt.Password = "SuperSecret@2026";

            using (var fs = File.Create(destTar))
            {
                ZtarEngine.Pack(src, fs, opt);
            }

            using (var fs = File.OpenRead(destTar))
            {
                // Instant check returns true for correct password
                Assert.True(PayloadCrypto.CheckPassword(fs, "SuperSecret@2026"));
                // Instant check returns false for wrong password
                Assert.False(PayloadCrypto.CheckPassword(fs, "WrongPassword"));
            }
        }

        [Fact]
        public void Adaptive_AutoDetect_RoutesCorrectly()
        {
            using var ws = new TempWorkspace();
            string jsonFile = ws.At("data.json");
            File.WriteAllText(jsonFile, "{\"name\":\"test\",\"version\":1.0,\"items\":[1,2,3],\"key\":\"val\"}");

            var opt = CompressionOptions.AutoDetect(jsonFile);
            Assert.Equal(CompressionMethod.Zstd, opt.Method);
            Assert.True(opt.LongDistanceMatching);
        }
    }
}
