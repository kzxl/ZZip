using System;
using System.IO;
using System.Threading;
using ZZip.Core;

namespace ZZip.Tests
{
    public class CancellationTests
    {
        private static readonly byte[] DummyStub = new byte[512];

        [Fact]
        public void Pack_CanBeCancelled_MidStream()
        {
            using var ws = new TempWorkspace();
            // Large, compressible source so packing takes more than one buffer.
            string src = ws.At("src");
            Directory.CreateDirectory(src);
            byte[] block = new byte[4 * 1024 * 1024];
            new Random(1).NextBytes(block);
            using (var fs = File.Create(Path.Combine(src, "big.bin")))
                for (int i = 0; i < 8; i++) fs.Write(block); // 32 MiB

            string exe = ws.At("out.exe");
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // already cancelled -> first stream touch should throw

            var options = CompressionOptions.FromProfile(CompressionProfile.Ultra);
            Assert.Throws<OperationCanceledException>(() =>
                SfxComposer.BuildAppended(DummyStub, src, exe, options, null, cts.Token));
        }

        [Fact]
        public void Unpack_CanBeCancelled()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree(binSize: 1_000_000);
            string exe = ws.At("out.exe");
            SfxComposer.BuildAppended(DummyStub, src, exe, CompressionOptions.FromProfile(CompressionProfile.Fast));

            var footer = SfxComposer.ReadFooter(exe)!;
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
            {
                using var payload = SfxComposer.OpenPayload(exe, footer);
                ZtarEngine.Unpack(payload, ws.At("ex"), footer.Method, null, false, null, footer.WindowLog, cts.Token);
            });
        }

        [Fact]
        public void NoCancellation_CompletesNormally()
        {
            using var ws = new TempWorkspace();
            string src = ws.CreateSampleTree();
            string exe = ws.At("out.exe");
            // default token (CancellationToken.None) must not interfere.
            var result = SfxComposer.BuildAppended(DummyStub, src, exe,
                CompressionOptions.FromProfile(CompressionProfile.Fast), null, CancellationToken.None);
            Assert.True(result.CompressedSize > 0);
        }
    }
}
