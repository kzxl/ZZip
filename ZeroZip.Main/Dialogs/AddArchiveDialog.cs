using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroCompression.Core;
using ZeroCompression.Core.Analysis;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Theme;
using ZeroZip.Core;
using ZeroZip.Main.Services;

namespace ZeroZip.Main.Dialogs
{
    /// <summary>
    /// Dedicated "Add to Archive" configuration dialog (WinRAR & 7-Zip style).
    /// Allows users to specify source data, destination archive name (.zz, .zip, .exe),
    /// compression algorithm, dictionary window, split volumes, and AES-256-GCM encryption.
    /// </summary>
    public class AddArchiveDialog : BaseForm
    {
        private readonly string? _initialSource;
        private readonly SfxBuilderService _sfxService;
        private CancellationTokenSource? _cts;

        // UI Controls
        private Card cardSource = null!;
        private Card cardEngine = null!;
        private Card cardSecurity = null!;
        private ButtonEdit txtSource = null!;
        private SimpleButton btnBrowseFolder = null!;
        private SimpleButton btnBrowseFile = null!;
        private ButtonEdit txtDest = null!;
        private SimpleButton btnBrowseDest = null!;
        private ComboBoxEdit cmbMethod = null!;
        private ComboBoxEdit cmbProfile = null!;
        private ComboBoxEdit cmbSplit = null!;
        private CheckEdit chkWrapZip = null!;
        private CheckEdit chkPrecomp = null!;
        private CheckEdit chkLong = null!;
        private Label lblAutoDetectBadge = null!;
        private TextEdit txtPassword = null!;
        private ProgressBarControl progressBar = null!;
        private Label lblStatus = null!;
        private SimpleButton btnEstimate = null!;
        private SimpleButton btnStart = null!;
        private SimpleButton btnCancel = null!;

        private Card cardAction = null!;

        public string? ResultArchivePath { get; private set; }
        public bool IsSuccess { get; private set; }

        public AddArchiveDialog(string? initialSource = null)
        {
            _initialSource = initialSource;
            _sfxService = new SfxBuilderService();

            InitializeComponent();
            ApplyLocalization();

            if (!string.IsNullOrEmpty(_initialSource))
            {
                txtSource.Text = _initialSource;
                AutoSuggestDest(_initialSource);
                AutoDetectAndTune(_initialSource);
            }
        }

        private void InitializeComponent()
        {
            this.Text = LocalizationService.Get("Dialog_AddArchive_Title");
            this.Size = new Size(840, 800);
            this.MinimumSize = new Size(780, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;

            var pnlScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12)
            };

            int currentY = 10;
            const int cardWidth = 790;

            // --- Card 1: Nguồn & Tệp đích ---
            cardSource = new Card
            {
                Title = LocalizationService.Get("Studio_Card1_Title"),
                Subtitle = LocalizationService.Get("Studio_Card1_Sub"),
                StepNumber = 1,
                AutoFitContent = false,
                Size = new Size(cardWidth, 195),
                Location = new Point(12, currentY),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblSrc = new Label { Text = LocalizationService.Get("Studio_LblSource"), Location = new Point(14, 6), AutoSize = true };
            cardSource.ContentPanel.Controls.Add(lblSrc);

            txtSource = new ButtonEdit
            {
                Location = new Point(14, 26),
                Size = new Size(525, 32),
                PlaceholderText = "Chọn hoặc kéo thả thư mục / tệp cần nén..."
            };
            txtSource.ButtonClick += (s, e) => BrowseSourceFolder();
            txtSource.TextChanged += (s, e) => AutoDetectAndTune(txtSource.Text);
            cardSource.ContentPanel.Controls.Add(txtSource);

            btnBrowseFolder = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnBrowseFolder"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(545, 26),
                Size = new Size(115, 32)
            };
            btnBrowseFolder.Click += (s, e) => BrowseSourceFolder();
            cardSource.ContentPanel.Controls.Add(btnBrowseFolder);

            btnBrowseFile = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnBrowseFile"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(666, 26),
                Size = new Size(105, 32)
            };
            btnBrowseFile.Click += (s, e) => BrowseSourceFile();
            cardSource.ContentPanel.Controls.Add(btnBrowseFile);

