using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroZip.Main.Services;

namespace ZeroZip.Main
{
    public class Form1 : Form
    {
        private TextBox txtSource;
        private Button btnBrowseSource;
        private TextBox txtDest;
        private Button btnBrowseDest;
        private ComboBox cmbSplitMode;
        private ComboBox cmbMethod;
        private ComboBox cmbProfile;
        private TextBox txtPassword;
        private CheckBox chkWrapZip;
        private CheckBox chkPrecomp;
        private CheckBox chkLong;
        private Button btnEstimate;
        private Button btnCompress;
        private Button btnCancel;
        private ProgressBar progressBar;
        private Label lblStatus;

        private readonly SfxBuilderService _sfxService;
        private System.Threading.CancellationTokenSource? _cts;

        public Form1()
        {
            _sfxService = new SfxBuilderService();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "ZeroZip — Ultra-Compression Studio (ZeroUniverse)";
            TrySetWindowIcon();
            this.Size = new Size(570, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            Label lbl1 = new Label() { Text = "Nguồn (Thư mục / Tệp):", Location = new Point(20, 20), AutoSize = true };
            this.Controls.Add(lbl1);
            txtSource = new TextBox() { Location = new Point(20, 45), Size = new Size(380, 25) };
            this.Controls.Add(txtSource);
            btnBrowseSource = new Button() { Text = "Chọn", Location = new Point(410, 43), Size = new Size(120, 28) };
            btnBrowseSource.Click += BtnBrowseSource_Click;
            this.Controls.Add(btnBrowseSource);

            Label lbl2 = new Label() { Text = "Lưu thành (.exe SFX):", Location = new Point(20, 80), AutoSize = true };
            this.Controls.Add(lbl2);
            txtDest = new TextBox() { Location = new Point(20, 105), Size = new Size(380, 25) };
            this.Controls.Add(txtDest);
            btnBrowseDest = new Button() { Text = "Lưu...", Location = new Point(410, 103), Size = new Size(120, 28) };
            btnBrowseDest.Click += BtnBrowseDest_Click;
            this.Controls.Add(btnBrowseDest);

            // Compression method
            Label lblMethod = new Label() { Text = "Thuật toán nén:", Location = new Point(20, 145), AutoSize = true };
            this.Controls.Add(lblMethod);
            cmbMethod = new ComboBox() { Location = new Point(130, 142), Size = new Size(180, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMethod.Items.Add("Zstandard (nhanh, đa luồng)");
            cmbMethod.Items.Add("LZMA (nén sâu nhất)");
            cmbMethod.Items.Add("Brotli (web/text)");
            cmbMethod.Items.Add("Không nén (Store)");
            cmbMethod.SelectedIndex = 0;
            this.Controls.Add(cmbMethod);

            // Profile
            Label lblProfile = new Label() { Text = "Mức nén:", Location = new Point(330, 145), AutoSize = true };
            this.Controls.Add(lblProfile);
            cmbProfile = new ComboBox() { Location = new Point(400, 142), Size = new Size(130, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbProfile.Items.Add("Nhanh");
            cmbProfile.Items.Add("Cân bằng");
            cmbProfile.Items.Add("Siêu nén (Ultra)");
            cmbProfile.SelectedIndex = 2;
            this.Controls.Add(cmbProfile);

            // Split mode
            Label lbl3 = new Label() { Text = "Cắt Volume:", Location = new Point(20, 180), AutoSize = true };
            this.Controls.Add(lbl3);
            cmbSplitMode = new ComboBox() { Location = new Point(130, 177), Size = new Size(400, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSplitMode.Items.Add("Nhúng trực tiếp (1 tệp .exe)");
            cmbSplitMode.Items.Add("Cắt mảnh 2GB (An toàn FAT32)");
            cmbSplitMode.Items.Add("Cắt mảnh 4GB (Tiêu chuẩn)");
            cmbSplitMode.SelectedIndex = 0;
            this.Controls.Add(cmbSplitMode);

            // Password
            Label lblPwd = new Label() { Text = "Mật khẩu (tùy chọn):", Location = new Point(20, 215), AutoSize = true };
            this.Controls.Add(lblPwd);
            txtPassword = new TextBox() { Location = new Point(160, 212), Size = new Size(250, 25), UseSystemPasswordChar = true };
            this.Controls.Add(txtPassword);
            CheckBox chkShowPwd = new CheckBox() { Text = "Hiện", Location = new Point(420, 214), AutoSize = true };
            chkShowPwd.CheckedChanged += (s, e) => txtPassword.UseSystemPasswordChar = !chkShowPwd.Checked;
            this.Controls.Add(chkShowPwd);

            // Wrap zip
            chkWrapZip = new CheckBox() { Text = "Bọc ZIP để gửi (né bộ lọc chặn .exe)", Location = new Point(20, 245), AutoSize = true };
            this.Controls.Add(chkWrapZip);

            // Deep compression (precomp) - only enabled when the external tool is present.
            bool precompOk = _sfxService.IsPrecompAvailable();
            chkPrecomp = new CheckBox()
            {
                Text = precompOk ? "Nén sâu kiểu repack (precomp)" : "Nén sâu (precomp) - chưa có precomp.exe",
                Location = new Point(300, 245),
                AutoSize = true,
                Enabled = precompOk,
            };
            this.Controls.Add(chkPrecomp);

            // Long-distance mode (zstd large window).
            chkLong = new CheckBox() { Text = "Nén tầm xa (cửa sổ 2GB, file rất lớn)", Location = new Point(300, 270), AutoSize = true };
            this.Controls.Add(chkLong);

            // Estimate button
            btnEstimate = new Button() { Text = "Kiểm tra nhanh tỉ lệ nén", Location = new Point(20, 275), Size = new Size(250, 30) };
            btnEstimate.Click += BtnEstimate_Click;
            this.Controls.Add(btnEstimate);

            var btnContextMenu = new Button()
            {
                Text = ShellContextMenuService.IsRegistered() ? "✓ Đã tích hợp Menu Chuột Phải" : "⚙ Tích hợp Menu Chuột Phải",
                Location = new Point(280, 275),
                Size = new Size(250, 30)
            };
            btnContextMenu.Click += (s, e) =>
            {
                if (ShellContextMenuService.IsRegistered())
                {
                    var res = MessageBox.Show("ZeroZip hiện đã có trong menu chuột phải của Windows. Bạn có muốn gỡ bỏ tích hợp không?", "Menu Chuột Phải", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (res == DialogResult.Yes)
                    {
                        ShellContextMenuService.Unregister();
                        btnContextMenu.Text = "⚙ Tích hợp Menu Chuột Phải";
                        MessageBox.Show("Đã gỡ bỏ ZeroZip khỏi menu chuột phải Windows Explorer.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    if (ShellContextMenuService.Register())
                    {
                        btnContextMenu.Text = "✓ Đã tích hợp Menu Chuột Phải";
                        MessageBox.Show("Đã đăng ký thành công ZeroZip vào menu chuột phải Windows Explorer (Files, Folders & .ztar).", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Không thể ghi Registry người dùng.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };
            this.Controls.Add(btnContextMenu);

            progressBar = new ProgressBar() { Location = new Point(20, 320), Size = new Size(510, 20), Style = ProgressBarStyle.Continuous };
            this.Controls.Add(progressBar);

            lblStatus = new Label() { Text = "Sẵn sàng.", Location = new Point(20, 350), Size = new Size(510, 40) };
            this.Controls.Add(lblStatus);

            btnCompress = new Button() { Text = "Bắt đầu Siêu Nén", Location = new Point(330, 395), Size = new Size(200, 45), Font = new Font(this.Font, FontStyle.Bold) };
            btnCompress.Click += BtnCompress_Click;
            this.Controls.Add(btnCompress);

            btnCancel = new Button() { Text = "Hủy", Location = new Point(230, 395), Size = new Size(90, 45), Enabled = false };
            btnCancel.Click += (s, e) => { _cts?.Cancel(); btnCancel.Enabled = false; lblStatus.Text = "Đang hủy..."; };
            this.Controls.Add(btnCancel);

            ApplyZeroUiDarkTheme();
        }

        private void ApplyZeroUiDarkTheme()
        {
            var bgPrimary = Color.FromArgb(17, 19, 31);
            var bgInput = Color.FromArgb(29, 32, 52);
            var bgHover = Color.FromArgb(38, 42, 68);
            var borderSubtle = Color.FromArgb(46, 52, 78);
            var textPrimary = Color.FromArgb(241, 245, 249);
            var textSecondary = Color.FromArgb(148, 163, 184);
            var accent = Color.FromArgb(99, 102, 241);
            var accentHover = Color.FromArgb(129, 140, 248);

            this.BackColor = bgPrimary;
            this.ForeColor = textPrimary;
            this.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is Label lbl)
                {
                    lbl.ForeColor = (lbl == lblStatus) ? accentHover : textSecondary;
                }
                else if (ctrl is TextBox txt)
                {
                    txt.BackColor = bgInput;
                    txt.ForeColor = textPrimary;
                    txt.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (ctrl is ComboBox cmb)
                {
                    cmb.BackColor = bgInput;
                    cmb.ForeColor = textPrimary;
                    cmb.FlatStyle = FlatStyle.Flat;
                }
                else if (ctrl is CheckBox chk)
                {
                    chk.ForeColor = textPrimary;
                }
                else if (ctrl is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 1;
                    if (btn == btnCompress)
                    {
                        btn.BackColor = accent;
                        btn.ForeColor = Color.White;
                        btn.FlatAppearance.BorderColor = accent;
                    }
                    else
                    {
                        btn.BackColor = bgHover;
                        btn.ForeColor = textPrimary;
                        btn.FlatAppearance.BorderColor = borderSubtle;
                    }
                }
            }
        }

        private void BtnBrowseSource_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtSource.Text = fbd.SelectedPath;
                    if (string.IsNullOrEmpty(txtDest.Text))
                    {
                        txtDest.Text = Path.Combine(Path.GetDirectoryName(fbd.SelectedPath), Path.GetFileName(fbd.SelectedPath) + "_Repack.exe");
                    }
                }
            }
        }

        private void BtnBrowseDest_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog() { Filter = "Executable Files (*.exe)|*.exe", Title = "Lưu file SFX" })
            {
                if (sfd.ShowDialog() == DialogResult.OK) txtDest.Text = sfd.FileName;
            }
        }

        private ZeroZip.Core.CompressionMethod SelectedMethod() => cmbMethod.SelectedIndex switch
        {
            1 => ZeroZip.Core.CompressionMethod.Lzma,
            2 => ZeroZip.Core.CompressionMethod.Brotli,
            3 => ZeroZip.Core.CompressionMethod.Store,
            _ => ZeroZip.Core.CompressionMethod.Zstd,
        };

        private ZeroZip.Core.CompressionProfile SelectedProfile() => cmbProfile.SelectedIndex switch
        {
            0 => ZeroZip.Core.CompressionProfile.Fast,
            1 => ZeroZip.Core.CompressionProfile.Normal,
            _ => ZeroZip.Core.CompressionProfile.Ultra,
        };

        private async void BtnEstimate_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSource.Text) || !(Directory.Exists(txtSource.Text) || File.Exists(txtSource.Text)))
            {
                MessageBox.Show("Vui lòng chọn nguồn hợp lệ trước.");
                return;
            }
            btnEstimate.Enabled = false;
            string src = txtSource.Text;
            lblStatus.Text = "Đang lấy mẫu và ước tính...";
            try
            {
                var est = await Task.Run(() => _sfxService.Estimate(src));
                lblStatus.Text = est.Summary();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Lỗi ước tính: " + ex.Message;
            }
            finally
            {
                btnEstimate.Enabled = true;
            }
        }

        private async void BtnCompress_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSource.Text) || string.IsNullOrWhiteSpace(txtDest.Text))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ nguồn và đích.");
                return;
            }

            btnCompress.Enabled = false;
            btnEstimate.Enabled = false;
            btnCancel.Enabled = true;
            _cts = new System.Threading.CancellationTokenSource();
            progressBar.Style = ProgressBarStyle.Marquee;

            string source = txtSource.Text;
            string dest = txtDest.Text;
            long splitSize = 0;
            if (cmbSplitMode.SelectedIndex == 1) splitSize = 2L * 1024 * 1024 * 1024; // 2GB
            if (cmbSplitMode.SelectedIndex == 2) splitSize = 4L * 1024 * 1024 * 1024; // 4GB

            var method = SelectedMethod();
            var profile = SelectedProfile();
            string? password = string.IsNullOrEmpty(txtPassword.Text) ? null : txtPassword.Text;
            bool wrapZip = chkWrapZip.Checked;
            bool usePrecomp = chkPrecomp.Checked;
            bool longMode = chkLong.Checked;
            lblStatus.Text = $"Đang nén [{method} / {profile}{(usePrecomp ? " / precomp" : "")}]. Vui lòng đợi...";

            var progress = new Progress<long>(done =>
            {
                lblStatus.Text = $"Đang nén... đã xử lý {FormatSize(done)}";
            });

            try
            {
                var options = ZeroZip.Core.CompressionOptions.FromProfile(profile, method);
                options.Password = password;
                options.UsePrecomp = usePrecomp;
                if (longMode)
                {
                    options.LongDistanceMatching = true;
                    if (options.WindowLog < ZeroZip.Core.CompressionOptions.MaxLongWindowLog)
                        options.WindowLog = ZeroZip.Core.CompressionOptions.MaxLongWindowLog;
                }

                // UI gọi Service và không chứa Logic TarFile hay Process Start
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
                MessageBox.Show(
                    $"Nén thành công!\n\nThuật toán: {method}\nGốc: {FormatSize(result.OriginalSize)}\nSau nén: {FormatSize(result.CompressedSize)}\nTỉ lệ: {result.Ratio:P1}"
                    + (password != null ? "\nĐã mã hóa AES-256." : "") + extra,
                    "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Đã hủy theo yêu cầu.";
                // Clean up the partially written output so no corrupt SFX is left behind.
                try { if (File.Exists(dest)) File.Delete(dest); } catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
                lblStatus.Text = "Trạng thái: Xảy ra lỗi hỏng tiến trình.";
            }
            finally
            {
                btnCompress.Enabled = true;
                btnEstimate.Enabled = true;
                btnCancel.Enabled = false;
                _cts?.Dispose();
                _cts = null;
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 100;
            }
        }

        private void TrySetWindowIcon()
        {
            // The icon is embedded in the exe via <ApplicationIcon>; extract it for the titlebar.
            try
            {
                string exe = Environment.ProcessPath ?? "";
                if (File.Exists(exe))
                {
                    var ico = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                    if (ico != null) this.Icon = ico;
                }
            }
            catch { /* icon is cosmetic; ignore failures */ }
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
