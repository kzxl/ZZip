using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroCompression.Core;
using ZeroCompression.Core.Analysis;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Theme;
using TabControlEx = ZeroUI.WinForms.Navigation.TabControlEx;
using TabPageEx = ZeroUI.WinForms.Navigation.TabPageEx;
using TabStyle = ZeroUI.WinForms.Navigation.TabStyle;
using ZZip.Core;
using ZZip.Localization;
using ZZip.Main.Services;

namespace ZZip.Main.Dialogs
{
    /// <summary>
    /// Optimized "Add to Archive" configuration dialog (WinRAR & 7-Zip standard).
    /// Features a compact tabbed design (General, Advanced, Security) with zero scrolling
    /// and delegates active compression tracking to OperationProgressDialog.
    /// </summary>
    public class AddArchiveDialog : BaseForm
    {
        private readonly string? _initialSource;
        private readonly SfxBuilderService _sfxService;

        // UI Controls - Tabs
        private TabControlEx tabs = null!;
        private TabPageEx pageGeneral = null!;
        private TabPageEx pageAdvanced = null!;
        private TabPageEx pageSecurity = null!;

        // Tab 1: General
        private Label lblSrc = null!;
        private ButtonEdit txtSource = null!;
        private SimpleButton btnBrowseFolder = null!;
        private SimpleButton btnBrowseFile = null!;
        private Label lblDst = null!;
        private ButtonEdit txtDest = null!;
        private SimpleButton btnBrowseDest = null!;
        private Label lblMethod = null!;
        private ComboBoxEdit cmbMethod = null!;
        private Label lblProfile = null!;
        private ComboBoxEdit cmbProfile = null!;
        private Label lblSplit = null!;
        private ComboBoxEdit cmbSplit = null!;
        private Panel pnlBadge = null!;
        private Label lblAutoDetectBadge = null!;

        // Tab 2: Advanced
        private CheckEdit chkWrapZip = null!;
        private CheckEdit chkPrecomp = null!;
        private CheckEdit chkLong = null!;
        private Panel pnlEst = null!;
        private SimpleButton btnEstimateTab = null!;
        private Label lblEstInfo = null!;

        // Tab 3: Security
        private Label lblPass = null!;
        private TextEdit txtPassword = null!;
        private Panel pnlSecInfo = null!;
        private Label lblSecTitle = null!;
        private Label lblSecBody = null!;

        // Bottom Bar
        private Panel pnlBottomBar = null!;
        private Label lblEngineBrand = null!;
        private SimpleButton btnEstimate = null!;
        private SimpleButton btnStart = null!;
        private SimpleButton btnCancel = null!;

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
            this.Size = new Size(740, 430);
            this.MinimumSize = new Size(700, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;

            // --- 1. Pinned Bottom Action Bar ---
            pnlBottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = ZeroTheme.Colors.Surface,
                Padding = new Padding(16, 8, 16, 8)
            };
            pnlBottomBar.Paint += (s, e) =>
            {
                using var pen = new Pen(ZeroTheme.Colors.Border, 1);
                e.Graphics.DrawLine(pen, 0, 0, pnlBottomBar.Width, 0);
            };

            lblEngineBrand = new Label
            {
                Dock = DockStyle.Left,
                Text = "⚡ Powered by ZeroUniverse Sovereign Codecs",
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            };
            pnlBottomBar.Controls.Add(lblEngineBrand);

            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Padding = new Padding(0, 2, 0, 2)
            };

