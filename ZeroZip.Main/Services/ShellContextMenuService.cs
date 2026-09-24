using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ZeroZip.Main.Services
{
    [SupportedOSPlatform("windows")]
    public static class ShellContextMenuService
    {
        private const string FileShellKey = @"Software\Classes\*\shell\ZeroZip";
        private const string FileDirectKey = @"Software\Classes\*\shell\ZeroZipDirect";
        private const string FileContextMenuKey = @"Software\Classes\*\ContextMenus\ZeroZip";

        private const string DirShellKey = @"Software\Classes\Directory\shell\ZeroZip";
        private const string DirDirectKey = @"Software\Classes\Directory\shell\ZeroZipDirect";
        private const string DirContextMenuKey = @"Software\Classes\Directory\ContextMenus\ZeroZip";

        private const string DirBgShellKey = @"Software\Classes\Directory\Background\shell\ZeroZip";
        private const string DirBgDirectKey = @"Software\Classes\Directory\Background\shell\ZeroZipDirect";
        private const string DirBgContextMenuKey = @"Software\Classes\Directory\Background\ContextMenus\ZeroZip";

        private const string ArchiveClassKey = @"Software\Classes\ZeroZip.Archive";
        private const string ExtensionKeyZz = @"Software\Classes\.zz";
        private const string ExtensionKeyZtar = @"Software\Classes\.ztar";
        private const string SystemFileAssocKeyZz = @"Software\Classes\SystemFileAssociations\.zz\shell";
        private const string SystemFileAssocKeyZtar = @"Software\Classes\SystemFileAssociations\.ztar\shell";

        public static bool IsRegistered()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(FileShellKey);
                using var dirKey = Registry.CurrentUser.OpenSubKey(DirShellKey);
                return key != null || dirKey != null;
            }
            catch
            {
                return false;
            }
        }

        public static string ResolveExecutablePath(string? customPath = null)
        {
            if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
                return Path.GetFullPath(customPath);

            var current = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(current) && File.Exists(current) && !current.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(current);
            }

            string baseDir = AppContext.BaseDirectory;
            string[] probePaths =
            {
                Path.Combine(baseDir, "ZeroZip.exe"),
                Path.Combine(baseDir, "..", "..", "..", "..", "publish", "lite", "ZeroZip.exe"),
                Path.Combine(baseDir, "..", "..", "..", "publish", "lite", "ZeroZip.exe"),
                Path.Combine(baseDir, "..", "Release", "net10.0-windows", "ZeroZip.exe")
            };

            foreach (var p in probePaths)
            {
                try
                {
                    string full = Path.GetFullPath(p);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }

            return current ?? "";
        }

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private static void NotifyShell()
        {
            try
            {
                // SHCNE_ASSOCCHANGED = 0x08000000, SHCNF_IDLIST = 0x0000
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        public static bool Register(string? executablePath = null)
        {
            try
            {
                var exe = ResolveExecutablePath(executablePath);
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                {
                    return false;
                }

                // Clean stale/duplicate entries first
                Unregister();

                const string menuTitle = "ZeroZip";

                // --- 1. Cascading Menu for Files (*\shell\ZeroZip) ---
                using (var root = Registry.CurrentUser.CreateSubKey(FileShellKey))
                {
                    // CRITICAL: Do NOT set default value ("") on cascading parent key!
                    root.DeleteValue("", throwOnMissingValue: false);
                    root.SetValue("MUIVerb", menuTitle);
                    root.SetValue("Icon", $"\"{exe}\",0");
                    root.SetValue("SubCommands", "");

                    using var shell = root.CreateSubKey("shell");
                    PopulateFileSubCommands(shell, exe);
                }

                // --- 2. Cascading Menu for Folders (Directory\shell\ZeroZip) ---
                using (var dirRoot = Registry.CurrentUser.CreateSubKey(DirShellKey))
                {
                    dirRoot.DeleteValue("", throwOnMissingValue: false);
                    dirRoot.SetValue("MUIVerb", menuTitle);
                    dirRoot.SetValue("Icon", $"\"{exe}\",0");
                    dirRoot.SetValue("SubCommands", "");

                    using var shell = dirRoot.CreateSubKey("shell");
                    PopulateDirectorySubCommands(shell, exe);
                }

                // --- 3. Cascading Menu for Folder Background (Directory\Background\shell\ZeroZip) ---
                using (var bgRoot = Registry.CurrentUser.CreateSubKey(DirBgShellKey))
                {
                    bgRoot.DeleteValue("", throwOnMissingValue: false);
                    bgRoot.SetValue("MUIVerb", menuTitle);
                    bgRoot.SetValue("Icon", $"\"{exe}\",0");
                    bgRoot.SetValue("SubCommands", "");

                    using var shell = bgRoot.CreateSubKey("shell");
                    PopulateBackgroundSubCommands(shell, exe);
                }

                // --- 4. File Association for .zz and .ztar (ZeroZip.Archive) ---
                using (var extKey = Registry.CurrentUser.CreateSubKey(ExtensionKeyZz))
                {
                    extKey.SetValue("", "ZeroZip.Archive");
                }
                using (var extKeyOld = Registry.CurrentUser.CreateSubKey(ExtensionKeyZtar))
                {
                    extKeyOld.SetValue("", "ZeroZip.Archive");
                }

                using (var archKey = Registry.CurrentUser.CreateSubKey(ArchiveClassKey))
                {
                    archKey.SetValue("", "ZeroZip Sovereign Archive (.zz)");
                    using (var dIcon = archKey.CreateSubKey("DefaultIcon"))
                    {
                        dIcon.SetValue("", $"\"{exe}\",0");
                    }

                    using var shell = archKey.CreateSubKey("shell");
                    PopulateArchiveShellVerbs(shell, exe);
                }

                // SystemFileAssociations (Checked first by modern Windows Shell)
                using (var sfaKeyZz = Registry.CurrentUser.CreateSubKey(SystemFileAssocKeyZz))
                {
                    PopulateArchiveShellVerbs(sfaKeyZz, exe);
                }
                using (var sfaKeyZtar = Registry.CurrentUser.CreateSubKey(SystemFileAssocKeyZtar))
                {
                    PopulateArchiveShellVerbs(sfaKeyZtar, exe);
                }

                NotifyShell();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void PopulateFileSubCommands(RegistryKey shell, string exe)
        {
            using (var c1 = shell.CreateSubKey("01_AddToArchive"))
            {
                c1.SetValue("", "Thêm vào tập tin nén...");
                c1.SetValue("MUIVerb", "Thêm vào tập tin nén...");
                c1.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c1.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --studio \"%1\"");
            }

            using (var c2 = shell.CreateSubKey("02_CompressZz"))
            {
                c2.SetValue("", "Nén nhanh sang .zz");
                c2.SetValue("MUIVerb", "Nén nhanh sang .zz");
                c2.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c2.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\"");
            }

            using (var c3 = shell.CreateSubKey("03_CompressZip"))
            {
                c3.SetValue("", "Nén nhanh sang .zip");
                c3.SetValue("MUIVerb", "Nén nhanh sang .zip");
                c3.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c3.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --zip");
            }

            using (var c4 = shell.CreateSubKey("04_CompressSplit2G"))
            {
                c4.SetValue("", "Nén & chia nhỏ 2GB (.001, .002)...");
                c4.SetValue("MUIVerb", "Nén & chia nhỏ 2GB (.001, .002)...");
                c4.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c4.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --split 2GB");
            }

            using (var c5 = shell.CreateSubKey("05_OpenStudio"))
            {
                c5.SetValue("", "Mở trong ZeroZip Studio...");
                c5.SetValue("MUIVerb", "Mở trong ZeroZip Studio...");
                c5.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c5.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --studio \"%1\"");
            }

            using (var c6 = shell.CreateSubKey("06_CrcSha"))
            {
                c6.SetValue("", "Kiểm tra mã băm & tỉ lệ nén (CRC SHA)...");
                c6.SetValue("MUIVerb", "Kiểm tra mã băm & tỉ lệ nén (CRC SHA)...");
                c6.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c6.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" e \"%1\"");
            }
        }

        private static void PopulateDirectorySubCommands(RegistryKey shell, string exe)
        {
            using (var c1 = shell.CreateSubKey("01_AddToArchive"))
            {
                c1.SetValue("", "Thêm vào tập tin nén...");
                c1.SetValue("MUIVerb", "Thêm vào tập tin nén...");
                c1.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c1.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --studio \"%1\"");
            }

            using (var c2 = shell.CreateSubKey("02_CompressDirZz"))
            {
                c2.SetValue("", "Nén nhanh thư mục sang .zz");
                c2.SetValue("MUIVerb", "Nén nhanh thư mục sang .zz");
                c2.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c2.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\"");
            }

            using (var c3 = shell.CreateSubKey("03_CompressDirZip"))
            {
                c3.SetValue("", "Nén nhanh thư mục sang .zip");
                c3.SetValue("MUIVerb", "Nén nhanh thư mục sang .zip");
                c3.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c3.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --zip");
            }

            using (var c4 = shell.CreateSubKey("04_CompressDirSplit2G"))
            {
                c4.SetValue("", "Nén thư mục chia nhỏ 2GB...");
                c4.SetValue("MUIVerb", "Nén thư mục chia nhỏ 2GB...");
                c4.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c4.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --split 2GB");
            }

            using (var c5 = shell.CreateSubKey("05_OpenStudioDir"))
            {
                c5.SetValue("", "Mở trong ZeroZip Studio...");
                c5.SetValue("MUIVerb", "Mở trong ZeroZip Studio...");
                c5.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c5.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --studio \"%1\"");
            }
        }

        private static void PopulateBackgroundSubCommands(RegistryKey shell, string exe)
        {
            using (var c1 = shell.CreateSubKey("01_CompressCurrentFolderZz"))
            {
                c1.SetValue("", "Nén thư mục hiện tại sang .zz...");
                c1.SetValue("MUIVerb", "Nén thư mục hiện tại sang .zz...");
                c1.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c1.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%V\"");
            }

            using (var c2 = shell.CreateSubKey("02_CompressCurrentFolderZip"))
            {
                c2.SetValue("", "Nén thư mục hiện tại sang .zip...");
                c2.SetValue("MUIVerb", "Nén thư mục hiện tại sang .zip...");
                c2.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c2.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%V\" --zip");
            }

            using (var c3 = shell.CreateSubKey("03_OpenStudioCurrent"))
            {
                c3.SetValue("", "Mở ZeroZip Studio tại đây...");
                c3.SetValue("MUIVerb", "Mở ZeroZip Studio tại đây...");
                c3.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c3.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --studio \"%V\"");
            }
        }

        private static void PopulateArchiveShellVerbs(RegistryKey shell, string exe)
        {
            // Default verb: Open with ZeroZip Explorer (WinRAR style)
            using (var openKey = shell.CreateSubKey("open"))
            {
                openKey.SetValue("", "Mở tập tin nén");
                using var cmd = openKey.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" \"%1\"");
            }

            using (var extFilesKey = shell.CreateSubKey("ExtractFiles"))
            {
                extFilesKey.SetValue("", "Giải nén tập tin...");
                extFilesKey.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = extFilesKey.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-extract \"%1\"");
            }

            using (var extHereKey = shell.CreateSubKey("ExtractHere"))
            {
                extHereKey.SetValue("", "Giải nén tại đây");
                extHereKey.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = extHereKey.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-extract \"%1\"");
            }

            using (var extFolderKey = shell.CreateSubKey("ExtractToFolder"))
            {
                extFolderKey.SetValue("", "Giải nén vào thư mục riêng...");
                extFolderKey.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = extFolderKey.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-extract \"%1\" --to-folder");
            }

            using (var testKey = shell.CreateSubKey("TestArchive"))
            {
                testKey.SetValue("", "Kiểm tra tập tin nén");
                testKey.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = testKey.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-test \"%1\"");
            }
        }

        public static bool Unregister()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(FileShellKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(FileDirectKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(FileContextMenuKey, throwOnMissingSubKey: false);

                Registry.CurrentUser.DeleteSubKeyTree(DirShellKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(DirDirectKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(DirContextMenuKey, throwOnMissingSubKey: false);

                Registry.CurrentUser.DeleteSubKeyTree(DirBgShellKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(DirBgDirectKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(DirBgContextMenuKey, throwOnMissingSubKey: false);

                Registry.CurrentUser.DeleteSubKeyTree(ArchiveClassKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(ExtensionKeyZz, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(ExtensionKeyZtar, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\SystemFileAssociations\.zz", throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\SystemFileAssociations\.ztar", throwOnMissingSubKey: false);

                NotifyShell();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
