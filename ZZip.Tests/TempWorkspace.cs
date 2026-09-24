using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace ZZip.Tests
{
    /// <summary>
    /// Shared utilities: scratch directories that auto-clean, deterministic sample trees,
    /// and recursive content comparison for roundtrip assertions.
    /// </summary>
    internal sealed class TempWorkspace : IDisposable
    {
        public string Root { get; }

        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "ztar_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string At(params string[] parts) =>
            System.IO.Path.Combine(new[] { Root }.Concat(parts).ToArray());

        /// <summary>
        /// Builds a deterministic source tree with mixed content: highly compressible text,
        /// tiny text, and incompressible pseudo-random binary. Returns the source dir path.
        /// </summary>
        public string CreateSampleTree(string name = "src", int seed = 1234, int binSize = 200_000)
        {
            string src = At(name);
            Directory.CreateDirectory(System.IO.Path.Combine(src, "sub"));

            File.WriteAllText(System.IO.Path.Combine(src, "readme.txt"),
                string.Concat(Enumerable.Repeat("Hello ZZip roundtrip test! ", 500)));
            File.WriteAllText(System.IO.Path.Combine(src, "sub", "notes.md"),
                "# Notes\nalpha\nbeta\ngamma\ndelta");

            var rng = new Random(seed);
            byte[] bin = new byte[binSize];
            rng.NextBytes(bin);
            File.WriteAllBytes(System.IO.Path.Combine(src, "random.bin"), bin);

            return src;
        }

        /// <summary>Asserts every file under <paramref name="expectedDir"/> exists with identical bytes under <paramref name="actualDir"/>.</summary>
        public static void AssertTreesEqual(string expectedDir, string actualDir)
        {
            foreach (var f in Directory.EnumerateFiles(expectedDir, "*", SearchOption.AllDirectories))
            {
                string rel = System.IO.Path.GetRelativePath(expectedDir, f);
                string other = System.IO.Path.Combine(actualDir, rel);
                Assert.True(File.Exists(other), $"Thiếu tệp sau giải nén: {rel}");
                Assert.Equal(Hash(f), Hash(other));
            }
        }

        private static string Hash(string path)
        {
            using var s = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(s));
        }

        public void Dispose()
        {
            try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
