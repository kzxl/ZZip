using System.IO;
using System.Linq;
using Ztar.Core;

namespace Ztar.Tests
{
    /// <summary>
    /// Long-distance mode uses a windowLog above the 27 safe ceiling, which requires the
    /// decompressor to raise its windowLogMax. These tests prove large windows roundtrip and
    /// that the footer carries the windowLog needed for extraction.
    /// </summary>
    public class LongModeTests
    {
        private static readonly byte[] DummyStub = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();

        [Theory]
        [InlineData(28)]
        [InlineData(31)]
        public void LargeWindow_RoundTrips(int windowLog)
        {
            using var ws = new TempWorkspace();
            // Build a source with long-range redundancy so a big window actually matters.
            string src = ws.At("src");
            Directory.CreateDirectory(src);
            byte[] block = new byte[256 * 1024];
            new System.Random(5).NextBytes(block);
            using (var fs = File.Create(Path.Combine(src, "repeated.bin")))
            {
                for (int i = 0; i < 12; i++) fs.Write(block); // ~3 MiB, far-apart duplicates
            }

            string exe = ws.At("long.exe");
            var options = CompressionOptions.FromProfile(CompressionProfile.Normal, CompressionMethod.Zstd);
            options.LongDistanceMatching = true;
            options.WindowLog = windowLog;

            var result = SfxComposer.BuildAppended(DummyStub, src, exe, options);
            // Repeated blocks must collapse hard.
            Assert.True(result.Ratio < 0.2, $"Mong tỉ lệ <0.2 nhờ dedup, thực tế {result.Ratio:0.###}");

            var footer = SfxComposer.ReadFooter(exe)!;
            Assert.Equal((byte)windowLog, footer.WindowLog);

            string outDir = ws.At("ex");
            using (var payload = SfxComposer.OpenPayload(exe, footer))
                ZtarEngine.Unpack(payload, outDir, footer.Method, null, false, null, footer.WindowLog);

            TempWorkspace.AssertTreesEqual(src, Path.Combine(outDir, "src"));
        }
    }
}
