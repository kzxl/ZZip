using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

namespace ZZip.Core
{
    /// <summary>
    /// Result of a disk space pre-check before performing an extraction or decompression operation.
    /// </summary>
    public sealed class DiskSpaceCheckResult
    {
        public bool CheckSucceeded { get; init; }
        public bool HasEnoughSpace { get; init; } = true;
        public long RequiredBytes { get; init; }
        public long AvailableFreeBytes { get; init; }
        public long DeficitBytes => Math.Max(0, RequiredBytes - AvailableFreeBytes);
        public string DriveName { get; init; } = "";
    }

    /// <summary>
    /// Safety helper for validating remaining disk space against expected uncompressed archive sizes.
    /// </summary>
    public static class DiskSpaceSafety
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceEx(
            string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        /// <summary>
        /// Retrieves the available free disk space (in bytes) on the volume where the destination path resides.
        /// Supports local drive letters, nested non-existent paths, and UNC network paths.
        /// </summary>
        public static bool TryGetAvailableFreeSpace(string destinationPath, out long freeBytes, out string driveName)
        {
            freeBytes = 0;
            driveName = "";

            if (string.IsNullOrWhiteSpace(destinationPath))
                return false;

            try
            {
                string fullPath = Path.GetFullPath(destinationPath);
                string? root = Path.GetPathRoot(fullPath);
                if (string.IsNullOrEmpty(root))
                    return false;

                // 1. Try standard DriveInfo for local drive letters (e.g. C:\)
                if (!root.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var drive = new DriveInfo(root);
                        if (drive.IsReady)
                        {
                            freeBytes = drive.AvailableFreeSpace;
                            driveName = drive.Name;
                            return true;
                        }
                    }
                    catch
                    {
                        // Fall through to Win32 API
                    }
                }

                // 2. On Windows, use GetDiskFreeSpaceEx which supports UNC paths, mount points, and user quotas
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string checkPath = fullPath;
                    while (!Directory.Exists(checkPath) && !string.IsNullOrEmpty(checkPath))
                    {
                        string? parent = Path.GetDirectoryName(checkPath);
                        if (parent == null || parent.Equals(checkPath, StringComparison.OrdinalIgnoreCase))
                            break;
                        checkPath = parent;
                    }

                    if (Directory.Exists(checkPath))
                    {
                        if (GetDiskFreeSpaceEx(checkPath, out ulong freeAvail, out _, out _))
                        {
                            freeBytes = (long)freeAvail;
                            driveName = root;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Estimates the total uncompressed size of the given archive file without decompressing it.
        /// Returns 0 or -1 if the format cannot be determined or read.
        /// </summary>
        public static long EstimateArchiveUncompressedSize(string archivePath)
        {
            if (string.IsNullOrEmpty(archivePath) || !File.Exists(archivePath))
                return 0;

            try
            {
                // 1. Check Native ZZip (ZTAR / SFX footer)
                var footer = SfxComposer.ReadFooter(archivePath);
                if (footer != null && footer.OriginalSize > 0)
                {
                    return footer.OriginalSize;
                }

                // 2. Check Standard PKZIP (.zip)
                if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using var zip = ZipFile.OpenRead(archivePath);
                    return zip.Entries.Sum(e => e.Length);
                }
            }
            catch
            {
                // If encrypted or corrupted, return 0
            }

            return 0;
        }

        /// <summary>
        /// Evaluates whether the destination path has sufficient disk space for the expected uncompressed size.
        /// </summary>
        public static DiskSpaceCheckResult CheckDiskSpace(string destinationPath, long requiredBytes)
        {
            if (requiredBytes <= 0)
            {
                return new DiskSpaceCheckResult
                {
                    CheckSucceeded = true,
                    HasEnoughSpace = true,
                    RequiredBytes = 0,
                    AvailableFreeBytes = 0,
                    DriveName = ""
                };
            }

            if (!TryGetAvailableFreeSpace(destinationPath, out long freeBytes, out string driveName))
            {
                return new DiskSpaceCheckResult
                {
                    CheckSucceeded = false,
                    HasEnoughSpace = true, // Permissive on check failure
                    RequiredBytes = requiredBytes,
                    AvailableFreeBytes = 0,
                    DriveName = ""
                };
            }

            return new DiskSpaceCheckResult
            {
                CheckSucceeded = true,
                HasEnoughSpace = freeBytes >= requiredBytes,
                RequiredBytes = requiredBytes,
                AvailableFreeBytes = freeBytes,
                DriveName = driveName
            };
        }

        /// <summary>
        /// Formats byte quantities into readable strings (e.g. 1.5 GB).
        /// </summary>
        public static string FormatSize(long bytes)
        {
            if (bytes < 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double b = bytes;
            int u = 0;
            while (b >= 1024 && u < units.Length - 1)
            {
                b /= 1024;
                u++;
            }
            return $"{b:F1} {units[u]}";
        }
    }
}
