using System.IO;
using System.Linq;
using SZip.Core;

namespace SZip.Tests
{
    public class ListAndTestTests
    {
        private static readonly byte[] DummyStub = new byte[512];

        [Fact]
        public void ListEntries_ReportsFilesWithoutExtracting()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string exe = ws.At("out.exe");
            SfxComposer.BuildAppended(DummyStub, src, exe, CompressionOptions.FromProfile(CompressionProfile.Fast));

            var footer = SfxComposer.ReadFooter(exe)!;
            using var payload = SfxComposer.OpenPayload(exe, footer);
            var entries = SZipEngine.ListEntries(payload, footer.Method, null, false, footer.WindowLog);

            var names = entries.Select(e => e.Name).ToList();
            Assert.Contains(names, n => n.EndsWith("readme.txt"));
            Assert.Contains(names, n => n.EndsWith("notes.md"));
            Assert.Contains(names, n => n.EndsWith("random.bin"));

            // The readme entry's reported size must match the real file.
            var readme = entries.First(e => e.Name.EndsWith("readme.txt"));
            Assert.Equal(new FileInfo(Path.Combine(src, "readme.txt")).Length, readme.Size);
        }

        [Fact]
        public void TestArchive_ReturnsTrue_ForGoodArchive()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string exe = ws.At("out.exe");
            SfxComposer.BuildAppended(DummyStub, src, exe, CompressionOptions.FromProfile(CompressionProfile.Normal, CompressionMethod.Lzma));

            var footer = SfxComposer.ReadFooter(exe)!;
            using var payload = SfxComposer.OpenPayload(exe, footer);
            Assert.True(SZipEngine.TestArchive(payload, footer.Method, null, false, footer.WindowLog));
        }

        [Fact]
        public void TestArchive_Throws_OnTamperedEncrypted()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string exe = ws.At("out.exe");
            var options = CompressionOptions.FromProfile(CompressionProfile.Fast);
            options.Password = "pw";
            SfxComposer.BuildAppended(DummyStub, src, exe, options);

            var footer = SfxComposer.ReadFooter(exe)!;
            using (var fs = new FileStream(exe, FileMode.Open, FileAccess.ReadWrite))
            {
                long pos = footer.PayloadOffset + footer.PayloadSize - 20;
                fs.Seek(pos, SeekOrigin.Begin);
                int b = fs.ReadByte();
                fs.Seek(pos, SeekOrigin.Begin);
                fs.WriteByte((byte)(b ^ 0xFF));
            }

            Assert.ThrowsAny<System.Exception>(() =>
            {
                using var payload = SfxComposer.OpenPayload(exe, footer);
                SZipEngine.TestArchive(payload, footer.Method, "pw", false, footer.WindowLog);
            });
        }
    }
}
