using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroCompression.Core;
using ZeroCompression.Core.Crypto;
using ZeroCompression.Core.Packaging;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Data;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.Theme;
using ZeroZip.Core;
using ZeroZip.Main.Services;
using TabControlEx = ZeroUI.WinForms.Navigation.TabControlEx;
using TabPageEx = ZeroUI.WinForms.Navigation.TabPageEx;
using ListViewControl = ZeroUI.WinForms.Data.ListViewControl;
using ModalDialog = ZeroUI.WinForms.Overlays.ModalDialog;
using ToastType = ZeroUI.WinForms.Overlays.ToastType;

namespace ZeroZip.Main
{
    public class Form1 : BaseForm
    {
        // UI Controls - Tab 1: Studio SFX & Packer
        private ButtonEdit txtSource = null!;
        private SimpleButton btnBrowseSourceFolder = null!;
        private SimpleButton btnBrowseSourceFile = null!;
        private ButtonEdit txtDest = null!;
        private SimpleButton btnBrowseDest = null!;
        private ComboBoxEdit cmbSplitMode = null!;
        private ComboBoxEdit cmbMethod = null!;
        private ComboBoxEdit cmbProfile = null!;
        private TextEdit txtPassword = null!;
        private CheckEdit chkWrapZip = null!;
        private CheckEdit chkPrecomp = null!;
        private CheckEdit chkLong = null!;
        private SimpleButton btnEstimate = null!;
        private SimpleButton btnCompress = null!;
        private SimpleButton btnCancel = null!;
        private SimpleButton btnContextMenu = null!;
        private ProgressBarControl progressBar = null!;
        private Label lblStatus = null!;

        // UI Controls - Tab 2: Archive Inspector
        private ButtonEdit txtInspectFile = null!;
        private SimpleButton btnBrowseInspect = null!;
        private SimpleButton btnRunInspect = null!;
        private SimpleButton btnExtractFromInspect = null!;
        private ListViewControl lstInspectDetails = null!;

        // Navigation & Services
        private TabControlEx tabControl = null!;
        private SimpleButton btnToggleTheme = null!;
        private readonly SfxBuilderService _sfxService;
        private CancellationTokenSource? _cts;

