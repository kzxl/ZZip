using System;
using System.Diagnostics;
using System.IO;

namespace Ztar.Core
{
    /// <summary>
    /// Drives an external <c>precomp</c> executable (Schnaader's Precomp) to undo the internal
    /// compression of already-compressed streams (zlib/deflate inside zip, png, pdf, gzip, ...),
    /// turning them back into raw data that the final codec can pack far better. This is the
    /// technique that makes game/installer repacks (FitGirl-style) so small.
    ///
    /// precomp is NOT bundled. The tool auto-detects it; without it, the feature stays disabled.
    /// </summary>
    public static class PrecompService
    {
        /// <summary>
        /// Finds precomp.exe. Search order: explicit path, app dir, app\tools, current dir,
        /// then PATH. Returns null when not found.
        /// </summary>
        public static string? Locate(string? explicitPath = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
                return Path.GetFullPath(explicitPath);

            string exeName = OperatingSystem.IsWindows() ? "precomp.exe" : "precomp";
            string appDir = AppContext.BaseDirectory;

            string[] candidates =
            {
                Path.Combine(appDir, exeName),
                Path.Combine(appDir, "tools", exeName),
                Path.Combine(Directory.GetCurrentDirectory(), exeName),
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return Path.GetFullPath(c);

            // PATH lookup.
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    try
                    {
                        string p = Path.Combine(dir.Trim(), exeName);
                        if (File.Exists(p)) return p;
                    }
                    catch { /* ignore malformed PATH entries */ }
                }
            }
            return null;
        }

        /// <summary>True when a usable precomp executable can be located.</summary>
        public static bool IsAvailable(string? explicitPath = null) => Locate(explicitPath) != null;

        /// <summary>
        /// Precompresses <paramref name="inputFile"/> into <paramref name="outputFile"/> using
        /// precomp with no internal compression (-cn) so our codec does the final squeezing.
        /// </summary>
        public static void Precompress(string precompExe, string inputFile, string outputFile, string? extraArgs = null)
        {
            // precomp -cn -o<output> <input>
            string args = $"-cn {extraArgs} -o\"{outputFile}\" \"{inputFile}\"";
            Run(precompExe, args, Path.GetDirectoryName(Path.GetFullPath(outputFile))!);
            if (!File.Exists(outputFile))
                throw new IOException("precomp không tạo được tệp đầu ra (precompress).");
        }

        /// <summary>Restores a precomp container file back to the original bytes.</summary>
        public static void Restore(string precompExe, string inputFile, string outputFile)
        {
            // precomp -r -o<output> <input>
            string args = $"-r -o\"{outputFile}\" \"{inputFile}\"";
            Run(precompExe, args, Path.GetDirectoryName(Path.GetFullPath(outputFile))!);
            if (!File.Exists(outputFile))
                throw new IOException("precomp không phục hồi được dữ liệu (restore).");
        }

        private static void Run(string exe, string args, string workingDir)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = workingDir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Không khởi chạy được precomp.");
            // Drain pipes to avoid deadlock on large output.
            string stderr = process.StandardError.ReadToEnd();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception($"precomp lỗi (mã {process.ExitCode}): {stderr}");
        }
    }
}
