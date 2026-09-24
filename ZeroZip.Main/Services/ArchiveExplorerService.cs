using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroCompression.Core;
using ZeroZip.Core;

namespace ZeroZip.Main.Services
{
    public sealed class ArchiveExplorerService
    {
        private readonly List<ArchiveEntry> _allEntries = new();
        private ZtarFooter? _currentFooter;
        private string? _currentArchivePath;
        private string _currentVirtualPath = "";

        public string? CurrentArchivePath => _currentArchivePath;
        public ZtarFooter? CurrentFooter => _currentFooter;
        public string CurrentVirtualPath => _currentVirtualPath;
        public IReadOnlyList<ArchiveEntry> AllEntries => _allEntries;

        /// <summary>
        /// Loads and parses the archive directory structure.
        /// </summary>
        public async Task<ArchiveLoadResult> LoadArchiveAsync(string archivePath, string? password = null, CancellationToken cancel = default)
        {
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("Không tìm thấy tệp lưu trữ.", archivePath);

            return await Task.Run(() =>
            {
                var footer = SfxComposer.ReadFooter(archivePath);
                if (footer == null)
                {
                    return new ArchiveLoadResult
                    {
                        Success = false,
                        ErrorMessage = "Tệp không phải định dạng ZeroZip (.ztar hoặc SFX) hợp lệ hoặc thiếu footer ZTAR."
                    };
                }

                if (footer.IsEncrypted && string.IsNullOrEmpty(password))
                {
                    return new ArchiveLoadResult
                    {
                        Success = false,
                        IsEncrypted = true,
                        ErrorMessage = "Gói nén đã được mã hóa bằng AES-256-GCM. Vui lòng nhập mật khẩu."
                    };
                }

                _currentArchivePath = archivePath;
                _currentFooter = footer;
                _currentVirtualPath = "";
                _allEntries.Clear();

                using (var payload = SfxComposer.OpenPayload(archivePath, footer))
                {
                    var entries = ZtarEngine.ListEntries(payload, footer.Method,
                        footer.IsEncrypted ? password : null,
                        footer.IsPrecompressed,
                        footer.WindowLog,
                        cancel);

                    _allEntries.AddRange(entries);
                }

                return new ArchiveLoadResult
                {
                    Success = true,
                    Footer = footer,
                    TotalEntries = _allEntries.Count,
                    TotalOriginalSize = _allEntries.Where(e => !e.IsDirectory).Sum(e => e.Size)
                };
            }, cancel);
        }

        /// <summary>
        /// Closes the currently loaded archive and resets internal state.
        /// </summary>
        public void CloseArchive()
        {
            _currentArchivePath = null;
            _currentFooter = null;
            _currentVirtualPath = "";
            _allEntries.Clear();
        }

        /// <summary>
        /// Retrieves visible entries within the current virtual directory.
        /// </summary>
        public IReadOnlyList<VirtualItem> GetCurrentDirectoryItems()
        {
            string prefix = NormalizePath(_currentVirtualPath);
            var result = new Dictionary<string, VirtualItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _allEntries)
            {
                string norm = entry.Name.Replace('\\', '/').TrimStart('/');
                if (!norm.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string relative = norm.Substring(prefix.Length).TrimStart('/');
                if (string.IsNullOrEmpty(relative))
                    continue;

                int slashIndex = relative.IndexOf('/');
                if (slashIndex >= 0)
                {
                    // It's a subdirectory
                    string dirName = relative.Substring(0, slashIndex);
                    if (!result.ContainsKey(dirName))
                    {
                        result[dirName] = new VirtualItem
                        {
                            Name = dirName,
                            RelativePath = prefix + dirName + "/",
                            IsDirectory = true,
                            Size = 0,
                            ModificationTime = entry.ModificationTime
                        };
                    }
                }
                else
                {
                    // It's a file or direct child directory
                    if (!result.ContainsKey(relative))
                    {
                        result[relative] = new VirtualItem
                        {
                            Name = relative,
                            RelativePath = norm,
                            IsDirectory = entry.IsDirectory,
                            Size = entry.Size,
                            ModificationTime = entry.ModificationTime
                        };
                    }
                }
            }

            return result.Values.OrderByDescending(v => v.IsDirectory).ThenBy(v => v.Name).ToList();
        }

