using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ZZip.Main.Services
{
    [SupportedOSPlatform("windows")]
    public static class ShellContextMenuService
    {
        private const string FileShellKey = @"Software\Classes\*\shell\ZZip";
        private const string FileDirectKey = @"Software\Classes\*\shell\ZZipDirect";
        private const string FileContextMenuKey = @"Software\Classes\*\ContextMenus\ZZip";

        private const string DirShellKey = @"Software\Classes\Directory\shell\ZZip";
        private const string DirDirectKey = @"Software\Classes\Directory\shell\ZZipDirect";
        private const string DirContextMenuKey = @"Software\Classes\Directory\ContextMenus\ZZip";

        private const string DirBgShellKey = @"Software\Classes\Directory\Background\shell\ZZip";
        private const string DirBgDirectKey = @"Software\Classes\Directory\Background\shell\ZZipDirect";
        private const string DirBgContextMenuKey = @"Software\Classes\Directory\Background\ContextMenus\ZZip";

        private const string ArchiveClassKey = @"Software\Classes\ZZip.Archive";
        private const string ExtensionKeyZz = @"Software\Classes\.zz";
        private const string ExtensionKeyZtar = @"Software\Classes\.ztar";
        private const string SystemFileAssocKeyZz = @"Software\Classes\SystemFileAssociations\.zz\shell";
        private const string SystemFileAssocKeyZtar = @"Software\Classes\SystemFileAssociations\.ztar\shell";

        public static readonly string[] ArchiveExtensions =
        {
            ".zz", ".ztar", ".zip", ".7z", ".rar", ".tar",
            ".gz", ".tgz", ".bz2", ".tbz2", ".xz", ".txz",
            ".iso", ".cab", ".zst", ".lz4", ".lzma", ".wim",
            ".001"
        };

        /// <summary>
        /// Builds an AQS (Advanced Query Syntax) filter that excludes all known archive extensions.
        /// Used as AppliesTo value on *\shell\ZZip so the generic compression menu
        /// does NOT appear on archive files (where SystemFileAssociations provides extraction options).
        /// Example output: System.FileExtension:<>".zip" AND System.FileExtension:<>".7z" AND ...
        /// </summary>
        private static string BuildArchiveExclusionFilter()
        {
            var parts = new string[ArchiveExtensions.Length];
            for (int i = 0; i < ArchiveExtensions.Length; i++)
            {
                parts[i] = $"System.FileExtension:<>\"{ArchiveExtensions[i]}\"";
            }
            return string.Join(" AND ", parts);
        }

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
                Path.Combine(baseDir, "ZZip.exe"),
                Path.Combine(baseDir, "..", "..", "..", "..", "publish", "lite", "ZZip.exe"),
                Path.Combine(baseDir, "..", "..", "..", "publish", "lite", "ZZip.exe"),
                Path.Combine(baseDir, "..", "Release", "net10.0-windows", "ZZip.exe")
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

                const string menuTitle = "ZZip";

                // --- 1. Cascading Menu for Generic Files (*\shell\ZZip) ---
                // Shows ONLY compression options for normal/uncompressed files.
                // AppliesTo AQS filter excludes archive extensions so this menu does NOT
                // appear when right-clicking archive files — SystemFileAssociations\{ext}\shell\ZZip
                // (which includes extraction options) takes over instead.
                // SubCommands = "" is REQUIRED by Windows Explorer to draw the '>' cascading arrow.
                // SeparatorBefore & SeparatorAfter isolate ZZip into its own dedicated context menu cluster.
                using (var root = Registry.CurrentUser.CreateSubKey(FileShellKey))
                {
                    root.DeleteValue("", throwOnMissingValue: false);
                    root.SetValue("MUIVerb", menuTitle);
                    root.SetValue("Icon", $"\"{exe}\",0");
                    root.SetValue("SubCommands", "");
                    root.SetValue("SeparatorBefore", "");
                    root.SetValue("SeparatorAfter", "");
                    root.SetValue("AppliesTo", BuildArchiveExclusionFilter());

                    using var shell = root.CreateSubKey("shell");
                    PopulateGenericCompressionSubCommands(shell, exe);
                }

                // --- 2. Cascading Menu for Folders (Directory\shell\ZZip) ---
                using (var dirRoot = Registry.CurrentUser.CreateSubKey(DirShellKey))
                {
                    dirRoot.DeleteValue("", throwOnMissingValue: false);
                    dirRoot.SetValue("MUIVerb", menuTitle);
                    dirRoot.SetValue("Icon", $"\"{exe}\",0");
                    dirRoot.SetValue("SubCommands", "");
                    dirRoot.SetValue("SeparatorBefore", "");
                    dirRoot.SetValue("SeparatorAfter", "");

                    using var shell = dirRoot.CreateSubKey("shell");
                    PopulateDirectorySubCommands(shell, exe);
                }

                // --- 3. Cascading Menu for Folder Background (Directory\Background\shell\ZZip) ---
                using (var bgRoot = Registry.CurrentUser.CreateSubKey(DirBgShellKey))
                {
                    bgRoot.DeleteValue("", throwOnMissingValue: false);
                    bgRoot.SetValue("MUIVerb", menuTitle);
                    bgRoot.SetValue("Icon", $"\"{exe}\",0");
                    bgRoot.SetValue("SubCommands", "");
                    bgRoot.SetValue("SeparatorBefore", "");
                    bgRoot.SetValue("SeparatorAfter", "");

                    using var shell = bgRoot.CreateSubKey("shell");
                    PopulateBackgroundSubCommands(shell, exe);
                }

                // --- 4. File Associations for Native Archives (.zz and .ztar) ---
                using (var extKey = Registry.CurrentUser.CreateSubKey(ExtensionKeyZz))
                {
                    extKey.SetValue("", "ZZip.Archive");
                }
                using (var extKeyOld = Registry.CurrentUser.CreateSubKey(ExtensionKeyZtar))
                {
                    extKeyOld.SetValue("", "ZZip.Archive");
                }

                using (var archKey = Registry.CurrentUser.CreateSubKey(ArchiveClassKey))
                {
                    archKey.SetValue("", LocalizationService.Get("Shell_ZZipArchiveDesc"));
                    using (var dIcon = archKey.CreateSubKey("DefaultIcon"))
                    {
                        dIcon.SetValue("", $"\"{exe}\",0");
                    }

                    // Default double-click verb: Open with ZZip Explorer (WinRAR/7-Zip style)
                    using (var openKey = archKey.CreateSubKey(@"shell\open"))
                    {
                        string text = LocalizationService.Get("Shell_OpenArchive");
                        openKey.SetValue("", text);
                        openKey.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = openKey.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" \"%1\"");
                    }

                    // Cascading ZZip cluster for ZZip.Archive ProgID
                    using (var cascKey = archKey.CreateSubKey(@"shell\ZZip"))
                    {
                        cascKey.DeleteValue("", throwOnMissingValue: false);
                        cascKey.SetValue("MUIVerb", menuTitle);
                        cascKey.SetValue("Icon", $"\"{exe}\",0");
                        cascKey.SetValue("SubCommands", "");
                        cascKey.SetValue("SeparatorBefore", "");
                        cascKey.SetValue("SeparatorAfter", "");

                        using var shell = cascKey.CreateSubKey("shell");
                        PopulateArchiveSubCommands(shell, exe);
                    }
                }

                // --- 5. SystemFileAssociations for all archive extensions ---
                // Windows Explorer queries SystemFileAssociations\<ext>\shell\ZZip when right-clicking archive files,
                // overriding *\shell\ZZip and cleanly presenting extraction actions + compression actions.
                foreach (var ext in ArchiveExtensions)
                {
                    string sfaKeyPath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\ZZip";
                    using var sfaRoot = Registry.CurrentUser.CreateSubKey(sfaKeyPath);
                    sfaRoot.DeleteValue("", throwOnMissingValue: false);
                    sfaRoot.SetValue("MUIVerb", menuTitle);
                    sfaRoot.SetValue("Icon", $"\"{exe}\",0");
                    sfaRoot.SetValue("SubCommands", "");
                    sfaRoot.SetValue("SeparatorBefore", "");
                    sfaRoot.SetValue("SeparatorAfter", "");

                    using var shell = sfaRoot.CreateSubKey("shell");
                    PopulateArchiveSubCommands(shell, exe);
                }

                // --- 6. CompressedFolder ProgID for Windows default .zip handler ---
                using (var zipProgIdRoot = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CompressedFolder\shell\ZZip"))
                {
                    zipProgIdRoot.DeleteValue("", throwOnMissingValue: false);
                    zipProgIdRoot.SetValue("MUIVerb", menuTitle);
                    zipProgIdRoot.SetValue("Icon", $"\"{exe}\",0");
                    zipProgIdRoot.SetValue("SubCommands", "");
                    zipProgIdRoot.SetValue("SeparatorBefore", "");
                    zipProgIdRoot.SetValue("SeparatorAfter", "");

                    using var shell = zipProgIdRoot.CreateSubKey("shell");
                    PopulateArchiveSubCommands(shell, exe);
                }

                // Explicit double click handler for .zz and .ztar in SystemFileAssociations
                using (var sfaOpenZz = Registry.CurrentUser.CreateSubKey(@"Software\Classes\SystemFileAssociations\.zz\shell\open"))
                {
                    sfaOpenZz.SetValue("", LocalizationService.Get("Shell_OpenArchive"));
                    sfaOpenZz.SetValue("Icon", $"\"{exe}\",0");
                    using var cmd = sfaOpenZz.CreateSubKey("command");
                    cmd.SetValue("", $"\"{exe}\" \"%1\"");
                }
                using (var sfaOpenZtar = Registry.CurrentUser.CreateSubKey(@"Software\Classes\SystemFileAssociations\.ztar\shell\open"))
                {
                    sfaOpenZtar.SetValue("", LocalizationService.Get("Shell_OpenArchive"));
                    sfaOpenZtar.SetValue("Icon", $"\"{exe}\",0");
                    using var cmd = sfaOpenZtar.CreateSubKey("command");
                    cmd.SetValue("", $"\"{exe}\" \"%1\"");
                }

                NotifyShell();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Populates the cascading submenu for generic / non-archive files.
        /// Contains only compression and checksum options.
        /// </summary>
        private static void PopulateGenericCompressionSubCommands(RegistryKey shell, string exe)
        {
            using (var c1 = shell.CreateSubKey("01_AddToArchive"))
            {
                string text = LocalizationService.Get("Shell_AddToArchive");
                c1.SetValue("", text);
                c1.SetValue("MUIVerb", text);
                c1.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c1.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --studio \"%1\"");
            }

            using (var c2 = shell.CreateSubKey("02_CompressZz"))
            {
                string text = LocalizationService.Get("Shell_CompressZz");
                c2.SetValue("", text);
                c2.SetValue("MUIVerb", text);
                c2.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c2.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\"");
            }

            using (var c3 = shell.CreateSubKey("03_CompressZip"))
            {
                string text = LocalizationService.Get("Shell_CompressZip");
                c3.SetValue("", text);
                c3.SetValue("MUIVerb", text);
                c3.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c3.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --zip");
            }

            using (var c4 = shell.CreateSubKey("04_CompressSplit2G"))
            {
                string text = LocalizationService.Get("Shell_CompressSplit2G");
                c4.SetValue("", text);
                c4.SetValue("MUIVerb", text);
                c4.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c4.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --split 2GB");
            }

            using (var c5 = shell.CreateSubKey("05_CrcSha"))
            {
                string text = LocalizationService.Get("Shell_CrcSha");
                c5.SetValue("", text);
                c5.SetValue("MUIVerb", text);
                c5.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c5.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" e \"%1\"");
            }
        }

        /// <summary>
        /// Populates the cascading submenu for archive files (e.g. .zz, .ztar, .zip, .7z, .rar, etc.).
        /// Contains extraction options first, followed by an internal divider, then compression options.
        /// </summary>
        private static void PopulateArchiveSubCommands(RegistryKey shell, string exe)
        {
            // --- Open in Explorer ---
            using (var c0 = shell.CreateSubKey("00_OpenArchive"))
            {
                string text = LocalizationService.Get("Shell_OpenArchive");
                c0.SetValue("", text);
                c0.SetValue("MUIVerb", text);
                c0.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c0.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" \"%1\"");
            }

            // --- Extraction commands ---
            using (var c1 = shell.CreateSubKey("01_ExtractFiles"))
            {
                string text = LocalizationService.Get("Shell_ExtractFiles");
                c1.SetValue("", text);
                c1.SetValue("MUIVerb", text);
                c1.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c1.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-extract \"%1\" --prompt");
            }

            using (var c2 = shell.CreateSubKey("02_ExtractHere"))
            {
                string text = LocalizationService.Get("Shell_ExtractHere");
                c2.SetValue("", text);
                c2.SetValue("MUIVerb", text);
                c2.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c2.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-extract \"%1\" --here");
            }

            using (var c3 = shell.CreateSubKey("03_ExtractToFolder"))
            {
                string text = LocalizationService.Get("Shell_ExtractToFolder");
                c3.SetValue("", text);
                c3.SetValue("MUIVerb", text);
                c3.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c3.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-extract \"%1\" --to-folder");
            }

            using (var c4 = shell.CreateSubKey("04_TestArchive"))
            {
                string text = LocalizationService.Get("Shell_TestArchive");
                c4.SetValue("", text);
                c4.SetValue("MUIVerb", text);
                c4.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c4.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-test \"%1\"");
            }

            // --- Compression commands (with SeparatorBefore to separate extraction from compression) ---
            using (var c5 = shell.CreateSubKey("05_AddToArchive"))
            {
                string text = LocalizationService.Get("Shell_AddToArchive");
                c5.SetValue("", text);
                c5.SetValue("MUIVerb", text);
                c5.SetValue("Icon", $"\"{exe}\",0");
                c5.SetValue("SeparatorBefore", "");
                using var cmd = c5.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --studio \"%1\"");
            }

            using (var c6 = shell.CreateSubKey("06_CompressZz"))
            {
                string text = LocalizationService.Get("Shell_CompressZz");
                c6.SetValue("", text);
                c6.SetValue("MUIVerb", text);
                c6.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c6.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\"");
            }

            using (var c7 = shell.CreateSubKey("07_CompressZip"))
            {
                string text = LocalizationService.Get("Shell_CompressZip");
                c7.SetValue("", text);
                c7.SetValue("MUIVerb", text);
                c7.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c7.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --zip");
            }

            using (var c8 = shell.CreateSubKey("08_CompressSplit2G"))
            {
                string text = LocalizationService.Get("Shell_CompressSplit2G");
                c8.SetValue("", text);
                c8.SetValue("MUIVerb", text);
                c8.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c8.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --split 2GB");
            }

            using (var c9 = shell.CreateSubKey("09_CrcSha"))
            {
                string text = LocalizationService.Get("Shell_CrcSha");
                c9.SetValue("", text);
                c9.SetValue("MUIVerb", text);
                c9.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c9.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" e \"%1\"");
            }
        }

        private static void PopulateDirectorySubCommands(RegistryKey shell, string exe)
        {
            using (var c1 = shell.CreateSubKey("01_AddToArchive"))
            {
                string text = LocalizationService.Get("Shell_AddToArchive");
                c1.SetValue("", text);
                c1.SetValue("MUIVerb", text);
                c1.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c1.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --studio \"%1\"");
            }

            using (var c2 = shell.CreateSubKey("02_CompressDirZz"))
            {
                string text = LocalizationService.Get("Shell_CompressDirZz");
                c2.SetValue("", text);
                c2.SetValue("MUIVerb", text);
                c2.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c2.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\"");
            }

            using (var c3 = shell.CreateSubKey("03_CompressDirZip"))
            {
                string text = LocalizationService.Get("Shell_CompressDirZip");
                c3.SetValue("", text);
                c3.SetValue("MUIVerb", text);
                c3.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c3.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --zip");
            }

            using (var c4 = shell.CreateSubKey("04_CompressDirSplit2G"))
            {
                string text = LocalizationService.Get("Shell_CompressDirSplit2G");
                c4.SetValue("", text);
                c4.SetValue("MUIVerb", text);
                c4.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c4.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --split 2GB");
            }
        }

        private static void PopulateBackgroundSubCommands(RegistryKey shell, string exe)
        {
            using (var c1 = shell.CreateSubKey("01_CompressCurrentFolderZz"))
            {
                string text = LocalizationService.Get("Shell_CompressCurrentFolderZz");
                c1.SetValue("", text);
                c1.SetValue("MUIVerb", text);
                c1.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c1.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%V\"");
            }

            using (var c2 = shell.CreateSubKey("02_CompressCurrentFolderZip"))
            {
                string text = LocalizationService.Get("Shell_CompressCurrentFolderZip");
                c2.SetValue("", text);
                c2.SetValue("MUIVerb", text);
                c2.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c2.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --gui-compress \"%V\" --zip");
            }

            using (var c3 = shell.CreateSubKey("03_OpenStudioCurrent"))
            {
                string text = LocalizationService.Get("Shell_OpenStudioCurrent");
                c3.SetValue("", text);
                c3.SetValue("MUIVerb", text);
                c3.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = c3.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" --studio \"%V\"");
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

                foreach (var ext in ArchiveExtensions)
                {
                    try
                    {
                        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\SystemFileAssociations\{ext}\shell\ZZip", throwOnMissingSubKey: false);
                    }
                    catch { }
                }

                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CompressedFolder\shell\ZZip", throwOnMissingSubKey: false);

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