        public Form1()
        {
            _sfxService = new SfxBuilderService();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "ZeroZip — Sovereign Ultra-Compression Studio (ZeroUniverse)";
            TrySetWindowIcon();
            this.Size = new Size(895, 875);
            this.MinimumSize = new Size(860, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AllowDrop = true;

            // Drag & Drop handlers on the root form
            this.DragEnter += Form1_DragEnter;
            this.DragDrop += Form1_DragDrop;

            // Top Header Bar
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(20, 8, 20, 8),
                BackColor = Color.Transparent
            };

            var lblAppTitle = new Label
            {
                Text = "⚡ ZEROZIP STUDIO",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                Location = new Point(18, 8),
                AutoSize = true,
                ForeColor = ZeroTheme.Colors.Primary
            };
            pnlHeader.Controls.Add(lblAppTitle);

            var lblAppSub = new Label
            {
                Text = "Sovereign Ultra-Compression & Archival Suite — ZeroUniverse",
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                Location = new Point(20, 32),
                AutoSize = true,
                ForeColor = ZeroTheme.Colors.TextSecondary
            };
            pnlHeader.Controls.Add(lblAppSub);

            btnToggleTheme = new SimpleButton
            {
                Text = ZeroTheme.IsDark ? "☀️ Giao diện Sáng" : "🌙 Giao diện Tối",
                ButtonStyle = ZeroButtonStyle.Ghost,
                Size = new Size(150, 32),
                Location = new Point(705, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnToggleTheme.Click += (s, e) =>
            {
                ZeroTheme.CurrentMode = ZeroTheme.IsDark ? ZeroThemeMode.Light : ZeroThemeMode.Dark;
                btnToggleTheme.Text = ZeroTheme.IsDark ? "☀️ Giao diện Sáng" : "🌙 Giao diện Tối";
                lblAppTitle.ForeColor = ZeroTheme.Colors.Primary;
                lblAppSub.ForeColor = ZeroTheme.Colors.TextSecondary;
                ShowToast(ZeroTheme.IsDark ? "Đã chuyển sang giao diện Tối (Obsidian Dark)" : "Đã chuyển sang giao diện Sáng (Clean Light)", "Chủ đề giao diện", ToastType.Info);
            };
            pnlHeader.Controls.Add(btnToggleTheme);

            // TabControl Navigation
            tabControl = new TabControlEx
            {
                Dock = DockStyle.Fill,
                TabStyle = TabStyle.Pill,
                TabHeight = 38,
                TabWidth = 260
            };

            var tabStudio = tabControl.AddTab("⚡ Siêu Nén & Đóng Gói (Studio)", "");
            var tabInspector = tabControl.AddTab("🔍 Phân Tích & Duyệt Gói (Inspector)", "");

            BuildStudioTab(tabStudio);
            BuildInspectorTab(tabInspector);

            // Add tabControl first so pnlHeader docks above it properly
            this.Controls.Add(tabControl);
            this.Controls.Add(pnlHeader);
        }

        private void BuildStudioTab(TabPageEx page)
        {
            var pnlContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(10)
            };
            page.Controls.Add(pnlContainer);

            int currentY = 8;
            const int cardWidth = 840;

            // --- Card 1: Nguồn dữ liệu & Tệp đích ---
            var cardSource = new Card
            {
                Title = "1. Nguồn Dữ Liệu & Tệp Đích",
                Subtitle = "Kéo thả thư mục hoặc tệp vào đây, hoặc nhấn nút chọn đường dẫn",
                StepNumber = 1,
                Size = new Size(cardWidth, 195),
                Location = new Point(10, currentY),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblSrc = new Label { Text = "Nguồn (Thư mục / Tệp):", Location = new Point(14, 6), AutoSize = true };
            cardSource.ContentPanel.Controls.Add(lblSrc);

            txtSource = new ButtonEdit
            {
                Location = new Point(14, 26),
                Size = new Size(540, 32),
                PlaceholderText = "Chọn hoặc kéo thả thư mục / tệp cần nén..."
            };
            txtSource.ButtonClick += (s, e) => BrowseSourceFolder();
            cardSource.ContentPanel.Controls.Add(txtSource);

            btnBrowseSourceFolder = new SimpleButton
            {
                Text = "📁 Thư mục",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(564, 26),
                Size = new Size(115, 32)
            };
            btnBrowseSourceFolder.Click += (s, e) => BrowseSourceFolder();
            cardSource.ContentPanel.Controls.Add(btnBrowseSourceFolder);

            btnBrowseSourceFile = new SimpleButton
            {
                Text = "📄 Tệp tin",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(687, 26),
                Size = new Size(115, 32)
            };
            btnBrowseSourceFile.Click += (s, e) => BrowseSourceFile();
            cardSource.ContentPanel.Controls.Add(btnBrowseSourceFile);

            var lblDst = new Label { Text = "Lưu thành (.exe SFX / .ztar):", Location = new Point(14, 66), AutoSize = true };
            cardSource.ContentPanel.Controls.Add(lblDst);

            txtDest = new ButtonEdit
            {
                Location = new Point(14, 86),
                Size = new Size(665, 32),
                PlaceholderText = "Đường dẫn tệp đầu ra (.exe SFX)..."
            };
            txtDest.ButtonClick += (s, e) => BrowseDest();
            cardSource.ContentPanel.Controls.Add(txtDest);

            btnBrowseDest = new SimpleButton
            {
                Text = "💾 Lưu...",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(687, 86),
                Size = new Size(115, 32)
            };
            btnBrowseDest.Click += (s, e) => BrowseDest();
            cardSource.ContentPanel.Controls.Add(btnBrowseDest);

            pnlContainer.Controls.Add(cardSource);
            currentY += 203;

            // --- Card 2: Thuật toán & Thông số nén ---
            var cardEngine = new Card
            {
                Title = "2. Thuật Toán & Thông Số Nén",
                Subtitle = "Lựa chọn động cơ nén tối ưu theo mục đích sử dụng",
                StepNumber = 2,
                Size = new Size(cardWidth, 170),
                Location = new Point(10, currentY),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblMethod = new Label { Text = "Thuật toán nén:", Location = new Point(14, 6), AutoSize = true };
            cardEngine.ContentPanel.Controls.Add(lblMethod);

            cmbMethod = new ComboBoxEdit
            {
                Location = new Point(14, 26),
                Size = new Size(250, 32)
            };
            cmbMethod.Items.Add("⭐ Tự động nhận diện (Adaptive Auto-Detect)");
            cmbMethod.Items.Add("Zstandard (nhanh, đa luồng, giải nén GB/s)");
            cmbMethod.Items.Add("LZMA (nén sâu nhất, chuẩn đóng gói)");
            cmbMethod.Items.Add("Brotli (nén text/web/json)");
            cmbMethod.Items.Add("Không nén (Store - đóng gói thô)");
            cmbMethod.Items.Add("ZeroTelemetry (Nghiên cứu: Gorilla XOR / Delta DoD)");
            cmbMethod.SelectedIndex = 0;
            cardEngine.ContentPanel.Controls.Add(cmbMethod);

            var lblProfile = new Label { Text = "Mức nén (Profile):", Location = new Point(280, 6), AutoSize = true };
            cardEngine.ContentPanel.Controls.Add(lblProfile);

            cmbProfile = new ComboBoxEdit
            {
                Location = new Point(280, 26),
                Size = new Size(180, 32)
            };
            cmbProfile.Items.Add("Nhanh (Fast)");
            cmbProfile.Items.Add("Cân bằng (Normal)");
            cmbProfile.Items.Add("Siêu nén (Ultra)");
            cmbProfile.SelectedIndex = 2;
            cardEngine.ContentPanel.Controls.Add(cmbProfile);

            var lblSplit = new Label { Text = "Cắt Volume (Split):", Location = new Point(480, 6), AutoSize = true };
            cardEngine.ContentPanel.Controls.Add(lblSplit);

            cmbSplitMode = new ComboBoxEdit
            {
                Location = new Point(480, 26),
                Size = new Size(322, 32)
            };
            cmbSplitMode.Items.Add("Nhúng trực tiếp (1 tệp .exe)");
            cmbSplitMode.Items.Add("Cắt mảnh 2GB (An toàn FAT32/Flash)");
            cmbSplitMode.Items.Add("Cắt mảnh 4GB (Tiêu chuẩn ISO)");
            cmbSplitMode.SelectedIndex = 0;
            cardEngine.ContentPanel.Controls.Add(cmbSplitMode);

            // Checkboxes
            chkWrapZip = new CheckEdit
            {
                Text = "Bọc ZIP bảo vệ (né bộ lọc chặn .exe)",
                Location = new Point(14, 66),
                Size = new Size(250, 24)
            };
            cardEngine.ContentPanel.Controls.Add(chkWrapZip);

            bool precompOk = _sfxService.IsPrecompAvailable();
            chkPrecomp = new CheckEdit
            {
                Text = precompOk ? "Nén sâu repack (precomp)" : "Nén repack (chưa có precomp.exe)",
                Location = new Point(280, 66),
                Size = new Size(240, 24),
                Enabled = precompOk
            };
            cardEngine.ContentPanel.Controls.Add(chkPrecomp);

            chkLong = new CheckEdit
            {
                Text = "Nén tầm xa (cửa sổ Zstd 2GB)",
                Location = new Point(530, 66),
                Size = new Size(260, 24)
            };
            cardEngine.ContentPanel.Controls.Add(chkLong);

            pnlContainer.Controls.Add(cardEngine);
            currentY += 178;

            // --- Card 3: Bảo mật mã hóa ---
            var cardSecurity = new Card
            {
                Title = "3. Bảo Mật & Xác Thực Toàn Vẹn (AES-256-GCM AEAD)",
                Subtitle = "Tùy chọn mã hóa đối xứng xác thực với cơ chế kiểm tra mật khẩu tức thì <5ms",
                StepNumber = 3,
                Size = new Size(cardWidth, 138),
                Location = new Point(10, currentY),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblPwd = new Label { Text = "Mật khẩu bảo vệ (tùy chọn):", Location = new Point(14, 6), AutoSize = true };
            cardSecurity.ContentPanel.Controls.Add(lblPwd);

            txtPassword = new TextEdit
            {
                Location = new Point(14, 26),
                Size = new Size(788, 32),
                UseSystemPasswordChar = true,
                ShowPasswordEyeButton = true,
                PlaceholderText = "Để trống nếu không mã hóa, hoặc nhập mật khẩu an toàn..."
            };
            cardSecurity.ContentPanel.Controls.Add(txtPassword);

            pnlContainer.Controls.Add(cardSecurity);
            currentY += 146;

            // --- Card 4: Tiến trình & Điều khiển ---
            var cardExec = new Card
            {
                Title = "4. Tiến Trình Thực Thi & Thao Tác",
                Subtitle = "Bắt đầu nén hoặc ước tính nhanh tỷ lệ",
                StepNumber = 4,
                Size = new Size(cardWidth, 180),
                Location = new Point(10, currentY),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            progressBar = new ProgressBarControl
            {
                Location = new Point(14, 8),
                Size = new Size(788, 22),
                ShowPercentage = true
            };
            cardExec.ContentPanel.Controls.Add(progressBar);

            lblStatus = new Label
            {
                Text = "Sẵn sàng nhận lệnh.",
                Location = new Point(14, 34),
                Size = new Size(788, 22),
                ForeColor = ZeroTheme.Colors.Primary
            };
            cardExec.ContentPanel.Controls.Add(lblStatus);

            // Action buttons
            btnCompress = new SimpleButton
            {
                Text = "⚡ Bắt đầu Siêu Nén",
                ButtonStyle = ZeroButtonStyle.Primary,
                Location = new Point(14, 62),
                Size = new Size(185, 40)
            };
            btnCompress.Click += BtnCompress_Click;
            cardExec.ContentPanel.Controls.Add(btnCompress);

            btnEstimate = new SimpleButton
            {
                Text = "🔍 Ước tính tỷ lệ",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(209, 62),
                Size = new Size(155, 40)
            };
            btnEstimate.Click += BtnEstimate_Click;
            cardExec.ContentPanel.Controls.Add(btnEstimate);

            btnCancel = new SimpleButton
            {
                Text = "✕ Hủy",
                ButtonStyle = ZeroButtonStyle.Danger,
                Location = new Point(374, 62),
                Size = new Size(90, 40),
                Enabled = false
            };
            btnCancel.Click += (s, e) =>
            {
                _cts?.Cancel();
                btnCancel.Enabled = false;
                lblStatus.Text = "Đang hủy tiến trình...";
            };
            cardExec.ContentPanel.Controls.Add(btnCancel);

            btnContextMenu = new SimpleButton
            {
                Text = ShellContextMenuService.IsRegistered() ? "✓ Đã tích hợp Explorer" : "⚙ Menu Chuột Phải Explorer",
                ButtonStyle = ZeroButtonStyle.Ghost,
                Location = new Point(474, 62),
                Size = new Size(210, 40)
            };
            btnContextMenu.Click += BtnContextMenu_Click;
            cardExec.ContentPanel.Controls.Add(btnContextMenu);

            pnlContainer.Controls.Add(cardExec);
            currentY += 188;

            // Ensure scrolling range accommodates all cards with breathing space
            pnlContainer.AutoScrollMinSize = new Size(0, currentY + 15);
        }

        private void BuildInspectorTab(TabPageEx page)
        {
            var pnlContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(10)
            };
            page.Controls.Add(pnlContainer);

            const int cardWidth = 840;

            // Card 1: Chọn tệp cần kiểm tra
            var cardInspectSource = new Card
            {
                Title = "Phân Tích Cấu Trúc Header & Metadata (ZTAR / SFX)",
                Subtitle = "Kiểm tra thông tin chi tiết payload, thuật toán, mã hóa và CRC32 mà không cần trích xuất",
                Size = new Size(cardWidth, 138),
                Location = new Point(10, 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblIns = new Label { Text = "Chọn tệp cần kiểm tra (.exe SFX / .ztar):", Location = new Point(14, 6), AutoSize = true };
            cardInspectSource.ContentPanel.Controls.Add(lblIns);

            txtInspectFile = new ButtonEdit
            {
                Location = new Point(14, 26),
                Size = new Size(480, 32),
                PlaceholderText = "Chọn tệp .exe SFX hoặc .ztar cần kiểm tra..."
            };
            txtInspectFile.ButtonClick += (s, e) => BrowseInspectFile();
            cardInspectSource.ContentPanel.Controls.Add(txtInspectFile);

            btnBrowseInspect = new SimpleButton
            {
                Text = "Chọn tệp...",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(502, 26),
                Size = new Size(95, 32)
            };
            btnBrowseInspect.Click += (s, e) => BrowseInspectFile();
            cardInspectSource.ContentPanel.Controls.Add(btnBrowseInspect);

            btnRunInspect = new SimpleButton
            {
                Text = "🔍 Phân tích",
                ButtonStyle = ZeroButtonStyle.Primary,
                Location = new Point(605, 26),
                Size = new Size(95, 32)
            };
            btnRunInspect.Click += (s, e) => RunInspect();
            cardInspectSource.ContentPanel.Controls.Add(btnRunInspect);

            btnExtractFromInspect = new SimpleButton
            {
                Text = "📦 Giải nén...",
                ButtonStyle = ZeroButtonStyle.Success,
                Location = new Point(708, 26),
                Size = new Size(95, 32),
                Enabled = false
            };
            btnExtractFromInspect.Click += BtnExtractFromInspect_Click;
            cardInspectSource.ContentPanel.Controls.Add(btnExtractFromInspect);

            pnlContainer.Controls.Add(cardInspectSource);

            // Card 2: Kết quả phân tích chi tiết
            var cardInspectResult = new Card
            {
                Title = "Thông Tin Chi Tiết Gói Nén (Archive Metadata)",
                Subtitle = "Nhật ký kiểm tra và phân tích cấu trúc nhị phân",
                Size = new Size(cardWidth, 480),
                Location = new Point(10, 154),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            lstInspectDetails = new ListViewControl
            {
                Dock = DockStyle.Fill
            };
            cardInspectResult.ContentPanel.Controls.Add(lstInspectDetails);

            pnlContainer.Controls.Add(cardInspectResult);
            pnlContainer.AutoScrollMinSize = new Size(0, 650);
        }

        #region Drag & Drop Support

        private void Form1_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void Form1_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true) return;
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            string path = files[0];
            string ext = Path.GetExtension(path).ToLowerInvariant();

            // If dropped an archive or SFX, inspect it or set as target
            if (ext == ".exe" || ext == ".ztar")
            {
                txtInspectFile.Text = path;
                tabControl.SelectedIndex = 1;
                RunInspect();
                ShowToast($"Đã nạp tệp vào Trình Phân Tích: {Path.GetFileName(path)}", "Kéo thả thành công", ToastType.Info);
                return;
            }

            // Set as compression source
            txtSource.Text = path;
            AutoSuggestDest(path);
            ShowToast($"Đã chọn nguồn nén: {Path.GetFileName(path)}", "Kéo thả thành công", ToastType.Info);
        }

        #endregion

        #region Actions & Compression Logic

        private void BrowseSourceFolder()
        {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtSource.Text = fbd.SelectedPath;
                AutoSuggestDest(fbd.SelectedPath);
            }
        }

        private void BrowseSourceFile()
        {
            using var ofd = new OpenFileDialog { Title = "Chọn tệp cần nén" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txtSource.Text = ofd.FileName;
                AutoSuggestDest(ofd.FileName);
            }
        }

        private void AutoSuggestDest(string sourcePath)
        {
            if (string.IsNullOrEmpty(txtDest.Text))
            {
                string? dir = Path.GetDirectoryName(sourcePath);
                string name = Path.GetFileName(sourcePath);
                if (string.IsNullOrEmpty(name)) name = "Archive";
                txtDest.Text = Path.Combine(dir ?? "", name + "_Repack.exe");
            }
        }

        private void BrowseDest()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Executable SFX (*.exe)|*.exe|ZTar Container (*.ztar)|*.ztar",
                Title = "Lưu file nén / SFX"
            };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                txtDest.Text = sfd.FileName;
            }
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
            var overlay = ShowLoading("Đang phân tích", "Đang phân tích dữ liệu và nhận diện loại payload...");

            try
            {
                var estTask = Task.Run(() => _sfxService.Estimate(src));
                var classifyTask = Task.Run(() => ZeroCompression.Core.Analysis.DataClassifier.ClassifyPath(src));
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
                HideLoading();
                btnEstimate.Enabled = true;
            }
        }

        private async void BtnCompress_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSource.Text) || string.IsNullOrWhiteSpace(txtDest.Text))
            {
                ShowToast("Vui lòng nhập đầy đủ nguồn và đích.", "Thiếu thông tin", ToastType.Warning);
                return;
            }