            var lblDst = new Label { Text = LocalizationService.Get("Studio_LblDestSave"), Location = new Point(14, 66), AutoSize = true };
            cardSource.ContentPanel.Controls.Add(lblDst);

            txtDest = new ButtonEdit
            {
                Location = new Point(14, 86),
                Size = new Size(645, 32),
                PlaceholderText = "Đường dẫn kho lưu trữ (.zz, .zip hoặc .exe SFX)..."
            };
            txtDest.ButtonClick += (s, e) => BrowseDest();
            cardSource.ContentPanel.Controls.Add(txtDest);

            btnBrowseDest = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnBrowseDest"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(666, 86),
                Size = new Size(105, 32)
            };
            btnBrowseDest.Click += (s, e) => BrowseDest();
            cardSource.ContentPanel.Controls.Add(btnBrowseDest);

            pnlScroll.Controls.Add(cardSource);

            // --- Card 2: Thuật toán & Cấu hình nén ZeroUniverse ---
            cardEngine = new Card
            {
                Title = LocalizationService.Get("Studio_Card2_Title"),
                Subtitle = LocalizationService.Get("Studio_Card2_Sub"),
                StepNumber = 2,
                AutoFitContent = false,
                Size = new Size(cardWidth, 248),
                Location = new Point(12, 215),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblMethod = new Label { Text = LocalizationService.Get("Studio_LblMethod"), Location = new Point(14, 6), AutoSize = true };
            cardEngine.ContentPanel.Controls.Add(lblMethod);

            cmbMethod = new ComboBoxEdit
            {
                Location = new Point(14, 26),
                Size = new Size(260, 32)
            };
            cmbMethod.Items.Add("⭐ Tự động nhận diện (Adaptive Auto-Detect)");
            cmbMethod.Items.Add("Zstandard (Nhanh, đa luồng, GB/s)");
            cmbMethod.Items.Add("LZMA (Nén sâu nhất)");
            cmbMethod.Items.Add("Brotli (Văn bản / Web / Code)");
            cmbMethod.Items.Add("Không nén (Store)");
            cmbMethod.Items.Add("ZeroTelemetry (Gorilla XOR)");
            cmbMethod.SelectedIndex = 0;
            cardEngine.ContentPanel.Controls.Add(cmbMethod);

            var lblProfile = new Label { Text = LocalizationService.Get("Studio_LblProfile"), Location = new Point(285, 6), AutoSize = true };
            cardEngine.ContentPanel.Controls.Add(lblProfile);

            cmbProfile = new ComboBoxEdit
            {
                Location = new Point(285, 26),
                Size = new Size(180, 32)
            };
            cmbProfile.Items.Add("Nhanh (Fast)");
            cmbProfile.Items.Add("Cân bằng (Normal)");
            cmbProfile.Items.Add("Siêu nén (Ultra)");
            cmbProfile.SelectedIndex = 2;
            cardEngine.ContentPanel.Controls.Add(cmbProfile);

            var lblSplit = new Label { Text = LocalizationService.Get("Studio_LblSplit"), Location = new Point(475, 6), AutoSize = true };
            cardEngine.ContentPanel.Controls.Add(lblSplit);

            cmbSplit = new ComboBoxEdit
            {
                Location = new Point(475, 26),
                Size = new Size(295, 32)
            };
            cmbSplit.Items.Add("Nhúng trực tiếp (1 tệp)");
            cmbSplit.Items.Add("Cắt mảnh 2GB (FAT32 an toàn)");
            cmbSplit.Items.Add("Cắt mảnh 4GB (Tiêu chuẩn ISO)");
            cmbSplit.SelectedIndex = 0;
            cardEngine.ContentPanel.Controls.Add(cmbSplit);

            // Row 2a: Checkboxes 1 & 2 (Clean 2-row layout prevents text truncation)
            chkWrapZip = new CheckEdit
            {
                Text = "Bọc ZIP bảo vệ (gửi qua mail/web)",
                Location = new Point(14, 68),
                Size = new Size(345, 24)
            };
            cardEngine.ContentPanel.Controls.Add(chkWrapZip);

            bool precompOk = _sfxService.IsPrecompAvailable();
            chkPrecomp = new CheckEdit
            {
                Text = precompOk ? "Nén sâu repack (precomp cho ảnh/game)" : "Nén repack (chưa có precomp.exe)",
                Location = new Point(370, 68),
                Size = new Size(390, 24),
                Enabled = precompOk
            };
            cardEngine.ContentPanel.Controls.Add(chkPrecomp);

            // Row 2b: Checkbox 3
            chkLong = new CheckEdit
            {
                Text = "Khử trùng lặp khoảng cách xa (Zstd LDM 2GB)",
                Location = new Point(14, 96),
                Size = new Size(420, 24)
            };
            cardEngine.ContentPanel.Controls.Add(chkLong);

            // Row 3: Elegant styled recommendation banner
            var pnlBadge = new Panel
            {
                Location = new Point(14, 126),
                Size = new Size(cardWidth - 45, 46),
                BackColor = ZeroTheme.IsDark ? Color.FromArgb(30, 38, 54) : Color.FromArgb(242, 246, 255),
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 0, 0, 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            ZeroTheme.ThemeChanged += (s, e) =>
            {
                if (!pnlBadge.IsDisposed)
                {
                    pnlBadge.BackColor = ZeroTheme.IsDark ? Color.FromArgb(30, 38, 54) : Color.FromArgb(242, 246, 255);
                }
            };

            lblAutoDetectBadge = new Label
            {
                Dock = DockStyle.Fill,
                Text = "💡 Chưa nạp dữ liệu phân tích.",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = ZeroTheme.Colors.Primary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            pnlBadge.Controls.Add(lblAutoDetectBadge);
            cardEngine.ContentPanel.Controls.Add(pnlBadge);

            pnlScroll.Controls.Add(cardEngine);

            // --- Card 3: Bảo mật & Mã hóa ---
            cardSecurity = new Card
            {
                Title = LocalizationService.Get("Studio_Card3_Title"),
                Subtitle = LocalizationService.Get("Studio_Card3_Sub"),
                StepNumber = 3,
                AutoFitContent = false,
                Size = new Size(cardWidth, 185),
                Location = new Point(12, 473),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblPass = new Label { Text = LocalizationService.Get("Studio_LblPassword"), Location = new Point(14, 6), AutoSize = true };
            cardSecurity.ContentPanel.Controls.Add(lblPass);

            txtPassword = new TextEdit
            {
                Location = new Point(14, 26),
                Size = new Size(500, 32),
                PlaceholderText = "Mật khẩu mã hóa (tùy chọn)...",
                UseSystemPasswordChar = true,
                ShowPasswordEyeButton = true
            };
            cardSecurity.ContentPanel.Controls.Add(txtPassword);

            var lblCryptoNotice = new Label
            {
                Text = "🔒 Mã hóa có xác thực chuẩn quân sự AES-256-GCM AEAD (Chống giả mạo dữ liệu).",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Location = new Point(14, 68),
                Size = new Size(cardWidth - 45, 22),
                Margin = new Padding(0, 0, 0, 18),
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cardSecurity.ContentPanel.Controls.Add(lblCryptoNotice);

            pnlScroll.Controls.Add(cardSecurity);

            // --- Card 4: Thực thi & Tiến trình ---
            cardAction = new Card
            {
                Title = LocalizationService.Get("Studio_Card4_Title"),
                Subtitle = LocalizationService.Get("Studio_Card4_Sub"),
                StepNumber = 4,
                AutoFitContent = false,
                Size = new Size(cardWidth, 145),
                Location = new Point(12, 658),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            progressBar = new ProgressBarControl
            {
                Location = new Point(14, 12),
                Size = new Size(cardWidth - 45, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cardAction.ContentPanel.Controls.Add(progressBar);

            lblStatus = new Label
            {
                Text = "Sẵn sàng thực hiện.",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Location = new Point(14, 46),
                Size = new Size(cardWidth - 45, 22),
                Margin = new Padding(0, 0, 0, 18),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cardAction.ContentPanel.Controls.Add(lblStatus);

            pnlScroll.Controls.Add(cardAction);

            // --- Pinned Bottom Action Bar (WinRAR & 7-Zip Standard) ---
            var pnlBottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = ZeroTheme.Colors.Surface,
                Padding = new Padding(16, 8, 16, 8)
            };
            pnlBottomBar.Paint += (s, e) =>
            {
                using var pen = new Pen(ZeroTheme.Colors.Border, 1);
                e.Graphics.DrawLine(pen, 0, 0, pnlBottomBar.Width, 0);
            };
            ZeroTheme.ThemeChanged += (s, e) =>
            {
                if (!pnlBottomBar.IsDisposed)
                {
                    pnlBottomBar.BackColor = ZeroTheme.Colors.Surface;
                    pnlBottomBar.Invalidate();
                }
            };

            var lblEngineBrand = new Label
            {
                Dock = DockStyle.Left,
                Text = "⚡ Powered by ZeroUniverse Sovereign Codecs",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                AutoSize = true
            };
            pnlBottomBar.Controls.Add(lblEngineBrand);

            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 4, 8, 4),
                WrapContents = false
            };

            btnStart = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnCompress"),
                ButtonStyle = ZeroButtonStyle.Primary,
                Size = new Size(170, 38),
                Margin = new Padding(0, 0, 8, 0)
            };
            btnStart.Click += BtnStart_Click;
            pnlButtons.Controls.Add(btnStart);

            btnEstimate = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnEstimate"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(150, 38),
                Margin = new Padding(0, 0, 8, 0)
            };
            btnEstimate.Click += BtnEstimate_Click;
            pnlButtons.Controls.Add(btnEstimate);

            btnCancel = new SimpleButton
            {
                Text = "Đóng",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(95, 38),
                Margin = new Padding(0)
            };
            btnCancel.Click += (s, e) =>
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                    lblStatus.Text = "Đang hủy...";
                }
                else
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            };
            pnlButtons.Controls.Add(btnCancel);

            pnlBottomBar.Controls.Add(pnlButtons);

            // Correct docking order: Bottom docked control added, then Fill docked control added and brought to front
            this.Controls.Add(pnlBottomBar);
            this.Controls.Add(pnlScroll);
            pnlScroll.BringToFront();

            // Flow layout updater: automatically stacks all cards without overlapping
            void UpdateCardsLayout()
            {
                int y = 10;
                int pad = 12;
                int effectiveWidth = Math.Max(730, pnlScroll.ClientSize.Width - (pad * 2));

                Card[] cards = [cardSource, cardEngine, cardSecurity, cardAction];
                foreach (var card in cards)
                {
                    if (card == null) continue;
                    card.Location = new Point(pad, y);
                    card.Width = effectiveWidth;
                    y += card.Height + 14;
                }

                pnlScroll.AutoScrollMinSize = new Size(0, y + 10);
            }

            pnlScroll.Resize += (s, e) => UpdateCardsLayout();
            this.Shown += (s, e) => UpdateCardsLayout();
            UpdateCardsLayout();
        }

        private void ApplyLocalization()
        {
            this.Text = LocalizationService.Get("Dialog_AddArchive_Title");
            if (cardSource != null)
            {
                cardSource.Title = LocalizationService.Get("Studio_Card1_Title");
                cardSource.Subtitle = LocalizationService.Get("Studio_Card1_Sub");
            }
            if (btnBrowseFolder != null) btnBrowseFolder.Text = LocalizationService.Get("Studio_BtnBrowseFolder");
            if (btnBrowseFile != null) btnBrowseFile.Text = LocalizationService.Get("Studio_BtnBrowseFile");
            if (btnBrowseDest != null) btnBrowseDest.Text = LocalizationService.Get("Studio_BtnBrowseDest");

            if (cardEngine != null)
            {
                cardEngine.Title = LocalizationService.Get("Studio_Card2_Title");
                cardEngine.Subtitle = LocalizationService.Get("Studio_Card2_Sub");
            }
            if (cardSecurity != null)
            {
                cardSecurity.Title = LocalizationService.Get("Studio_Card3_Title");
                cardSecurity.Subtitle = LocalizationService.Get("Studio_Card3_Sub");
            }
            if (cardAction != null)
            {
                cardAction.Title = LocalizationService.Get("Studio_Card4_Title");
                cardAction.Subtitle = LocalizationService.Get("Studio_Card4_Sub");
            }
            if (btnStart != null) btnStart.Text = LocalizationService.Get("Studio_BtnCompress");
            if (btnEstimate != null) btnEstimate.Text = LocalizationService.Get("Studio_BtnEstimate");
        }

        private void BrowseSourceFolder()
        {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtSource.Text = fbd.SelectedPath;
                AutoSuggestDest(fbd.SelectedPath);
                AutoDetectAndTune(fbd.SelectedPath);
            }
        }

        private void BrowseSourceFile()
        {
            using var ofd = new OpenFileDialog { Title = "Chọn tệp cần nén" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txtSource.Text = ofd.FileName;
                AutoSuggestDest(ofd.FileName);
                AutoDetectAndTune(ofd.FileName);
            }
        }

        private void AutoSuggestDest(string sourcePath)
        {
            if (string.IsNullOrEmpty(txtDest.Text))
            {
                string? dir = Path.GetDirectoryName(sourcePath);
                string name = Path.GetFileName(sourcePath);
                if (string.IsNullOrEmpty(name)) name = "Archive";
                txtDest.Text = Path.Combine(dir ?? "", name + ".zz");
            }
        }

        private void BrowseDest()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Kho lưu trữ ZeroZip (*.zz)|*.zz|Executable SFX (*.exe)|*.exe|Tệp nén ZIP (*.zip)|*.zip|Kho lưu trữ ZTar (*.ztar)|*.ztar",
                Title = "Lưu file nén / SFX"
            };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                txtDest.Text = sfd.FileName;
            }
        }

        private void AutoDetectAndTune(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !(Directory.Exists(path) || File.Exists(path))) return;

            Task.Run(() =>
            {
                try
                {
                    var cls = DataClassifier.ClassifyPath(path);
                    if (this.IsDisposed) return;
                    this.BeginInvoke(new Action(() =>
                    {
                        lblAutoDetectBadge.Text = $"💡 Nhận diện: [{cls.DetectedType}] -> Khuyến nghị: {cls.RecommendedMethod} | {cls.Reason}";
                    }));
                }
                catch { }
            });
        }

        private CompressionMethod SelectedMethod() => cmbMethod.SelectedIndex switch
        {
            2 => CompressionMethod.Lzma,
            3 => CompressionMethod.Brotli,
            4 => CompressionMethod.Store,
            5 => CompressionMethod.ZeroTelemetry,
            _ => CompressionMethod.Zstd,
        };

        private CompressionProfile SelectedProfile() => cmbProfile.SelectedIndex switch
        {
            0 => CompressionProfile.Fast,
            1 => CompressionProfile.Normal,
            _ => CompressionProfile.Ultra,
        };

        private async void BtnEstimate_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSource.Text) || !(Directory.Exists(txtSource.Text) || File.Exists(txtSource.Text)))
            {
                ShowToast("Vui lòng chọn nguồn hợp lệ trước.", "Lỗi Nguồn", ToastType.Warning);
                return;
            }

            btnEstimate.Enabled = false;
            string src = txtSource.Text;
            lblStatus.Text = "Đang lấy mẫu và phân tích dữ liệu...";

            try
            {
                var estTask = Task.Run(() => _sfxService.Estimate(src));
                var classifyTask = Task.Run(() => DataClassifier.ClassifyPath(src));
                await Task.WhenAll(estTask, classifyTask);

                var est = estTask.Result;
                var cls = classifyTask.Result;

                lblStatus.Text = $"[{cls.DetectedType}] -> {cls.RecommendedMethod} | {est.Summary()}";
                ShowToast($"Nhận diện: {cls.DetectedType} -> Khuyến nghị: {cls.RecommendedMethod}\n{est.Summary()}", "Nhận diện & Ước tính", ToastType.Info);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Lỗi phân tích: " + ex.Message;
                ShowToast(ex.Message, "Lỗi phân tích", ToastType.Error);
            }
            finally
            {
                btnEstimate.Enabled = true;
            }
        }

        private async void BtnStart_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSource.Text) || string.IsNullOrWhiteSpace(txtDest.Text))
            {
                ShowToast("Vui lòng nhập đầy đủ nguồn và đích.", "Thiếu thông tin", ToastType.Warning);
                return;
            }

            btnStart.Enabled = false;
            btnEstimate.Enabled = false;
            btnCancel.Text = "Dừng Lại";
            _cts = new CancellationTokenSource();
            progressBar.IsIndeterminate = true;

            string source = txtSource.Text;
            string dest = txtDest.Text;
            long splitSize = 0;
            if (cmbSplit.SelectedIndex == 1) splitSize = 2L * 1024 * 1024 * 1024;
            if (cmbSplit.SelectedIndex == 2) splitSize = 4L * 1024 * 1024 * 1024;

            var profile = SelectedProfile();
            string? password = string.IsNullOrEmpty(txtPassword.Text) ? null : txtPassword.Text;
            bool wrapZip = chkWrapZip.Checked;
            bool usePrecomp = chkPrecomp.Checked;
            bool longMode = chkLong.Checked;

            bool isAuto = cmbMethod.SelectedIndex == 0;
            DataClassificationResult? classified = null;
            if (isAuto)
            {
                classified = DataClassifier.ClassifyPath(source);
                lblStatus.Text = $"Tự động nhận diện: [{classified.DetectedType}] -> Áp dụng {classified.RecommendedMethod}. Đang nén...";
            }
            else
            {
                var method = SelectedMethod();
                lblStatus.Text = $"Đang nén [{method} / {profile}{(usePrecomp ? " / precomp" : "")}]. Vui lòng đợi...";
            }

            var progress = new Progress<long>(done =>
            {
                lblStatus.Text = $"Đang nén... đã xử lý {FormatSize(done)}";
            });

            try
            {
                CompressionOptions options;
                if (classified != null)
                {
                    options = classified.CreateOptions(profile);
                }
                else
                {
                    options = CompressionOptions.FromProfile(profile, SelectedMethod());
                }

                options.Password = password;
                options.UsePrecomp = usePrecomp;
                options.Workers = Environment.ProcessorCount;
                if (longMode)
                {
                    options.LongDistanceMatching = true;
                    if (options.WindowLog < CompressionOptions.MaxLongWindowLog)
                        options.WindowLog = CompressionOptions.MaxLongWindowLog;
                }

                var token = _cts.Token;
                PackResult result;
                if (dest.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Run(() => StandardZip.CompressToZip(source, dest, progress), token);
                    long origSize = File.Exists(source) ? new FileInfo(source).Length : 0;
                    if (Directory.Exists(source))
                    {
                        foreach (var f in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                        {
                            try { origSize += new FileInfo(f).Length; } catch { }
                        }
                    }
                    long compSize = File.Exists(dest) ? new FileInfo(dest).Length : 0;
                    result = new PackResult
                    {
                        OriginalSize = origSize,
                        CompressedSize = compSize,
                        Method = CompressionMethod.Store,
                    };
                }
                else
                {
                    bool isSfx = dest.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                    result = await Task.Run(() =>
                        _sfxService.BuildPackage(source, dest, splitSize, options, isSfx, progress, token), token);
                }

                string extra = "";
                if (wrapZip)
                {
                    lblStatus.Text = "Đang bọc ZIP để gửi...";
                    string zip = await Task.Run(() => _sfxService.WrapForTransport(dest));
                    extra = $"\nĐã bọc ZIP: {Path.GetFileName(zip)}";
                }

                lblStatus.Text = $"Hoàn tất! {FormatSize(result.OriginalSize)} -> {FormatSize(result.CompressedSize)} ({result.Ratio:P1})";
                progressBar.IsIndeterminate = false;
                progressBar.Value = 100;

                ResultArchivePath = dest;
                IsSuccess = true;

                ShowToast(
                    $"Nén thành công! {FormatSize(result.OriginalSize)} -> {FormatSize(result.CompressedSize)} ({result.Ratio:P1})",
                    "Hoàn tất Siêu Nén",
                    ToastType.Success);

                await Task.Delay(400);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Đã hủy theo yêu cầu.";
                try { if (File.Exists(dest)) File.Delete(dest); } catch { }
                ShowToast("Tiến trình đã được hủy.", "Đã hủy", ToastType.Warning);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Lỗi: " + ex.Message;
                ShowToast(ex.Message, "Lỗi nén dữ liệu", ToastType.Error);
            }
            finally
            {
                btnStart.Enabled = true;
                btnEstimate.Enabled = true;
                btnCancel.Text = "Đóng";
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 0) return "?";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
            return $"{size:0.##} {units[unit]}";
        }
    }
}
