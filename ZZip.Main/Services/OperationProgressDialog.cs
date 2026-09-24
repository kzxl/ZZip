using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroCompression.Core;
using ZeroCompression.Core.Analysis;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Theme;
using ZZip.Core;
using ZZip.Localization;

namespace ZZip.Main.Services
{
    public enum OperationType
    {
        Compress,
        Extract,
        Test
    }

    public class OperationProgressDialog : BaseForm
    {
        private readonly OperationType _opType;
        private readonly string _sourcePath;
        private readonly string? _destPath;
        private readonly CompressionOptions? _options;
        private readonly string? _password;

        private Label lblOperationTitle = null!;
        private Label lblCurrentFile = null!;
        private ProgressBarControl progressBar = null!;
        private Label lblStats = null!;
        private Label lblSpeed = null!;
        private SimpleButton btnCancel = null!;
        private SimpleButton btnBackground = null!;

        private readonly CancellationTokenSource _cts = new();
        private readonly Stopwatch _stopwatch = new();
        private long _totalExpectedBytes;
        private long _lastBytes;
        private DateTime _lastTime;
        private System.Windows.Forms.Timer _timer = null!;

        public bool IsCompleted { get; private set; }
        public Exception? FailureError { get; private set; }
        private readonly long _splitSize;
        private readonly bool _wrapZip;

        public OperationProgressDialog(OperationType opType, string sourcePath, string? destPath = null,
            CompressionOptions? options = null, string? password = null, long totalExpectedBytes = 0,
            long splitSize = 0, bool wrapZip = false)
        {
            _opType = opType;
            _sourcePath = sourcePath;
            _destPath = destPath;
            _options = options;
            _password = password;
            _totalExpectedBytes = totalExpectedBytes;
            _splitSize = splitSize;
            _wrapZip = wrapZip;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(540, 240);
            this.MinimumSize = new Size(500, 220);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            string opName = _opType switch
            {
                OperationType.Compress => "Đang nén dữ liệu...",
                OperationType.Extract => "Đang giải nén tệp...",
                OperationType.Test => "Đang kiểm tra tính toàn vẹn...",
                _ => "Đang xử lý..."
            };
            this.Text = $"ZZip — {opName}";
            TrySetWindowIcon();

            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 16, 20, 16),
                BackColor = ZeroTheme.Colors.Surface
            };

            lblOperationTitle = new Label
            {
                Text = $"⚡ {opName.ToUpperInvariant()}",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.Primary,
                Location = new Point(18, 14),
                AutoSize = true
            };
            pnlMain.Controls.Add(lblOperationTitle);

            lblCurrentFile = new Label
            {
                Text = Path.GetFileName(_sourcePath),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Location = new Point(20, 40),
                Size = new Size(480, 20),
                AutoEllipsis = true
            };
            pnlMain.Controls.Add(lblCurrentFile);

            progressBar = new ProgressBarControl
            {
                Location = new Point(20, 68),
                Size = new Size(480, 24),
                Value = 0,
                Maximum = 100
            };
            pnlMain.Controls.Add(progressBar);

            lblStats = new Label
            {
                Text = "Đang khởi tạo...",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Location = new Point(20, 100),
                AutoSize = true
            };
            pnlMain.Controls.Add(lblStats);

            lblSpeed = new Label
            {
                Text = "Tốc độ: 0 MB/s",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Location = new Point(340, 100),
                Size = new Size(160, 20),
                TextAlign = ContentAlignment.TopRight
            };
            pnlMain.Controls.Add(lblSpeed);

            btnCancel = new SimpleButton
            {
                Text = "Hủy bỏ",
                ButtonStyle = ZeroButtonStyle.Danger,
                Location = new Point(390, 138),
                Size = new Size(110, 34)
            };
            btnCancel.Click += (s, e) =>
            {
                _cts.Cancel();
                btnCancel.Enabled = false;
                lblStats.Text = "Đang dừng tiến trình...";
            };
            pnlMain.Controls.Add(btnCancel);

            btnBackground = new SimpleButton
            {
                Text = "Chạy nền",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(270, 138),
                Size = new Size(110, 34)
            };
            btnBackground.Click += (s, e) =>
            {
                this.WindowState = FormWindowState.Minimized;
            };
            pnlMain.Controls.Add(btnBackground);

