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
        private const string DirShellKey = @"Software\Classes\Directory\shell\ZeroZip";
        private const string DirBgShellKey = @"Software\Classes\Directory\Background\shell\ZeroZip";
        private const string ArchiveClassKey = @"Software\Classes\ZeroZip.Archive";
        private const string ExtensionKey = @"Software\Classes\.ztar";

        public static bool IsRegistered()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(FileShellKey);
                return key != null;
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

            // Fallback: check publish/lite/ZeroZip.exe or bin release
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

                // 1. Context menu for individual files (*\shell\ZeroZip)
                using (var root = Registry.CurrentUser.CreateSubKey(FileShellKey))
                {
                    root.SetValue("", menuTitle);
                    root.SetValue("MUIVerb", menuTitle);
                    root.SetValue("Icon", $"\"{exe}\",0");
                    root.SetValue("SubCommands", "");

                    using var shell = root.CreateSubKey("shell");

                    using (var c1 = shell.CreateSubKey("CompressGui"))
                    {
                        c1.SetValue("", "Nén sang .ztar (Siêu tốc Zstandard)...");
                        c1.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c1.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\"");
                    }

                    using (var c2 = shell.CreateSubKey("CompressSplit2G"))
                    {
                        c2.SetValue("", "Nén & Chia nhỏ 2GB (.001, .002)...");
                        c2.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c2.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --split 2GB");
                    }

                    using (var c3 = shell.CreateSubKey("OpenStudio"))
                    {
                        c3.SetValue("", "Thêm vào kho nén (Mở Studio)...");
                        c3.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c3.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" --studio \"%1\"");
                    }

                    using (var c4 = shell.CreateSubKey("Estimate"))
                    {
                        c4.SetValue("", "Kiểm tra nhanh tỉ lệ nén...");
                        c4.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c4.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" e \"%1\"");
                    }
                }

                // 2. Context menu for directories (Directory\shell\ZeroZip)
                using (var dirRoot = Registry.CurrentUser.CreateSubKey(DirShellKey))
                {
                    dirRoot.SetValue("", menuTitle);
                    dirRoot.SetValue("MUIVerb", menuTitle);
                    dirRoot.SetValue("Icon", $"\"{exe}\",0");
                    dirRoot.SetValue("SubCommands", "");

                    using var shell = dirRoot.CreateSubKey("shell");

                    using (var c1 = shell.CreateSubKey("CompressDirGui"))
                    {
                        c1.SetValue("", "Nén thư mục này sang .ztar...");
                        c1.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c1.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\"");
                    }

                    using (var c2 = shell.CreateSubKey("CompressDirSplit2G"))
                    {
                        c2.SetValue("", "Nén thư mục chia nhỏ 2GB...");
                        c2.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c2.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" --gui-compress \"%1\" --split 2GB");
                    }

                    using (var c3 = shell.CreateSubKey("OpenStudioDir"))
                    {
                        c3.SetValue("", "Thêm thư mục vào Studio...");
                        c3.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c3.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" --studio \"%1\"");
                    }
                }

                // 3. Context menu for directory background (Directory\Background\shell\ZeroZip)
                using (var bgRoot = Registry.CurrentUser.CreateSubKey(DirBgShellKey))
                {
                    bgRoot.SetValue("", menuTitle);
                    bgRoot.SetValue("MUIVerb", menuTitle);
                    bgRoot.SetValue("Icon", $"\"{exe}\",0");
                    bgRoot.SetValue("SubCommands", "");

                    using var shell = bgRoot.CreateSubKey("shell");

                    using (var c1 = shell.CreateSubKey("CompressCurrentFolder"))
                    {
                        c1.SetValue("", "Nén thư mục hiện tại sang .ztar...");
                        c1.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c1.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" --gui-compress \"%V\"");
                    }
                }

                // 4. File association for .ztar (ZeroZip.Archive)
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

                    using (var shell = archKey.CreateSubKey("shell"))
                    {
                        // Default verb (Double click): Opens GUI to browse archive contents (WinRAR style)
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
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Unregister()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(FileShellKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(DirShellKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(DirBgShellKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(ArchiveClassKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(ExtensionKey, throwOnMissingSubKey: false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
