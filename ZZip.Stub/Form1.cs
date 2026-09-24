using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Theme;
using ZZip.Stub.Services;
using ToastType = ZeroUI.WinForms.Overlays.ToastType;

namespace ZZip.Stub
{
    public class Form1 : BaseForm
    {
        private ButtonEdit txtTargetDir = null!;
        private SimpleButton btnBrowse = null!;
        private SimpleButton btnExtract = null!;
        private ProgressBarControl progressBar = null!;
        private Label lblStatus = null!;
        private SimpleButton btnToggleTheme = null!;

        private readonly ExtractionService _extractionService;

        public Form1()
        {
            _extractionService = new ExtractionService();
            InitializeComponent();
        }

        protected override Task RunAfterShown()
        {
            CheckEnvironment();
            return Task.CompletedTask;
        }

        private void InitializeComponent()
        {
            this.Text = "ZZip — Trình Giải Nén Tự Động (Universal Extractor)";
            TrySetWindowIcon();
            this.Size = new Size(580, 340);
            this.MinimumSize = new Size(540, 320);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;

            // Header Bar
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(15, 8, 15, 8),
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text = "⚡ ZZIP EXTRACTOR",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                Location = new Point(14, 8),
                AutoSize = true,
                ForeColor = ZeroTheme.Colors.Primary
            };
            pnlHeader.Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text = "Trích xuất dữ liệu gốc nguyên vẹn — Single-Pass Verified",
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                Location = new Point(15, 28),
                AutoSize = true,
                ForeColor = ZeroTheme.Colors.TextSecondary
            };
            pnlHeader.Controls.Add(lblSub);

            btnToggleTheme = new SimpleButton
            {
                Text = ZeroTheme.IsDark ? "☀️ Sáng" : "🌙 Tối",
                ButtonStyle = ZeroButtonStyle.Ghost,
                Size = new Size(80, 28),
                Location = new Point(470, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnToggleTheme.Click += (s, e) =>
            {
                ZeroTheme.CurrentMode = ZeroTheme.IsDark ? ZeroThemeMode.Light : ZeroThemeMode.Dark;
                btnToggleTheme.Text = ZeroTheme.IsDark ? "☀️ Sáng" : "🌙 Tối";
                lblTitle.ForeColor = ZeroTheme.Colors.Primary;
                lblSub.ForeColor = ZeroTheme.Colors.TextSecondary;
            };
            pnlHeader.Controls.Add(btnToggleTheme);

            this.Controls.Add(pnlHeader);

            // Main Card
            var card = new Card
            {
                Title = "Thư Mục Đích & Tiến Trình Giải Nén",
                Subtitle = "Dữ liệu được giải nén trực tiếp không qua tệp tạm trung gian",
                Size = new Size(540, 220),
                Location = new Point(12, 60),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblTarget = new Label
            {
                Text = "Thư mục đích:",
                Location = new Point(14, 10),
                AutoSize = true
            };
            card.ContentPanel.Controls.Add(lblTarget);

            txtTargetDir = new ButtonEdit
            {
                Location = new Point(14, 30),
                Size = new Size(390, 32),
                Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extracted"),
                PlaceholderText = "Đường dẫn thư mục giải nén..."
            };
            txtTargetDir.ButtonClick += (s, e) => BtnBrowse_Click(s, EventArgs.Empty);
            card.ContentPanel.Controls.Add(txtTargetDir);

            btnBrowse = new SimpleButton
            {
                Text = "📁 Chọn...",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(412, 30),
                Size = new Size(95, 32)
            };
            btnBrowse.Click += BtnBrowse_Click;
            card.ContentPanel.Controls.Add(btnBrowse);

            progressBar = new ProgressBarControl
            {
                Location = new Point(14, 75),
                Size = new Size(493, 20),
                ShowPercentage = true
            };
            card.ContentPanel.Controls.Add(progressBar);

            lblStatus = new Label
            {
                Text = "Sẵn sàng nhận lệnh.",
                Location = new Point(14, 102),
                Size = new Size(330, 38),
                ForeColor = ZeroTheme.Colors.Primary
            };
            card.ContentPanel.Controls.Add(lblStatus);

            btnExtract = new SimpleButton
            {
                Text = "⚡ GIẢI NÉN NGAY",
                ButtonStyle = ZeroButtonStyle.Primary,
                Location = new Point(355, 100),
                Size = new Size(152, 40)
            };
            btnExtract.Click += BtnExtract_Click;
            card.ContentPanel.Controls.Add(btnExtract);

            this.Controls.Add(card);
        }

        private void CheckEnvironment()
        {
            if (_extractionService.CheckPayload(out string msg))
            {
                lblStatus.Text = msg;
            }
            else
            {
                lblStatus.Text = msg;
                btnExtract.Enabled = false;
                ShowToast(msg, "Lỗi Nhận Diện Payload", ToastType.Warning);
            }
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "Chọn thư mục giải nén",
                SelectedPath = txtTargetDir.Text
            };
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtTargetDir.Text = fbd.SelectedPath;
            }
        }