            this.Controls.Add(pnlMain);

            _timer = new System.Windows.Forms.Timer { Interval = 250 };
            _timer.Tick += Timer_Tick;
        }

        protected override async Task RunAfterShown()
        {
            // Safety Check: Verify sufficient free disk space on destination volume before extraction
            if (_opType == OperationType.Extract)
            {
                string destDir = _destPath ?? Path.Combine(Path.GetDirectoryName(_sourcePath) ?? ".",
                    Path.GetFileNameWithoutExtension(_sourcePath) + "_Extracted");

                long expectedSize = _totalExpectedBytes;
                if (expectedSize <= 0)
                {
                    expectedSize = DiskSpaceSafety.EstimateArchiveUncompressedSize(_sourcePath);
                    _totalExpectedBytes = expectedSize;
                }

                if (expectedSize > 0)
                {
                    var check = DiskSpaceSafety.CheckDiskSpace(destDir, expectedSize);
                    if (check.CheckSucceeded && !check.HasEnoughSpace)
                    {
                        string reqStr = DiskSpaceSafety.FormatSize(check.RequiredBytes);
                        string freeStr = DiskSpaceSafety.FormatSize(check.AvailableFreeBytes);
                        string defStr = DiskSpaceSafety.FormatSize(check.DeficitBytes);
                        string title = LocalizationService.Get("Msg_LowDiskSpaceTitle");
                        string msgPattern = LocalizationService.Get("Msg_LowDiskSpaceWarning");
                        string msg = string.Format(msgPattern, reqStr, check.DriveName, freeStr, defStr);

                        var confirm = MessageBox.Show(this, msg, title, MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

                        if (confirm != DialogResult.Yes)
                        {
                            lblStats.Text = "Đã hủy do không đủ dung lượng ổ đĩa.";
                            lblStats.ForeColor = ZeroTheme.Colors.Warning;
                            await Task.Delay(300);
                            this.DialogResult = DialogResult.Cancel;
                            this.Close();
                            return;
                        }
                    }
                }
            }

            // Pre-calculate expected size for compression progress bar
            if (_opType == OperationType.Compress && _totalExpectedBytes <= 0)
            {
                if (File.Exists(_sourcePath))
                {
                    _totalExpectedBytes = new FileInfo(_sourcePath).Length;
                }
                else if (Directory.Exists(_sourcePath))
                {
                    await Task.Run(() =>
                    {
                        long sum = 0;
                        try
                        {
                            foreach (var f in Directory.EnumerateFiles(_sourcePath, "*", SearchOption.AllDirectories))
                            {
                                try { sum += new FileInfo(f).Length; } catch { }
                            }
                        }
                        catch { }
                        _totalExpectedBytes = sum;
                    });
                }
            }

            _stopwatch.Start();
            _lastTime = DateTime.UtcNow;
            _timer.Start();

            try
            {
                await ExecuteOperationAsync();
                IsCompleted = true;
                _timer.Stop();
                progressBar.Value = 100;
                lblStats.Text = "Hoàn tất thành công!";
                lblSpeed.Text = $"Thời gian: {_stopwatch.Elapsed:mm\\:ss}";
                await Task.Delay(400);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (OperationCanceledException)
            {
                _timer.Stop();
                lblStats.Text = "Tiến trình đã bị hủy.";
                await Task.Delay(600);
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
            catch (Exception ex)
            {
                _timer.Stop();
                FailureError = ex;
                lblStats.Text = "Lỗi: " + ex.Message;
                lblStats.ForeColor = ZeroTheme.Colors.Danger;
                btnCancel.Text = "Đóng";
                btnCancel.Enabled = true;
                MessageBox.Show(this, "Thao tác thất bại: " + ex.Message, "Lỗi ZZip", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.Abort;
            }
        }

        private async Task ExecuteOperationAsync()
        {
            var progress = new Progress<long>(ReportProgress);

            if (_opType == OperationType.Compress)
            {
                string target = _destPath ?? (Directory.Exists(_sourcePath)
                    ? _sourcePath.TrimEnd('\\', '/') + ".zz"
                    : Path.ChangeExtension(_sourcePath, ".zz"));

                if (target.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Run(() =>
                    {
                        StandardZip.CompressToZip(_sourcePath, target, progress);
                    }, _cts.Token);
                    return;
                }

                var opts = _options ?? DataClassifier.ClassifyPath(_sourcePath).CreateOptions(CompressionProfile.Ultra);
                opts.Password = _password ?? opts.Password;
                opts.Workers = Environment.ProcessorCount; // High performance parallel compute

                bool isSfx = target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

                await Task.Run(() =>
                {
                    var builder = new SfxBuilderService();
                    builder.BuildPackage(_sourcePath, target, _splitSize, opts, isSfx, progress, _cts.Token);
                    if (_wrapZip)
                    {
                        builder.WrapForTransport(target);
                    }
                }, _cts.Token);
            }
            else if (_opType == OperationType.Extract)
            {
                string destDir = _destPath ?? Path.Combine(Path.GetDirectoryName(_sourcePath) ?? ".",
                    Path.GetFileNameWithoutExtension(_sourcePath) + "_Extracted");

                if (_sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Run(() =>
                    {
                        Directory.CreateDirectory(destDir);
                        System.IO.Compression.ZipFile.ExtractToDirectory(_sourcePath, destDir, overwriteFiles: true);
                    }, _cts.Token);
                    return;
                }

                await Task.Run(() =>
                {
                    var footer = SfxComposer.ReadFooter(_sourcePath);
                    if (footer == null) throw new InvalidDataException("Tệp không phải gói ZZip hợp lệ.");

                    using var payload = SfxComposer.OpenPayload(_sourcePath, footer);
                    ZtarEngine.UnpackVerified(payload, destDir, footer.Method, _password, footer.IsPrecompressed,
                        progress, footer.WindowLog, footer.Crc32, _cts.Token);
                }, _cts.Token);
            }
            else if (_opType == OperationType.Test)
            {
                await Task.Run(() =>
                {
                    var footer = SfxComposer.ReadFooter(_sourcePath);
                    if (footer == null) throw new InvalidDataException("Tệp không phải gói ZZip hợp lệ.");

                    using var payload = SfxComposer.OpenPayload(_sourcePath, footer);
                    bool ok = ZtarEngine.TestArchive(payload, footer.Method, _password, footer.IsPrecompressed,
                        footer.WindowLog, progress, _cts.Token);
                    if (!ok) throw new InvalidDataException("Dữ liệu nén bị hỏng (CRC32 checksum không khớp).");
                }, _cts.Token);
            }
        }

        private void ReportProgress(long bytesProcessed)
        {
            if (this.IsDisposed) return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => ReportProgress(bytesProcessed)));
                return;
            }

            if (_totalExpectedBytes > 0)
            {
                int pct = (int)Math.Clamp((bytesProcessed * 100) / _totalExpectedBytes, 0, 100);
                progressBar.Value = pct;
                lblStats.Text = $"{FormatSize(bytesProcessed)} / {FormatSize(_totalExpectedBytes)} ({pct}%)";
            }
            else
            {
                progressBar.IsIndeterminate = true;
                lblStats.Text = $"Đã xử lý: {FormatSize(bytesProcessed)}";
            }

            _lastBytes = bytesProcessed;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            double seconds = (now - _lastTime).TotalSeconds;
            if (seconds >= 0.5 && _lastBytes > 0)
            {
                double speed = _lastBytes / Math.Max(0.1, _stopwatch.Elapsed.TotalSeconds);
                lblSpeed.Text = $"{FormatSize((long)speed)}/s | {_stopwatch.Elapsed:mm\\:ss}";
            }
        }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double b = bytes;
            int u = 0;
            while (b >= 1024 && u < units.Length - 1)
            {
                b /= 1024;
                u++;
            }
            return $"{b:F1} {units[u]}";
        }

        private void TrySetWindowIcon()
        {
            try
            {
                string exe = Environment.ProcessPath ?? "";
                if (File.Exists(exe))
                {
                    var ico = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                    if (ico != null) this.Icon = ico;
                }
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Dispose();
                _cts?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