        public bool NavigateTo(string subfolder)
        {
            string clean = subfolder.Trim('/', '\\');
            _currentVirtualPath = string.IsNullOrEmpty(_currentVirtualPath) ? clean + "/" : _currentVirtualPath.TrimEnd('/') + "/" + clean + "/";
            return true;
        }

        public bool NavigateUp()
        {
            if (string.IsNullOrEmpty(_currentVirtualPath) || _currentVirtualPath == "/")
            {
                _currentVirtualPath = "";
                return false;
            }

            string trimmed = _currentVirtualPath.TrimEnd('/');
            int lastSlash = trimmed.LastIndexOf('/');
            _currentVirtualPath = lastSlash >= 0 ? trimmed.Substring(0, lastSlash + 1) : "";
            return true;
        }

        public void NavigateRoot()
        {
            _currentVirtualPath = "";
        }

        /// <summary>
        /// Extracts selected entries or the entire archive to a destination directory.
        /// </summary>
        public async Task ExtractAsync(string destinationDir, ISet<string>? selectedRelativePaths = null,
            string? password = null, IProgress<long>? progress = null, CancellationToken cancel = default)
        {
            if (_currentArchivePath == null || _currentFooter == null)
                throw new InvalidOperationException("Chưa mở gói nén nào.");

            await Task.Run(() =>
            {
                using var payload = SfxComposer.OpenPayload(_currentArchivePath, _currentFooter);
                ZtarEngine.ExtractSelected(payload, destinationDir, selectedRelativePaths,
                    _currentFooter.Method,
                    _currentFooter.IsEncrypted ? password : null,
                    _currentFooter.IsPrecompressed,
                    progress,
                    _currentFooter.WindowLog,
                    cancel);
            }, cancel);
        }

        /// <summary>
        /// Extracts a single file to a temporary file path for viewing/previewing.
        /// </summary>
        public async Task<string> ExtractSingleToTempAsync(string relativePath, string? password = null, CancellationToken cancel = default)
        {
            if (_currentArchivePath == null || _currentFooter == null)
                throw new InvalidOperationException("Chưa mở gói nén nào.");

            string tempDir = Path.Combine(Path.GetTempPath(), "ZeroZip_View_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { relativePath };
            await ExtractAsync(tempDir, set, password, null, cancel);

            string targetFile = Path.Combine(tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(targetFile))
            {
                // In case entry was relative without full folder hierarchy
                string fallback = Path.Combine(tempDir, Path.GetFileName(relativePath));
                if (File.Exists(fallback)) return fallback;
                throw new FileNotFoundException("Không thể trích xuất tệp tạm thời để xem.", relativePath);
            }

            return targetFile;
        }

        /// <summary>
        /// Tests the integrity of the archive payload and CRC32 checksum.
        /// </summary>
        public async Task<bool> TestArchiveAsync(string? password = null, IProgress<long>? progress = null, CancellationToken cancel = default)
        {
            if (_currentArchivePath == null || _currentFooter == null)
                throw new InvalidOperationException("Chưa mở gói nén nào.");

            return await Task.Run(() =>
            {
                using var payload = SfxComposer.OpenPayload(_currentArchivePath, _currentFooter);
                return ZtarEngine.TestArchive(payload, _currentFooter.Method,
                    _currentFooter.IsEncrypted ? password : null,
                    _currentFooter.IsPrecompressed,
                    _currentFooter.WindowLog,
                    progress,
                    cancel);
            }, cancel);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string p = path.Replace('\\', '/').Trim('/');
            return string.IsNullOrEmpty(p) ? "" : p + "/";
        }
    }

    public sealed class ArchiveLoadResult
    {
        public bool Success { get; init; }
        public bool IsEncrypted { get; init; }
        public string? ErrorMessage { get; init; }
        public ZtarFooter? Footer { get; init; }
        public int TotalEntries { get; init; }
        public long TotalOriginalSize { get; init; }
    }

    public sealed class VirtualItem
    {
        public required string Name { get; init; }
        public required string RelativePath { get; init; }
        public bool IsDirectory { get; init; }
        public long Size { get; init; }
        public DateTimeOffset ModificationTime { get; init; }
    }
}
