using System;
using System.Diagnostics;
using System.IO;
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
        private const string ExtensionKey = @"Software\Classes\.ztar";
        private const string SystemFileAssocKey = @"Software\Classes\SystemFileAssociations\.ztar\shell";

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

        public static bool Register(string? executablePath = null)
        {
            try
            {
                var exe = ResolveExecutablePath(executablePath);
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                {
                    return false;
                }

                const string menuTitle = "ZeroZip — Sovereign Ultra-Compression";

                // --- 1. Direct Quick Verb for Files (Instant access, guaranteed display) ---
                using (var direct = Registry.CurrentUser.CreateSubKey(FileDirectKey))
                {
                    direct.SetValue("", "Nén sang .ztar (ZeroZip)...");
                    direct.SetValue("MUIVerb", "Nén sang .ztar (ZeroZip)...");
                    direct.SetValue("Icon", $"\"{exe}\",0");
                    using var cmd = direct.CreateSubKey("command");
                    cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\"");
                }

                // --- 2. Cascading Menu for Files (ExtendedSubCommandsKey & SubCommands) ---
                using (var root = Registry.CurrentUser.CreateSubKey(FileShellKey))
                {
                    // CRITICAL: Do NOT set default value ("") on cascading parent key!
                    root.DeleteValue("", throwOnMissingValue: false);
                    root.SetValue("MUIVerb", menuTitle);
                    root.SetValue("Icon", $"\"{exe}\",0");
                    root.SetValue("ExtendedSubCommandsKey", @"*\ContextMenus\ZeroZip");
                    root.SetValue("SubCommands", "");

                    // Grandchild shell fallback
                    using var shell = root.CreateSubKey("shell");
                    PopulateFileSubCommands(shell, exe);
                }

                // ContextMenus structure for ExtendedSubCommandsKey
                using (var cmRoot = Registry.CurrentUser.CreateSubKey(FileContextMenuKey))
                {
                    using var shell = cmRoot.CreateSubKey("shell");
                    PopulateFileSubCommands(shell, exe);
                }

                // --- 3. Direct Quick Verb for Folders ---
                using (var dirDirect = Registry.CurrentUser.CreateSubKey(DirDirectKey))
                {
                    dirDirect.SetValue("", "Nén thư mục sang .ztar (ZeroZip)...");
                    dirDirect.SetValue("MUIVerb", "Nén thư mục sang .ztar (ZeroZip)...");
                    dirDirect.SetValue("Icon", $"\"{exe}\",0");
                    using var cmd = dirDirect.CreateSubKey("command");
                    cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\"");
                }

                // --- 4. Cascading Menu for Folders ---
                using (var dirRoot = Registry.CurrentUser.CreateSubKey(DirShellKey))
                {
                    dirRoot.DeleteValue("", throwOnMissingValue: false);
                    dirRoot.SetValue("MUIVerb", menuTitle);
                    dirRoot.SetValue("Icon", $"\"{exe}\",0");
                    dirRoot.SetValue("ExtendedSubCommandsKey", @"Directory\ContextMenus\ZeroZip");
                    dirRoot.SetValue("SubCommands", "");

                    using var shell = dirRoot.CreateSubKey("shell");
                    PopulateDirectorySubCommands(shell, exe);
                }

                using (var dirCmRoot = Registry.CurrentUser.CreateSubKey(DirContextMenuKey))
                {
                    using var shell = dirCmRoot.CreateSubKey("shell");
                    PopulateDirectorySubCommands(shell, exe);
                }

                // --- 5. Folder Background Menu (Right-click inside folder empty space) ---
                using (var bgDirect = Registry.CurrentUser.CreateSubKey(DirBgDirectKey))
                {
                    bgDirect.SetValue("", "Nén thư mục hiện tại sang .ztar (ZeroZip)...");
                    bgDirect.SetValue("MUIVerb", "Nén thư mục hiện tại sang .ztar (ZeroZip)...");
                    bgDirect.SetValue("Icon", $"\"{exe}\",0");
                    using var cmd = bgDirect.CreateSubKey("command");
                    cmd.SetValue("", $"\"{exe}\" --gui-compress \"%V\"");
                }

                using (var bgRoot = Registry.CurrentUser.CreateSubKey(DirBgShellKey))
                {
                    bgRoot.DeleteValue("", throwOnMissingValue: false);
                    bgRoot.SetValue("MUIVerb", menuTitle);
                    bgRoot.SetValue("Icon", $"\"{exe}\",0");
                    bgRoot.SetValue("ExtendedSubCommandsKey", @"Directory\Background\ContextMenus\ZeroZip");
                    bgRoot.SetValue("SubCommands", "");

                    using var shell = bgRoot.CreateSubKey("shell");
                    PopulateBackgroundSubCommands(shell, exe);
                }

                using (var bgCmRoot = Registry.CurrentUser.CreateSubKey(DirBgContextMenuKey))
                {
                    using var shell = bgCmRoot.CreateSubKey("shell");
                    PopulateBackgroundSubCommands(shell, exe);
                }

                // --- 6. File Association for .ztar ---
                using (var extKey = Registry.CurrentUser.CreateSubKey(ExtensionKey))
                {
                    extKey.SetValue("", "ZeroZip.Archive");
                }

                using (var archKey = Registry.CurrentUser.CreateSubKey(ArchiveClassKey))
                {
                    archKey.SetValue("", "ZeroZip Ultra-Compressed Archive");
                    using (var dIcon = archKey.CreateSubKey("DefaultIcon"))
                    {
                        dIcon.SetValue("", $"\"{exe}\",0");
                    }

                    using var shell = archKey.CreateSubKey("shell");
                    PopulateArchiveShellVerbs(shell, exe);
                }

                // SystemFileAssociations (Checked first by modern Windows Shell)
                using (var sfaKey = Registry.CurrentUser.CreateSubKey(SystemFileAssocKey))
                {
                    PopulateArchiveShellVerbs(sfaKey, exe);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void PopulateFileSubCommands(RegistryKey shell, string exe)
        {
            using (var c1 = shell.CreateSubKey("01_CompressGui"))
            {
                c1.SetValue("", "Nén sang .ztar (Siêu tốc Zstandard)...");
                c1.SetValue("MUIVerb", "Nén sang .ztar (Siêu tốc Zstandard)...");
                c1.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c1.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\"");
            }

            using (var c2 = shell.CreateSubKey("02_CompressSplit2G"))
            {
                c2.SetValue("", "Nén & Chia nhỏ 2GB (.001, .002)...");
                c2.SetValue("MUIVerb", "Nén & Chia nhỏ 2GB (.001, .002)...");
                c2.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c2.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --split 2GB");
            }

            using (var c3 = shell.CreateSubKey("03_OpenStudio"))
            {
                c3.SetValue("", "Thêm vào kho nén (Mở Studio)...");
                c3.SetValue("MUIVerb", "Thêm vào kho nén (Mở Studio)...");
                c3.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c3.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --studio \"%1\"");
            }

            using (var c4 = shell.CreateSubKey("04_Estimate"))
            {
                c4.SetValue("", "Kiểm tra nhanh tỉ lệ nén...");
                c4.SetValue("MUIVerb", "Kiểm tra nhanh tỉ lệ nén...");
                c4.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c4.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" e \"%1\"");
            }
        }

        private static void PopulateDirectorySubCommands(RegistryKey shell, string exe)
        {
            using (var c1 = shell.CreateSubKey("01_CompressDirGui"))
            {
                c1.SetValue("", "Nén thư mục này sang .ztar...");
                c1.SetValue("MUIVerb", "Nén thư mục này sang .ztar...");
                c1.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c1.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\"");
            }

            using (var c2 = shell.CreateSubKey("02_CompressDirSplit2G"))
            {
                c2.SetValue("", "Nén thư mục chia nhỏ 2GB...");
                c2.SetValue("MUIVerb", "Nén thư mục chia nhỏ 2GB...");
                c2.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c2.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --split 2GB");
            }

            using (var c3 = shell.CreateSubKey("03_OpenStudioDir"))
            {
                c3.SetValue("", "Thêm thư mục vào Studio...");
                c3.SetValue("MUIVerb", "Thêm thư mục vào Studio...");
                c3.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c3.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --studio \"%1\"");
            }
        }

        private static void PopulateBackgroundSubCommands(RegistryKey shell, string exe)
        {
            using (var c1 = shell.CreateSubKey("01_CompressCurrentFolder"))
            {
                c1.SetValue("", "Nén thư mục hiện tại sang .ztar...");
                c1.SetValue("MUIVerb", "Nén thư mục hiện tại sang .ztar...");
                c1.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c1.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%V\"");
            }
        }

        private static void PopulateArchiveShellVerbs(RegistryKey shell, string exe)
        {
            // Default verb: Open with ZeroZip Explorer (WinRAR style)
            using (var openKey = shell.CreateSubKey("open"))
            {
                openKey.SetValue("", "Mở bằng ZeroZip Explorer");
                using var cmd = openKey.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" \"%1\"");
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
                testKey.SetValue("", "Kiểm tra toàn vẹn gói nén");
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
                Registry.CurrentUser.DeleteSubKeyTree(ExtensionKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\SystemFileAssociations\.ztar", throwOnMissingSubKey: false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