        private async void BtnExtract_Click(object? sender, EventArgs e)
        {
            string targetPath = txtTargetDir.Text;
            if (string.IsNullOrWhiteSpace(targetPath)) return;

            // Prompt for password if encrypted
            string? password = null;
            if (_extractionService.Footer?.IsEncrypted == true)
            {
                password = PromptPassword();
                if (string.IsNullOrEmpty(password)) return;
            }

            btnExtract.Enabled = false;
            btnBrowse.Enabled = false;

            long originalSize = _extractionService.Footer?.OriginalSize ?? -1;
            if (originalSize > 0)
            {
                progressBar.IsIndeterminate = false;
                progressBar.Minimum = 0;
                progressBar.Maximum = 100;
                progressBar.Value = 0;
            }
            else
            {
                progressBar.IsIndeterminate = true;
            }
            lblStatus.Text = "Đang giải nén luồng dữ liệu. Xin vui lòng đợi...";

            var progress = new Progress<long>(done =>
            {
                if (originalSize > 0)
                {
                    int pct = (int)Math.Min(100, done * 100 / originalSize);
                    progressBar.Value = pct;
                    lblStatus.Text = $"Đang giải nén... {pct}%";
                }
            });

            try
            {
                await Task.Run(() => _extractionService.ExtractPayload(targetPath, password, progress));
                lblStatus.Text = "Giải nén hoàn tất! Dữ liệu đã sẵn sàng.";
                progressBar.IsIndeterminate = false;
                progressBar.Value = 100;

                ShowToast("Đã giải nén dữ liệu thành công!", "Hoàn tất", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast("Lỗi: " + ex.Message, "Lỗi giải nén", ToastType.Error);
                lblStatus.Text = "Tiến trình bị gián đoạn do lỗi: " + ex.Message;
            }
            finally
            {
                btnExtract.Enabled = true;
                btnBrowse.Enabled = true;
                progressBar.IsIndeterminate = false;
            }
        }

        private string? PromptPassword()
        {
            using var dlg = new Form
            {
                Text = "Nhập Mật Khẩu",
                Size = new Size(360, 160),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary
            };

            var lbl = new Label { Text = "Gói dữ liệu được mã hóa AES-256. Nhập mật khẩu:", Location = new Point(15, 15), AutoSize = true };
            var txt = new TextEdit { Location = new Point(15, 42), Size = new Size(315, 32), UseSystemPasswordChar = true, ShowPasswordEyeButton = true };
            var ok = new SimpleButton { Text = "OK", ButtonStyle = ZeroButtonStyle.Primary, DialogResult = DialogResult.OK, Location = new Point(145, 82), Size = new Size(85, 32) };
            var cancel = new SimpleButton { Text = "Hủy", ButtonStyle = ZeroButtonStyle.Secondary, DialogResult = DialogResult.Cancel, Location = new Point(245, 82), Size = new Size(85, 32) };

            dlg.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
            dlg.AcceptButton = ok;
            dlg.CancelButton = cancel;

            return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
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
    }
}
