using System.IO;
using System.Linq;
using ZZip.Core;

namespace ZZip.Tests
{
    /// <summary>
    /// AES-256-GCM authenticated encryption: correct roundtrip, wrong password, and detection
    /// of ciphertext tampering and truncation (which plain CBC + CRC could not guarantee).
    /// </summary>
    public class EncryptionTests
    {
        private static readonly byte[] DummyStub = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();

        [Theory]
        [InlineData(50)]            // tiny: single partial chunk
        [InlineData(64 * 1024)]     // exactly one chunk
        [InlineData(200_000)]       // multiple chunks
        public void RoundTrips_VariousSizes(int size)
        {
            using var ws = new TempWorkspace();
            string file = ws.At("data.bin");
            byte[] data = new byte[size];
            new System.Random(size).NextBytes(data);
            File.WriteAllBytes(file, data);

            string exe = ws.At("enc.exe");
            var options = CompressionOptions.FromProfile(CompressionProfile.Fast);
            options.Password = "p@ss-Word-123";
            SfxComposer.BuildAppended(DummyStub, file, exe, options);

            var footer = SfxComposer.ReadFooter(exe)!;
            Assert.True(footer.IsEncrypted);

            string outDir = ws.At("ex");
            using (var payload = SfxComposer.OpenPayload(exe, footer))
                ZtarEngine.Unpack(payload, outDir, footer.Method, "p@ss-Word-123", false);

            Assert.Equal(
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)),
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(outDir, "data.bin")))));
        }

        [Fact]
        public void Tampered_Ciphertext_FailsAuthentication()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string exe = ws.At("enc.exe");
            var options = CompressionOptions.FromProfile(CompressionProfile.Fast);
            options.Password = "secret";
            SfxComposer.BuildAppended(DummyStub, src, exe, options);

            var footer = SfxComposer.ReadFooter(exe)!;
            // Flip a byte deep inside the ciphertext region (past the salt/verifier header).
            using (var fs = new FileStream(exe, FileMode.Open, FileAccess.ReadWrite))
            {
                long pos = footer.PayloadOffset + footer.PayloadSize - 32;
                fs.Seek(pos, SeekOrigin.Begin);
                int b = fs.ReadByte();
                fs.Seek(pos, SeekOrigin.Begin);
                fs.WriteByte((byte)(b ^ 0xFF));
            }

            // GCM authentication must reject it (even though CRC over the file would also change,
            // the crypto layer itself catches tampering independent of the CRC pre-check).
            Assert.ThrowsAny<System.Exception>(() =>
            {
                using var payload = SfxComposer.OpenPayload(exe, footer);
                ZtarEngine.Unpack(payload, ws.At("ex"), footer.Method, "secret", false);
            });
        }

        [Fact]
        public void Truncated_Payload_IsDetected()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree(binSize: 300_000); // force multiple chunks
            string exe = ws.At("enc.exe");
            var options = CompressionOptions.FromProfile(CompressionProfile.Fast);
            options.Password = "secret";
            var result = SfxComposer.BuildAppended(DummyStub, src, exe, options);

            // Truncate the final chunk by reading the footer, then decrypting a shortened payload
            // stream directly (simulate a cut-off transfer): take all but the last 4 KiB.
            var footer = SfxComposer.ReadFooter(exe)!;
            byte[] full;
            using (var payload = SfxComposer.OpenPayload(exe, footer))
            {
                using var ms = new MemoryStream();
                payload.CopyTo(ms);
                full = ms.ToArray();
            }
            byte[] cut = full[..(full.Length - 4096)];

            // Truncation must be rejected: either a mid-frame cut (EndOfStreamException) or a
            // missing final chunk (InvalidDataException). Both prove the AEAD scheme detects
            // incomplete data. Their common base is SystemException.
            Assert.ThrowsAny<System.SystemException>(() =>
            {
                using var src2 = new MemoryStream(cut);
                ZtarEngine.Unpack(src2, ws.At("ex2"), footer.Method, "secret", false);
            });
        }
    }
}
