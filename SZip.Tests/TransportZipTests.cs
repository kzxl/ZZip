using System.IO;
using System.IO.Compression;
using System.Linq;
using SZip.Core;

namespace SZip.Tests
{
    public class TransportZipTests
    {
        private static readonly byte[] DummyStub = Enumerable.Range(0, 2048).Select(i => (byte)i).ToArray();

        [Fact]
        public void Wrap_Appended_ContainsExeStored()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string exe = ws.At("pack.exe");
            SfxComposer.BuildAppended(DummyStub, src, exe, CompressionOptions.FromProfile(CompressionProfile.Fast));

            string zip = ws.At("pack.zip");
            var names = TransportZip.Wrap(exe, zip);

            Assert.Contains("pack.exe", names);
            using var archive = ZipFile.OpenRead(zip);
            var entry = archive.GetEntry("pack.exe");
            Assert.NotNull(entry);
            // Store mode: compressed length equals the raw length (no recompression).
            Assert.Equal(entry!.Length, entry.CompressedLength);
        }

        [Fact]
        public void Wrap_MultiPart_IncludesAllVolumes()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree(binSize: 250_000);
            string exe = ws.At("mp.exe");
            SfxComposer.BuildMultiPart(DummyStub, src, exe, 64 * 1024,
                CompressionOptions.FromProfile(CompressionProfile.Fast));

            string zip = ws.At("mp.zip");
            var names = TransportZip.Wrap(exe, zip);

            Assert.Contains("mp.exe", names);
            Assert.Contains("mp.001", names);

            // Recipient flow: unzip elsewhere, then extract; volumes must rejoin.
            string recv = ws.At("recv");
            ZipFile.ExtractToDirectory(zip, recv);
            string recvExe = Path.Combine(recv, "mp.exe");

            var footer = SfxComposer.ReadFooter(recvExe)!;
            string outDir = ws.At("ex");
            using (var payload = SfxComposer.OpenPayload(recvExe, footer))
                SZipEngine.Unpack(payload, outDir, footer.Method, null, false);

            TempWorkspace.AssertTreesEqual(src, Path.Combine(outDir, "src"));
        }
    }
}
