using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZstdSharp;

namespace SZip.Core
{
    /// <summary>Result of a quick compressibility probe.</summary>
    public sealed class EstimateResult
    {
        /// <summary>Total bytes of the source (directory or file).</summary>
        public long TotalSize { get; init; }
        /// <summary>Bytes actually sampled and test-compressed.</summary>
        public long SampledBytes { get; init; }
        /// <summary>Predicted compressed/original ratio (0..1). Lower is better.</summary>
        public double PredictedRatio { get; init; }
        /// <summary>Projected output size for the whole source, in bytes.</summary>
        public long PredictedSize { get; init; }
        /// <summary>Estimated percentage reduction (e.g. 62 means ~62% smaller).</summary>
        public double SavingsPercent => (1.0 - PredictedRatio) * 100.0;
        /// <summary>True when the data is already compressed / incompressible (savings under ~3%).</summary>
        public bool AlreadyCompressed => SavingsPercent < 3.0;

        public string Summary()
        {
            if (AlreadyCompressed)
                return $"Dữ liệu gần như không nén được (giảm ~{SavingsPercent:0.#}%). "
                     + "Phần lớn đã được nén sẵn — nén sâu sẽ tốn thời gian mà lợi ích thấp.";
            return $"Ước tính giảm ~{SavingsPercent:0.#}% "
                 + $"({FormatSize(TotalSize)} -> ~{FormatSize(PredictedSize)}).";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 0) return "?";
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double s = bytes; int i = 0;
            while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
            return $"{s:0.##} {u[i]}";
        }
    }

    /// <summary>
    /// Fast compressibility estimator. Samples a bounded slice of the source and test-compresses
    /// it with low-effort zstd to predict the final ratio in ~1 second, regardless of source size.
    /// </summary>
    public static class CompressionEstimator
    {
        private const int ProbeLevel = 3;             // fast
        private const long DefaultBudget = 64L << 20; // sample at most 64 MiB
        private const int ChunkSize = 1 << 20;        // 1 MiB sampling granularity

        /// <summary>
        /// Estimates how well <paramref name="sourcePath"/> (file or directory) will compress.
        /// </summary>
        public static EstimateResult Estimate(string sourcePath, long sampleBudget = DefaultBudget)
        {
            var files = EnumerateFiles(sourcePath, out long totalSize);
            if (totalSize == 0)
                return new EstimateResult { TotalSize = 0, SampledBytes = 0, PredictedRatio = 1, PredictedSize = 0 };

            long budget = Math.Min(sampleBudget, totalSize);
            byte[] sample = GatherSample(files, totalSize, budget, out long sampled);

            long compressed;
            using (var compressor = new Compressor(ProbeLevel))
            {
                // Compress the gathered sample in one shot.
                Span<byte> dest = new byte[(int)Compressor.GetCompressBound(sample.Length)];
                compressed = compressor.Wrap(sample, dest);
            }

            double ratio = sampled > 0 ? (double)compressed / sampled : 1.0;
            ratio = Math.Clamp(ratio, 0.0001, 1.0);

            return new EstimateResult
            {
                TotalSize = totalSize,
                SampledBytes = sampled,
                PredictedRatio = ratio,
                PredictedSize = (long)(totalSize * ratio),
            };
        }

        private static List<string> EnumerateFiles(string sourcePath, out long totalSize)
        {
            var list = new List<string>();
            totalSize = 0;
            if (File.Exists(sourcePath))
            {
                list.Add(sourcePath);
                totalSize = new FileInfo(sourcePath).Length;
            }
            else if (Directory.Exists(sourcePath))
            {
                foreach (var f in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        long len = new FileInfo(f).Length;
                        list.Add(f);
                        totalSize += len;
                    }
                    catch { /* skip unreadable entries */ }
                }
            }
            else
            {
                throw new FileNotFoundException("Không tìm thấy đường dẫn nguồn.", sourcePath);
            }
            return list;
        }

        /// <summary>
        /// Builds a representative sample by reading evenly spaced 1 MiB chunks across all files
        /// until the budget is met. Spreading the reads avoids bias toward any single file type.
        /// </summary>
        private static byte[] GatherSample(List<string> files, long totalSize, long budget, out long sampled)
        {
            using var ms = new MemoryStream((int)Math.Min(budget, int.MaxValue));
            byte[] buf = new byte[ChunkSize];
            double fraction = (double)budget / totalSize; // portion of each file to read

            foreach (var f in files)
            {
                if (ms.Length >= budget) break;
                long len;
                try { len = new FileInfo(f).Length; } catch { continue; }
                if (len == 0) continue;

                long toReadFromFile = Math.Max(ChunkSize, (long)(len * fraction));
                toReadFromFile = Math.Min(toReadFromFile, len);

                try
                {
                    using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read);
                    long readSoFar = 0;
                    // Sample from the start; for large files also grab a middle chunk.
                    long[] offsets = len > 4L * ChunkSize
                        ? new[] { 0L, len / 2 }
                        : new[] { 0L };

                    foreach (long off in offsets)
                    {
                        if (ms.Length >= budget || readSoFar >= toReadFromFile) break;
                        fs.Seek(off, SeekOrigin.Begin);
                        long target = toReadFromFile / offsets.Length;
                        long got = 0;
                        while (got < target && ms.Length < budget)
                        {
                            int want = (int)Math.Min(buf.Length, target - got);
                            int n = fs.Read(buf, 0, want);
                            if (n == 0) break;
                            ms.Write(buf, 0, n);
                            got += n;
                            readSoFar += n;
                        }
                    }
                }
                catch { /* skip unreadable files */ }
            }

            sampled = ms.Length;
            return ms.ToArray();
        }
    }
}
