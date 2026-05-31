using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SZip.Stub.Services;

namespace SZip.Stub
{
    public class Form1 : Form
    {
        private TextBox txtTargetDir;
        private Button btnBrowse;
        private Button btnExtract;
        private ProgressBar progressBar;
        private Label lblStatus;

        private readonly ExtractionService _extractionService;

        public Form1()
        {
            _extractionService = new ExtractionService();
            InitializeComponent();
            CheckEnvironment();
        }

        private void InitializeComponent()
        {
            this.Text = "SZip - Trình Giải Nén (Universal Extractor)";
            this.Size = new Size(500, 250);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            Label lblInfo = new Label() { Text = "Chọn thư mục giải tung dữ liệu:", Location = new Point(20, 20), AutoSize = true };
            this.Controls.Add(lblInfo);

            txtTargetDir = new TextBox() { Location = new Point(20, 45), Size = new Size(350, 25), ReadOnly = true };
            txtTargetDir.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extracted");
            this.Controls.Add(txtTargetDir);

            btnBrowse = new Button() { Text = "...", Location = new Point(380, 44), Size = new Size(40, 26) };
            btnBrowse.Click += BtnBrowse_Click;
            this.Controls.Add(btnBrowse);

            progressBar = new ProgressBar() { Location = new Point(20, 90), Size = new Size(440, 20), Style = ProgressBarStyle.Continuous };
            this.Controls.Add(progressBar);

            lblStatus = new Label() { Text = "Sẵn sàng nhận lệnh.", Location = new Point(20, 115), AutoSize = true };
            this.Controls.Add(lblStatus);

            btnExtract = new Button() { Text = "GIẢI NÉN NGAY", Location = new Point(340, 150), Size = new Size(120, 40), Font = new Font(this.Font, FontStyle.Bold) };
            btnExtract.Click += BtnExtract_Click;
            this.Controls.Add(btnExtract);
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
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK) txtTargetDir.Text = fbd.SelectedPath;
            }
        }

        // View chỉ đóng vai trò Trigger và Update UI, 100% logic nằm ở Service layer
        private async void BtnExtract_Click(object sender, EventArgs e)
        {
            string targetPath = txtTargetDir.Text;
            if (string.IsNullOrWhiteSpace(targetPath)) return;

            // Prompt for a password when the payload is encrypted.
            string? password = null;
            if (_extractionService.Footer?.IsEncrypted == true)
            {
                password = PromptPassword();
                if (string.IsNullOrEmpty(password)) return; // user cancelled
            }

            btnExtract.Enabled = false;
            btnBrowse.Enabled = false;

            long originalSize = _extractionService.Footer?.OriginalSize ?? -1;
            if (originalSize > 0)
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Minimum = 0;
                progressBar.Maximum = 100;
                progressBar.Value = 0;
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Marquee;
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
                lblStatus.Text = "Giải nén hoàn tất! Tuyệt vời.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi giải nén: " + ex.Message, "Lỗi Nghiêm Trọng", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Tiến trình bị gián đoạn do lỗi.";
            }
            finally
            {
                btnExtract.Enabled = true;
                btnBrowse.Enabled = true;
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 100;
            }
        }

        /// <summary>Simple modal password prompt. Returns null when cancelled.</summary>
        private string? PromptPassword()
        {
            using var dlg = new Form
            {
                Text = "Nhập mật khẩu",
                Size = new Size(360, 150),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
            };
            var lbl = new Label { Text = "Gói dữ liệu được mã hóa. Nhập mật khẩu:", Location = new Point(15, 15), AutoSize = true };
            var txt = new TextBox { Location = new Point(15, 45), Size = new Size(310, 25), UseSystemPasswordChar = true };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(160, 80), Size = new Size(75, 28) };
            var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Location = new Point(250, 80), Size = new Size(75, 28) };
            dlg.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
            dlg.AcceptButton = ok;
            dlg.CancelButton = cancel;
            return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
        }
    }
}
