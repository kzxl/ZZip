using System;
using System.IO;
using System.Reflection;
using SZip.Core;

namespace SZip.Main.Services
{
    /// <summary>
    /// Builds self-extracting archives using the append model. The extractor stub is
    /// embedded into this application as a resource at build time, so creating an SFX is
    /// just: copy stub bytes + stream compressed payload + write footer. No .NET SDK and
    /// no on-the-fly recompilation required at runtime.
    /// </summary>
    public class SfxBuilderService
    {
        /// <summary>LogicalName of the embedded prebuilt stub executable (see SZip.Main.csproj).</summary>
        private const string StubResourceName = "SZip.SfxStub";

        /// <summary>
        /// Creates an SFX .exe from <paramref name="sourcePath"/> using fully specified options.
        /// </summary>
        /// <param name="splitSizeBytes">0 = embed payload in the exe; &gt;0 = split into external volumes.</param>
        public PackResult BuildSfx(string sourcePath, string destExePath, long splitSizeBytes,
            CompressionOptions options, IProgress<long>? progress = null,
            System.Threading.CancellationToken cancel = default)
        {
            byte[] stub = LoadStubBytes();

            if (splitSizeBytes <= 0)
                return SfxComposer.BuildAppended(stub, sourcePath, destExePath, options, progress, cancel);

            return SfxComposer.BuildMultiPart(stub, sourcePath, destExePath, splitSizeBytes, options, progress, cancel);
        }

        /// <summary>
        /// Convenience overload that builds options from a profile + method + optional password.
        /// </summary>
        public PackResult BuildSfx(string sourcePath, string destExePath, long splitSizeBytes,
            CompressionProfile profile = CompressionProfile.Ultra,
            CompressionMethod method = CompressionMethod.Zstd,
            string? password = null,
            IProgress<long>? progress = null)
        {
            var options = CompressionOptions.FromProfile(profile, method);
            options.Password = password;
            return BuildSfx(sourcePath, destExePath, splitSizeBytes, options, progress);
        }

        /// <summary>Quickly predicts the compression ratio for a source path.</summary>
        public EstimateResult Estimate(string sourcePath) => CompressionEstimator.Estimate(sourcePath);

        /// <summary>True when an external precomp tool is available for deep (repack-style) compression.</summary>
        public bool IsPrecompAvailable() => PrecompService.IsAvailable();

        /// <summary>
        /// Wraps a finished SFX and its volumes into a transport .zip so it can pass mail/web
        /// filters that block raw .exe attachments. Returns the zip path.
        /// </summary>
        public string WrapForTransport(string sfxExePath, string? zipPath = null)
        {
            zipPath ??= Path.ChangeExtension(sfxExePath, ".zip");
            TransportZip.Wrap(sfxExePath, zipPath);
            return zipPath;
        }

        /// <summary>Loads the prebuilt extractor stub embedded in this assembly.</summary>
        private static byte[] LoadStubBytes()
        {
            var asm = Assembly.GetExecutingAssembly();
            using Stream? s = asm.GetManifestResourceStream(StubResourceName);
            if (s == null)
            {
                throw new InvalidOperationException(
                    "Không tìm thấy stub giải nén được nhúng. Hãy build lại dự án để MSBuild đóng gói SZip.Stub.");
            }
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