            btnEstimate = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnEstimate"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(130, 34),
                Margin = new Padding(0, 0, 8, 0)
            };
            btnEstimate.Click += BtnEstimate_Click;
            pnlButtons.Controls.Add(btnEstimate);

            btnStart = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnCompress"),
                ButtonStyle = ZeroButtonStyle.Primary,
                Size = new Size(140, 34),
                Margin = new Padding(0, 0, 8, 0)
            };
            btnStart.Click += BtnStart_Click;
            pnlButtons.Controls.Add(btnStart);

            btnCancel = new SimpleButton
            {
                Text = LocalizationService.Get("Btn_Cancel"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(88, 34),
                Margin = new Padding(0)
            };
            btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            pnlButtons.Controls.Add(btnCancel);

            pnlBottomBar.Controls.Add(pnlButtons);

            // --- 2. Tab Control ---
            tabs = new TabControlEx
            {
                Dock = DockStyle.Fill,
                TabStyle = TabStyle.Underline,
                TabHeight = 38,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
            };

            // ==================== TAB 1: CHUNG (GENERAL) ====================
            pageGeneral = new TabPageEx
            {
                Title = LocalizationService.Get("Tab_General", "Chung (General)"),
                Padding = new Padding(16, 12, 16, 12),
                BackColor = ZeroTheme.Colors.Surface
            };

            lblSrc = new Label
            {
                Text = LocalizationService.Get("Studio_LblSource", "Tập tin / Thư mục cần nén:"),
                Location = new Point(14, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            pageGeneral.Controls.Add(lblSrc);

            txtSource = new ButtonEdit
            {
                Location = new Point(14, 32),
                Size = new Size(465, 32),
                PlaceholderText = "Chọn hoặc kéo thả thư mục / tệp cần nén..."
            };
            txtSource.ButtonClick += (s, e) => BrowseSourceFolder();
            txtSource.TextChanged += (s, e) => AutoDetectAndTune(txtSource.Text);
            pageGeneral.Controls.Add(txtSource);

            btnBrowseFolder = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnBrowseFolder", "Thư mục..."),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(487, 32),
                Size = new Size(100, 32)
            };
            btnBrowseFolder.Click += (s, e) => BrowseSourceFolder();
            pageGeneral.Controls.Add(btnBrowseFolder);

            btnBrowseFile = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnBrowseFile", "Tập tin..."),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(593, 32),
                Size = new Size(95, 32)
            };
            btnBrowseFile.Click += (s, e) => BrowseSourceFile();
            pageGeneral.Controls.Add(btnBrowseFile);

            lblDst = new Label
            {
                Text = LocalizationService.Get("Studio_LblDestSave", "Tên tập tin đích (.zz, .zip, .exe):"),
                Location = new Point(14, 72),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            pageGeneral.Controls.Add(lblDst);

            txtDest = new ButtonEdit
            {
                Location = new Point(14, 94),
                Size = new Size(573, 32),
                PlaceholderText = "Đường dẫn kho lưu trữ đích..."
            };
            txtDest.ButtonClick += (s, e) => BrowseDest();
            pageGeneral.Controls.Add(txtDest);

            btnBrowseDest = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnBrowseDest", "Duyệt..."),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(593, 94),
                Size = new Size(95, 32)
            };
            btnBrowseDest.Click += (s, e) => BrowseDest();
            pageGeneral.Controls.Add(btnBrowseDest);

            lblMethod = new Label
            {
                Text = LocalizationService.Get("Studio_LblMethod", "Thuật toán chính:"),
                Location = new Point(14, 134),
                AutoSize = true
            };
            pageGeneral.Controls.Add(lblMethod);

            cmbMethod = new ComboBoxEdit
            {
                Location = new Point(14, 154),
                Size = new Size(220, 32)
            };
            cmbMethod.Items.Add("⭐ Tự động (Adaptive)");
            cmbMethod.Items.Add("Zstandard (Nhanh, đa luồng)");
            cmbMethod.Items.Add("LZMA (Nén sâu nhất)");
            cmbMethod.Items.Add("Brotli (Web / Mã nguồn)");
            cmbMethod.Items.Add("Không nén (Store)");
            cmbMethod.Items.Add("ZeroTelemetry (Gorilla XOR)");
            cmbMethod.SelectedIndex = 0;
            pageGeneral.Controls.Add(cmbMethod);

            lblProfile = new Label
            {
                Text = LocalizationService.Get("Studio_LblProfile", "Mức độ nén:"),
                Location = new Point(244, 134),
                AutoSize = true
            };
            pageGeneral.Controls.Add(lblProfile);

            cmbProfile = new ComboBoxEdit
            {
                Location = new Point(244, 154),
                Size = new Size(195, 32)
            };
            cmbProfile.Items.Add("Nhanh (Fast)");
            cmbProfile.Items.Add("Cân bằng (Normal)");
            cmbProfile.Items.Add("Siêu nén (Ultra)");
            cmbProfile.SelectedIndex = 2;
            pageGeneral.Controls.Add(cmbProfile);

            lblSplit = new Label
            {
                Text = LocalizationService.Get("Studio_LblSplit", "Chia nhỏ khối lượng:"),
                Location = new Point(450, 134),
                AutoSize = true
            };
            pageGeneral.Controls.Add(lblSplit);

            cmbSplit = new ComboBoxEdit
            {
                Location = new Point(450, 154),
                Size = new Size(238, 32)
            };
            cmbSplit.Items.Add("Không chia (1 tệp duy nhất)");
            cmbSplit.Items.Add("2GB (Giới hạn FAT32)");
            cmbSplit.Items.Add("4GB (Tiêu chuẩn ISO)");
            cmbSplit.SelectedIndex = 0;
            pageGeneral.Controls.Add(cmbSplit);

            pnlBadge = new Panel
            {
                Location = new Point(14, 202),
                Size = new Size(674, 46),
                BackColor = ZeroTheme.IsDark ? Color.FromArgb(30, 38, 54) : Color.FromArgb(242, 246, 255),
                Padding = new Padding(12, 6, 12, 6)
            };

            lblAutoDetectBadge = new Label
            {
                Dock = DockStyle.Fill,
                Text = "💡 Nhận diện dữ liệu: Đang sẵn sàng...",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = ZeroTheme.Colors.Primary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            pnlBadge.Controls.Add(lblAutoDetectBadge);
            pageGeneral.Controls.Add(pnlBadge);

            // ==================== TAB 2: NÂNG CAO (ADVANCED) ====================
            pageAdvanced = new TabPageEx
            {
                Title = LocalizationService.Get("Tab_Advanced", "Nâng cao (Advanced)"),
                Padding = new Padding(16, 16, 16, 16),
                BackColor = ZeroTheme.Colors.Surface
            };

            chkWrapZip = new CheckEdit
            {
                Text = "Bọc ZIP bảo vệ (gửi qua email hoặc tải lên web an toàn, không bị tường lửa chặn)",
                Location = new Point(16, 14),
                Size = new Size(650, 24)
            };
            pageAdvanced.Controls.Add(chkWrapZip);

            bool precompOk = _sfxService.IsPrecompAvailable();
            chkPrecomp = new CheckEdit
            {
                Text = precompOk ? "Nén sâu repack (precomp cho ảnh JPEG, luồng deflate và tài nguyên game)" : "Nén repack (chưa phát hiện precomp.exe)",
                Location = new Point(16, 44),
                Size = new Size(650, 24),
                Enabled = precompOk
            };
            pageAdvanced.Controls.Add(chkPrecomp);

            chkLong = new CheckEdit
            {
                Text = "Khử trùng lặp khoảng cách xa (Zstandard Long Distance Matching cửa sổ 2GB)",
                Location = new Point(16, 74),
                Size = new Size(650, 24)
            };
            pageAdvanced.Controls.Add(chkLong);

            pnlEst = new Panel
            {
                Location = new Point(16, 114),
                Size = new Size(672, 120),
                BackColor = ZeroTheme.Colors.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(14)
            };

            btnEstimateTab = new SimpleButton
            {
                Text = "Ước tính tỉ lệ nén",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(14, 12),
                Size = new Size(150, 32)
            };
            btnEstimateTab.Click += BtnEstimate_Click;
            pnlEst.Controls.Add(btnEstimateTab);

            lblEstInfo = new Label
            {
                Text = "ZZip sẽ lấy mẫu ngẫu nhiên 64MB đầu tiên của tập dữ liệu để ước tính dung lượng và thời gian thực thi mà không làm thay đổi tệp gốc.",
                Location = new Point(14, 52),
                Size = new Size(640, 56),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };
            pnlEst.Controls.Add(lblEstInfo);
            pageAdvanced.Controls.Add(pnlEst);

            // ==================== TAB 3: BẢO MẬT (SECURITY) ====================
            pageSecurity = new TabPageEx
            {
                Title = LocalizationService.Get("Tab_Security", "Bảo mật (Security)"),
                Padding = new Padding(16, 16, 16, 16),
                BackColor = ZeroTheme.Colors.Surface
            };

            lblPass = new Label
            {
                Text = LocalizationService.Get("Studio_LblPassword", "Mật khẩu bảo vệ kho lưu trữ (tùy chọn):"),
                Location = new Point(16, 14),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            pageSecurity.Controls.Add(lblPass);

            txtPassword = new TextEdit
            {
                Location = new Point(16, 36),
                Size = new Size(480, 32),
                PlaceholderText = "Nhập mật khẩu mã hóa...",
                UseSystemPasswordChar = true,
                ShowPasswordEyeButton = true
            };
            pageSecurity.Controls.Add(txtPassword);

            pnlSecInfo = new Panel
            {
                Location = new Point(16, 82),
                Size = new Size(672, 135),
                BackColor = ZeroTheme.Colors.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(16)
            };

            lblSecTitle = new Label
            {
                Text = "🛡️ Chuẩn Mã Hóa Quân Sự AES-256-GCM AEAD",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.Primary,
                Location = new Point(14, 12),
                AutoSize = true
            };
            pnlSecInfo.Controls.Add(lblSecTitle);

            lblSecBody = new Label
            {
                Text = "Toàn bộ khối dữ liệu nén được bảo vệ bằng chuẩn mã hóa có xác thực AES-256-GCM.\n" +
                       "• Thuật toán xác thực GMAC 128-bit phát hiện tức thì nếu file bị hỏng hoặc cố ý chỉnh sửa.\n" +
                       "• Bảo mật tuyệt đối trước các cuộc tấn công brute-force và padding-oracle.\n" +
                       "• Bỏ trống mật khẩu nếu muốn tạo kho lưu trữ tiêu chuẩn truy cập tự do.",
                Location = new Point(14, 40),
                Size = new Size(640, 80),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular)
            };
            pnlSecInfo.Controls.Add(lblSecBody);
            pageSecurity.Controls.Add(pnlSecInfo);

            // Mount Tabs into TabControlEx
            tabs.AddTab(pageGeneral);
            tabs.AddTab(pageAdvanced);
            tabs.AddTab(pageSecurity);

            // Correct docking order: Bottom docked panel added first, then Fill docked tab control
            this.Controls.Add(tabs);
            this.Controls.Add(pnlBottomBar);

            // Theme support
            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (this.IsDisposed) return;
            pnlBottomBar.BackColor = ZeroTheme.Colors.Surface;
            pnlBottomBar.Invalidate();
            pageGeneral.BackColor = ZeroTheme.Colors.Surface;
            pageAdvanced.BackColor = ZeroTheme.Colors.Surface;
            pageSecurity.BackColor = ZeroTheme.Colors.Surface;
            pnlBadge.BackColor = ZeroTheme.IsDark ? Color.FromArgb(30, 38, 54) : Color.FromArgb(242, 246, 255);
            pnlEst.BackColor = ZeroTheme.Colors.Surface;
            pnlSecInfo.BackColor = ZeroTheme.Colors.Surface;
        }

        private void ApplyLocalization()
        {
            this.Text = LocalizationService.Get("Dialog_AddArchive_Title");
            if (pageGeneral != null) pageGeneral.Title = LocalizationService.Get("Tab_General", "Chung (General)");
            if (pageAdvanced != null) pageAdvanced.Title = LocalizationService.Get("Tab_Advanced", "Nâng cao (Advanced)");
            if (pageSecurity != null) pageSecurity.Title = LocalizationService.Get("Tab_Security", "Bảo mật (Security)");

            if (lblSrc != null) lblSrc.Text = LocalizationService.Get("Studio_LblSource", "Tập tin hoặc thư mục nguồn:");
            if (btnBrowseFolder != null) btnBrowseFolder.Text = LocalizationService.Get("Studio_BtnBrowseFolder", "Thư mục...");
            if (btnBrowseFile != null) btnBrowseFile.Text = LocalizationService.Get("Studio_BtnBrowseFile", "Tập tin...");
            if (lblDst != null) lblDst.Text = LocalizationService.Get("Studio_LblDestSave", "Tên tập tin đích (.zz, .zip, .exe):");
            if (btnBrowseDest != null) btnBrowseDest.Text = LocalizationService.Get("Studio_BtnBrowseDest", "Duyệt...");

            if (lblMethod != null) lblMethod.Text = LocalizationService.Get("Studio_LblMethod", "Thuật toán chính:");
            if (lblProfile != null) lblProfile.Text = LocalizationService.Get("Studio_LblProfile", "Mức độ nén:");
            if (lblSplit != null) lblSplit.Text = LocalizationService.Get("Studio_LblSplit", "Chia nhỏ khối lượng:");

            if (lblPass != null) lblPass.Text = LocalizationService.Get("Studio_LblPassword", "Mật khẩu bảo vệ kho lưu trữ (tùy chọn):");

            if (btnStart != null) btnStart.Text = LocalizationService.Get("Studio_BtnCompress");
            if (btnEstimate != null) btnEstimate.Text = LocalizationService.Get("Studio_BtnEstimate");
            if (btnCancel != null) btnCancel.Text = LocalizationService.Get("Btn_Cancel");
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
                Filter = "Kho lưu trữ ZZip (*.zz)|*.zz|Executable SFX (*.exe)|*.exe|Tệp nén ZIP (*.zip)|*.zip|Kho lưu trữ ZTar (*.ztar)|*.ztar",
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
            btnEstimateTab.Enabled = false;
            string src = txtSource.Text;
            lblEstInfo.Text = "Đang lấy mẫu và phân tích tỉ lệ nén dự kiến...";

            try
            {
                var estTask = Task.Run(() => _sfxService.Estimate(src));
                var classifyTask = Task.Run(() => DataClassifier.ClassifyPath(src));
                await Task.WhenAll(estTask, classifyTask);

                var est = estTask.Result;
                var cls = classifyTask.Result;

                lblEstInfo.Text = $"✅ [{cls.DetectedType}] -> Khuyến nghị: {cls.RecommendedMethod}\n{est.Summary()}";
                lblEstInfo.ForeColor = ZeroTheme.Colors.Primary;
                ShowToast($"Nhận diện: {cls.DetectedType} -> Khuyến nghị: {cls.RecommendedMethod}\n{est.Summary()}", "Nhận diện & Ước tính", ToastType.Info);
            }
            catch (Exception ex)
            {
                lblEstInfo.Text = "Lỗi phân tích: " + ex.Message;
                lblEstInfo.ForeColor = ZeroTheme.Colors.Danger;
                ShowToast(ex.Message, "Lỗi phân tích", ToastType.Error);
            }
            finally
            {
                btnEstimate.Enabled = true;
                btnEstimateTab.Enabled = true;
            }
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSource.Text) || string.IsNullOrWhiteSpace(txtDest.Text))
            {
                ShowToast("Vui lòng nhập đầy đủ nguồn và đích.", "Thiếu thông tin", ToastType.Warning);
                return;
            }

            if (!Directory.Exists(txtSource.Text) && !File.Exists(txtSource.Text))
            {
                ShowToast("Tệp hoặc thư mục nguồn không tồn tại.", "Nguồn không hợp lệ", ToastType.Error);
                return;
            }

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
            CompressionOptions options;
            if (isAuto)
            {
                var classified = DataClassifier.ClassifyPath(source);
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

            // Hide configuration dialog and delegate execution to modern OperationProgressDialog
            this.Hide();

            try
            {
                using var progressDialog = new OperationProgressDialog(
                    OperationType.Compress,
                    source,
                    dest,
                    options,
                    password,
                    totalExpectedBytes: 0,
                    splitSize: splitSize,
                    wrapZip: wrapZip);

                var dialogResult = progressDialog.ShowDialog(this.Owner ?? this);

                if (dialogResult == DialogResult.OK && progressDialog.IsCompleted)
                {
                    ResultArchivePath = dest;
                    IsSuccess = true;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    // User cancelled or operation failed: re-display configuration dialog
                    this.Show();
                    if (progressDialog.FailureError != null)
                    {
                        ShowToast("Thao tác nén bị gián đoạn.", "Chưa hoàn tất", ToastType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Show();
                ShowToast("Lỗi khởi tạo nén: " + ex.Message, "Lỗi", ToastType.Error);
            }
        }
    }
}
