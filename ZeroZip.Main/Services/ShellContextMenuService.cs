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

        public static bool Register(string? executablePath = null)
        {
            try
            {
                var exe = executablePath ?? Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                {
                    return false;
                }

                exe = Path.GetFullPath(exe);

                // 1. Context menu for individual files
                using (var root = Registry.CurrentUser.CreateSubKey(FileShellKey))
                {
                    root.SetValue("", "ZeroZip — Ultra Compress");
                    root.SetValue("Icon", $"\"{exe}\",0");
                    root.SetValue("SubCommands", "");

                    using var shell = root.CreateSubKey("shell");

                    using (var c1 = shell.CreateSubKey("Compress"))
                    {
                        c1.SetValue("", "Nén sang .ztar (Ultra Zstandard)");
                        c1.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c1.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" c \"%1\"");
                    }

                    using (var c2 = shell.CreateSubKey("CompressSplit2G"))
                    {
                        c2.SetValue("", "Nén & Chia nhỏ 2GB (.001, .002)");
                        c2.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c2.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" c \"%1\" --split 2GB");
                    }

                    using (var c3 = shell.CreateSubKey("Estimate"))
                    {
                        c3.SetValue("", "Kiểm tra nhanh tỉ lệ nén");
                        c3.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c3.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" e \"%1\"");
                    }
                }

                // 2. Context menu for directories
                using (var dirRoot = Registry.CurrentUser.CreateSubKey(DirShellKey))
                {
                    dirRoot.SetValue("", "ZeroZip — Ultra Compress Thư mục");
                    dirRoot.SetValue("Icon", $"\"{exe}\",0");
                    dirRoot.SetValue("SubCommands", "");

                    using var shell = dirRoot.CreateSubKey("shell");

                    using (var c1 = shell.CreateSubKey("CompressDir"))
                    {
                        c1.SetValue("", "Nén thư mục này (.ztar)");
                        c1.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c1.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" c \"%1\"");
                    }

                    using (var c2 = shell.CreateSubKey("CompressDirSplit2G"))
                    {
                        c2.SetValue("", "Nén thư mục chia nhỏ 2GB (.001, .002)");
                        c2.SetValue("Icon", $"\"{exe}\",0");
                        using var cmd = c2.CreateSubKey("command");
                        cmd.SetValue("", $"\"{exe}\" c \"%1\" --split 2GB");
                    }
                }

                // 3. File association for .ztar
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
                        using (var openKey = shell.CreateSubKey("open"))
                        {
                            openKey.SetValue("", "Giải nén bằng ZeroZip");
                            using var cmd = openKey.CreateSubKey("command");
                            cmd.SetValue("", $"\"{exe}\" x \"%1\"");
                        }

                        using (var extHereKey = shell.CreateSubKey("ExtractHere"))
                        {
                            extHereKey.SetValue("", "Giải nén tại đây");
                            using var cmd = extHereKey.CreateSubKey("command");
                            cmd.SetValue("", $"\"{exe}\" x \"%1\"");
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