            btnCompress.Enabled = false;
            btnEstimate.Enabled = false;
            btnCancel.Enabled = true;
            _cts = new CancellationTokenSource();
            progressBar.IsIndeterminate = true;

            string source = txtSource.Text;
            string dest = txtDest.Text;
            long splitSize = 0;
            if (cmbSplitMode.SelectedIndex == 1) splitSize = 2L * 1024 * 1024 * 1024; // 2GB
            if (cmbSplitMode.SelectedIndex == 2) splitSize = 4L * 1024 * 1024 * 1024; // 4GB

            var profile = SelectedProfile();
            string? password = string.IsNullOrEmpty(txtPassword.Text) ? null : txtPassword.Text;
            bool wrapZip = chkWrapZip.Checked;
            bool usePrecomp = chkPrecomp.Checked;
            bool longMode = chkLong.Checked;

            bool isAuto = cmbMethod.SelectedIndex == 0;
            ZeroCompression.Core.Analysis.DataClassificationResult? classified = null;
            if (isAuto)
            {
                classified = ZeroCompression.Core.Analysis.DataClassifier.ClassifyPath(source);
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
                if (longMode)
                {
                    options.LongDistanceMatching = true;
                    if (options.WindowLog < CompressionOptions.MaxLongWindowLog)
                        options.WindowLog = CompressionOptions.MaxLongWindowLog;
                }

                var token = _cts.Token;
                var result = await Task.Run(() =>
                    _sfxService.BuildSfx(source, dest, splitSize, options, progress, token), token);

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

                ShowToast(
                    $"Nén thành công! {FormatSize(result.OriginalSize)} -> {FormatSize(result.CompressedSize)} ({result.Ratio:P1})",
                    "Hoàn tất Siêu Nén",
                    ToastType.Success);

                ModalDialog.Info(
                    this,
                    "Nén Thành Công",
                    $"Tác vụ hoàn tất!\n\n"
                    + (isAuto ? $"Tự động nhận diện: {classified?.DetectedType}\n" : "")
                    + $"Thuật toán: {options.Method} (Level {options.Level})\n"
                    + $"Gốc: {FormatSize(result.OriginalSize)}\nSau nén: {FormatSize(result.CompressedSize)}\nTỉ lệ: {result.Ratio:P1}"
                    + (classified != null ? $"\nChi tiết: {classified.Reason}" : "")
                    + (password != null ? "\nBảo mật: Đã mã hóa AES-256-GCM." : "") + extra);
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
                ShowToast(ex.Message, "Lỗi Nén", ToastType.Error);
            }
            finally
            {
                btnCompress.Enabled = true;
                btnEstimate.Enabled = true;
                btnCancel.Enabled = false;
                _cts?.Dispose();
                _cts = null;
                progressBar.IsIndeterminate = false;
            }
        }

        private void BtnContextMenu_Click(object? sender, EventArgs e)
        {
            if (ShellContextMenuService.IsRegistered())
            {
                var res = MessageBox.Show(
                    "ZeroZip hiện đã có trong menu chuột phải của Windows. Bạn có muốn gỡ bỏ tích hợp không?",
                    "Menu Chuột Phải",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (res == DialogResult.Yes)
                {
                    ShellContextMenuService.Unregister();
                    btnContextMenu.Text = "⚙ Menu Chuột Phải Explorer";
                    ShowToast("Đã gỡ bỏ ZeroZip khỏi menu chuột phải Windows Explorer.", "Menu ngữ cảnh", ToastType.Info);
                }
            }
            else
            {
                if (ShellContextMenuService.Register())
                {
                    btnContextMenu.Text = "✓ Đã tích hợp Explorer";
                    ShowToast("Đã đăng ký thành công ZeroZip vào menu chuột phải Windows Explorer!", "Menu ngữ cảnh", ToastType.Success);
                }
                else
                {
                    ShowToast("Không thể ghi cấu hình Registry người dùng.", "Lỗi Menu", ToastType.Error);
                }
            }
        }

        #endregion

        #region Inspector Tab Logic

        private void BrowseInspectFile()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Supported Archives (*.exe;*.ztar)|*.exe;*.ztar|All Files (*.*)|*.*",
                Title = "Chọn tệp cần kiểm tra"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txtInspectFile.Text = ofd.FileName;
                RunInspect();
            }
        }

        private void RunInspect()
        {
            string path = txtInspectFile.Text;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                ShowToast("Vui lòng chọn tệp tồn tại để kiểm tra.", "Lỗi tệp", ToastType.Warning);
                return;
            }

            lstInspectDetails.Clear();
            lstInspectDetails.AddLog(LogSeverity.Info, $"Bắt đầu phân tích tệp: {Path.GetFileName(path)}");
            lstInspectDetails.AddLog(LogSeverity.Info, $"Dung lượng trên đĩa: {FormatSize(new FileInfo(path).Length)}");

            try
            {
                using var fs = File.OpenRead(path);
                var footer = ZtarFooter.ReadFromEnd(fs);

                if (footer == null)
                {
                    lstInspectDetails.AddLog(LogSeverity.Warning, "Không tìm thấy ZTAR footer hợp lệ ở cuối tệp.");
                    lstInspectDetails.AddLog(LogSeverity.Warning, "Tệp có thể là ZIP chuẩn hoặc không phải định dạng của ZeroZip.");
                    btnExtractFromInspect.Enabled = false;
                    return;
                }

                lstInspectDetails.AddLog(LogSeverity.Success, $"✔ Nhận diện thành công định dạng ZTAR (Phiên bản: v{footer.Version})");
                lstInspectDetails.AddLog(LogSeverity.Info, $"Chế độ gói: {(footer.IsAppended ? "Tự giải nén SFX (.exe nhúng)" : "Tệp nén độc lập (.ztar)")}");
                lstInspectDetails.AddLog(LogSeverity.Info, $"Thuật toán nén: {footer.Method}");
                lstInspectDetails.AddLog(LogSeverity.Info, $"Dung lượng gốc: {FormatSize(footer.OriginalSize)}");
                lstInspectDetails.AddLog(LogSeverity.Info, $"Dung lượng nén: {FormatSize(footer.PayloadSize)}");

                if (footer.OriginalSize > 0)
                {
                    double ratio = 1.0 - (double)footer.PayloadSize / footer.OriginalSize;
                    lstInspectDetails.AddLog(LogSeverity.Success, $"Tỷ lệ tiết kiệm dung lượng: {ratio:P1}");
                }

                lstInspectDetails.AddLog(LogSeverity.Info, $"Mã kiểm tra toàn vẹn CRC32: 0x{footer.Crc32:X8}");

                if (footer.IsEncrypted)
                {
                    lstInspectDetails.AddLog(LogSeverity.Warning, "🔒 Bảo mật: Payload ĐÃ ĐƯỢC MÃ HÓA (AES-256-GCM AEAD). Cần mật khẩu để giải nén.");
                }
                else
                {
                    lstInspectDetails.AddLog(LogSeverity.Info, "🔓 Bảo mật: Không mã hóa (Public Payload).");
                }

                if (footer.IsMultiPart)
                {
                    lstInspectDetails.AddLog(LogSeverity.Info, $"Khối đa phần (Multi-part): {footer.PartCount} volumes.");
                }

                btnExtractFromInspect.Enabled = true;
                ShowToast("Đã phân tích header thành công!", "Phân tích ZTAR", ToastType.Success);
            }
            catch (Exception ex)
            {
                lstInspectDetails.AddLog(LogSeverity.Error, "Lỗi đọc tệp: " + ex.Message);
                btnExtractFromInspect.Enabled = false;
            }
        }

        private void BtnExtractFromInspect_Click(object? sender, EventArgs e)
        {
            string path = txtInspectFile.Text;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            using var fbd = new FolderBrowserDialog { Description = "Chọn thư mục giải nén" };
            if (fbd.ShowDialog() != DialogResult.OK) return;

            string targetDir = fbd.SelectedPath;

            // Run extractor in background
            Task.Run(() =>
            {
                try
                {
                    using var fs = File.OpenRead(path);
                    var footer = ZtarFooter.ReadFromEnd(fs);
                    if (footer == null) throw new InvalidOperationException("Footer không hợp lệ.");

                    string? password = null;
                    if (footer.IsEncrypted)
                    {
                        this.Invoke(new Action(() =>
                        {
                            password = PromptPassword();
                        }));
                        if (string.IsNullOrEmpty(password)) return;
                    }

                    // Seek to payload offset and extract using Single-Pass Verified engine
                    fs.Seek(footer.PayloadOffset, SeekOrigin.Begin);
                    using var subStream = new SubStream(fs, footer.PayloadOffset, footer.PayloadSize, leaveOpen: true);

                    ZtarEngine.UnpackVerified(subStream, targetDir, footer.Method, password, footer.IsPrecompressed, progress: null, footer.WindowLog, footer.Crc32);

                    this.Invoke(new Action(() =>
                    {
                        ShowToast($"Đã giải nén thành công vào: {targetDir}", "Giải nén hoàn tất", ToastType.Success);
                        lstInspectDetails.AddLog(LogSeverity.Success, $"✔ Đã giải nén hoàn tất vào thư mục: {targetDir}");
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        ShowToast("Lỗi giải nén: " + ex.Message, "Lỗi", ToastType.Error);
                        lstInspectDetails.AddLog(LogSeverity.Error, "Thất bại khi giải nén: " + ex.Message);
                    }));
                }
            });
        }

        private string? PromptPassword()
        {
            using var dlg = new Form
            {
                Text = "Nhập Mật Khẩu",
                Size = new Size(380, 160),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary
            };

            var lbl = new Label { Text = "Gói dữ liệu được mã hóa AES-256. Nhập mật khẩu:", Location = new Point(15, 15), AutoSize = true };
            var txt = new TextEdit { Location = new Point(15, 42), Size = new Size(335, 32), UseSystemPasswordChar = true, ShowPasswordEyeButton = true };
            var ok = new SimpleButton { Text = "Xác nhận", ButtonStyle = ZeroButtonStyle.Primary, DialogResult = DialogResult.OK, Location = new Point(165, 82), Size = new Size(95, 32) };
            var cancel = new SimpleButton { Text = "Hủy", ButtonStyle = ZeroButtonStyle.Secondary, DialogResult = DialogResult.Cancel, Location = new Point(270, 82), Size = new Size(80, 32) };

            dlg.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
            dlg.AcceptButton = ok;
            dlg.CancelButton = cancel;

            return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
        }

        #endregion

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
