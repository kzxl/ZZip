using System.IO;
using System.Linq;
using Ztar.Core;

namespace Ztar.Tests
{
    /// <summary>
    /// End-to-end pack/unpack through SfxComposer for every codec, encryption, and layout.
    /// A small dummy stub stands in for the real extractor executable (the engine does not
    /// execute it; it only prepends the bytes and reads the footer back).
    /// </summary>
    public class RoundtripTests
    {
        private static readonly byte[] DummyStub = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();

        [Theory]
        [InlineData(CompressionMethod.Zstd)]
        [InlineData(CompressionMethod.Lzma)]
        [InlineData(CompressionMethod.Brotli)]
        [InlineData(CompressionMethod.Store)]
        public void Appended_AllCodecs_RoundTrip(CompressionMethod method)
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string exe = ws.At("out.exe");

            var options = CompressionOptions.FromProfile(CompressionProfile.Normal, method);
            var result = SfxComposer.BuildAppended(DummyStub, src, exe, options);

            Assert.True(result.OriginalSize > 0);
            Assert.True(result.CompressedSize > 0);

            var footer = SfxComposer.ReadFooter(exe);
            Assert.NotNull(footer);
            Assert.Equal(method, footer!.Method);
            Assert.True(footer.IsAppended);
            Assert.False(footer.IsEncrypted);

            // CRC over the on-disk payload must match the footer.
            using (var payload = SfxComposer.OpenPayload(exe, footer))
                Assert.True(ZtarEngine.VerifyCrc(payload, footer.Crc32));

            string outDir = ws.At("ex");
            using (var payload = SfxComposer.OpenPayload(exe, footer))
                ZtarEngine.Unpack(payload, outDir, footer.Method, null, footer.IsPrecompressed);

            TempWorkspace.AssertTreesEqual(src, Path.Combine(outDir, "src"));
        }

        [Fact]
        public void SingleFileSource_RoundTrips()
        {
            using var ws = new TempWorkspace();
            string file = ws.At("single.dat");
            byte[] data = new byte[123_456];
            new System.Random(3).NextBytes(data);
            File.WriteAllBytes(file, data);

            string exe = ws.At("single.exe");
            var options = CompressionOptions.FromProfile(CompressionProfile.Fast);
            SfxComposer.BuildAppended(DummyStub, file, exe, options);

            var footer = SfxComposer.ReadFooter(exe)!;
            string outDir = ws.At("ex");
            using (var payload = SfxComposer.OpenPayload(exe, footer))
                ZtarEngine.Unpack(payload, outDir, footer.Method, null, false);

            Assert.Equal(
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)),
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(outDir, "single.dat")))));
        }

        [Fact]
        public void Encrypted_CorrectPassword_RoundTrips()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string exe = ws.At("enc.exe");

            var options = CompressionOptions.FromProfile(CompressionProfile.Normal, CompressionMethod.Zstd);
            options.Password = "S3cr3t!";
            var result = SfxComposer.BuildAppended(DummyStub, src, exe, options);
            Assert.True(result.Encrypted);

            var footer = SfxComposer.ReadFooter(exe)!;
            Assert.True(footer.IsEncrypted);

            string outDir = ws.At("ex");
            using (var payload = SfxComposer.OpenPayload(exe, footer))
                ZtarEngine.Unpack(payload, outDir, footer.Method, "S3cr3t!", false);

            TempWorkspace.AssertTreesEqual(src, Path.Combine(outDir, "src"));
        }

        [Fact]
        public void Encrypted_WrongPassword_Throws()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string exe = ws.At("enc.exe");

            var options = CompressionOptions.FromProfile(CompressionProfile.Fast, CompressionMethod.Zstd);
            options.Password = "correct-horse";
            SfxComposer.BuildAppended(DummyStub, src, exe, options);

            var footer = SfxComposer.ReadFooter(exe)!;
            Assert.Throws<InvalidDataException>(() =>
            {
                using var payload = SfxComposer.OpenPayload(exe, footer);
                ZtarEngine.Unpack(payload, ws.At("ex"), footer.Method, "wrong-password", false);
            });
        }

        [Fact]
        public void MultiPart_SplitsAndRejoins()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree(binSize: 300_000);
            string exe = ws.At("mp.exe");

            var options = CompressionOptions.FromProfile(CompressionProfile.Fast, CompressionMethod.Zstd);
            var result = SfxComposer.BuildMultiPart(DummyStub, src, exe, splitSizeBytes: 64 * 1024, options);

            var footer = SfxComposer.ReadFooter(exe)!;
            Assert.True(footer.IsMultiPart);
            Assert.True(footer.PartCount >= 2, $"Mong >=2 phần, có {footer.PartCount}");
            Assert.True(File.Exists(ws.At("mp.001")));

            string outDir = ws.At("ex");
            using (var payload = SfxComposer.OpenPayload(exe, footer))
                ZtarEngine.Unpack(payload, outDir, footer.Method, null, false);

            TempWorkspace.AssertTreesEqual(src, Path.Combine(outDir, "src"));
        }

        [Fact]
        public void CorruptedPayload_FailsCrc()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string exe = ws.At("corrupt.exe");

            var options = CompressionOptions.FromProfile(CompressionProfile.Fast);
            SfxComposer.BuildAppended(DummyStub, src, exe, options);
            var footer = SfxComposer.ReadFooter(exe)!;

            // Flip a byte in the middle of the payload region.
            using (var fs = new FileStream(exe, FileMode.Open, FileAccess.ReadWrite))
            {
                long pos = footer.PayloadOffset + footer.PayloadSize / 2;
                fs.Seek(pos, SeekOrigin.Begin);
                int b = fs.ReadByte();
                fs.Seek(pos, SeekOrigin.Begin);
                fs.WriteByte((byte)(b ^ 0xFF));
            }

            using var payload = SfxComposer.OpenPayload(exe, footer);
            Assert.False(ZtarEngine.VerifyCrc(payload, footer.Crc32));
        }
    }
}
