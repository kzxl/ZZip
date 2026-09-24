using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;
using ZZip.Core;
using ZZip.Localization;

namespace ZZip.Tests
{
    public class DiskSpaceSafetyTests
    {
        [Fact]
        public void TryGetAvailableFreeSpace_ValidPath_ReturnsPositiveBytesAndDriveName()
        {
            string temp = Path.GetTempPath();
            bool ok = DiskSpaceSafety.TryGetAvailableFreeSpace(temp, out long freeBytes, out string driveName);

            Assert.True(ok);
            Assert.True(freeBytes > 0);
            Assert.False(string.IsNullOrWhiteSpace(driveName));
        }

        [Fact]
        public void CheckDiskSpace_SmallRequirement_ReturnsHasEnoughSpaceTrue()
        {
            string temp = Path.GetTempPath();
            var result = DiskSpaceSafety.CheckDiskSpace(temp, 1024);

            Assert.True(result.CheckSucceeded);
            Assert.True(result.HasEnoughSpace);
            Assert.Equal(0, result.DeficitBytes);
            Assert.Equal(1024, result.RequiredBytes);
        }

        [Fact]
        public void CheckDiskSpace_ExorbitantRequirement_ReturnsHasEnoughSpaceFalse()
        {
            string temp = Path.GetTempPath();
            // Request 1 Exabyte (10^18 bytes)
            long huge = 1_000_000_000_000_000_000L;
            var result = DiskSpaceSafety.CheckDiskSpace(temp, huge);

            Assert.True(result.CheckSucceeded);
            Assert.False(result.HasEnoughSpace);
            Assert.True(result.DeficitBytes > 0);
            Assert.Equal(huge - result.AvailableFreeBytes, result.DeficitBytes);
        }

        [Fact]
        public void CheckDiskSpace_ZeroOrNegativeRequirement_ReturnsTrue()
        {
            string temp = Path.GetTempPath();
            var resZero = DiskSpaceSafety.CheckDiskSpace(temp, 0);
            Assert.True(resZero.HasEnoughSpace);
            Assert.Equal(0, resZero.DeficitBytes);

            var resNeg = DiskSpaceSafety.CheckDiskSpace(temp, -500);
            Assert.True(resNeg.HasEnoughSpace);
            Assert.Equal(0, resNeg.DeficitBytes);
        }

        [Fact]
        public void EstimateArchiveUncompressedSize_ZipFile_ReturnsAccurateSize()
        {
            string tempZip = Path.Combine(Path.GetTempPath(), $"test_safety_{Guid.NewGuid():N}.zip");
            try
            {
                using (var archive = ZipFile.Open(tempZip, ZipArchiveMode.Create))
                {
                    var e1 = archive.CreateEntry("file1.txt");
                    using (var w1 = new StreamWriter(e1.Open()))
                    {
                        w1.Write("Hello World 12345"); // 17 bytes
                    }

                    var e2 = archive.CreateEntry("file2.bin");
                    using (var s2 = e2.Open())
                    {
                        s2.Write(new byte[100]); // 100 bytes
                    }
                }

                long estimated = DiskSpaceSafety.EstimateArchiveUncompressedSize(tempZip);
                Assert.Equal(117, estimated);
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
        }

        [Fact]
        public void Localization_DiskSpaceWarningKeys_ExistInAllLanguages()
        {
            var manager = LocalizationManager.Instance;
            string[] langCodes = { "vi", "en", "ja", "zh" };

            foreach (var code in langCodes)
            {
                manager.SetLanguage(code);
                string title = manager.Get("Msg_LowDiskSpaceTitle");
                string msg = manager.Get("Msg_LowDiskSpaceWarning");

                Assert.False(string.IsNullOrWhiteSpace(title), $"Title missing in {code}");
                Assert.False(string.IsNullOrWhiteSpace(msg), $"Warning missing in {code}");
                Assert.Contains("{0}", msg);
                Assert.Contains("{1}", msg);
                Assert.Contains("{2}", msg);
                Assert.Contains("{3}", msg);
            }
        }
    }
}
