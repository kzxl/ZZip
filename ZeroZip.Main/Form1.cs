using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroCompression.Core;
using ZeroCompression.Core.Analysis;
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
using ModalDialog = ZeroUI.WinForms.Overlays.ModalDialog;
using ToastType = ZeroUI.WinForms.Overlays.ToastType;

namespace ZeroZip.Main
{
    public class Form1 : BaseForm
    {
        // Initial launch parameters
        private readonly string? _initialArchivePath;
        private readonly string? _initialSourcePath;
        private readonly bool _openStudio;

        // UI Header & Toolbar
        private Label lblAppTitle = null!;
        private Label lblAppSub = null!;
        private SimpleButton btnToggleTheme = null!;
        private Panel pnlWinRarToolbar = null!;
        private SimpleButton tbBtnAdd = null!;
        private SimpleButton tbBtnExtract = null!;
        private SimpleButton tbBtnTest = null!;
        private SimpleButton tbBtnView = null!;
        private SimpleButton tbBtnInfo = null!;
        private SimpleButton tbBtnContextMenu = null!;

        // Navigation Tabs
        private TabControlEx tabControl = null!;

        // --- Tab 1: WinRAR Archive Explorer ---
        private readonly ArchiveExplorerService _archiveExplorer;
        private ButtonEdit txtCurrentPath = null!;
        private SimpleButton btnUpLevel = null!;
        private SimpleButton btnOpenArchive = null!;
        private ListView lvArchiveFiles = null!;
        private ImageList imgListFiles = null!;
        private ContextMenuStrip contextMenuFiles = null!;
        private StatusStrip statusStripArchive = null!;
        private ToolStripStatusLabel lblStatusItems = null!;
        private ToolStripStatusLabel lblStatusSelection = null!;
        private ToolStripStatusLabel lblStatusFormat = null!;

        // --- Tab 2: Studio SFX & Packer ---
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
        private ProgressBarControl progressBar = null!;
        private Label lblStatus = null!;
        private Label lblAutoDetectBadge = null!;

        // Services
        private readonly SfxBuilderService _sfxService;
        private CancellationTokenSource? _cts;

        public Form1(string? initialArchivePath = null, string? initialSourcePath = null, bool openStudio = false)
        {
            _initialArchivePath = initialArchivePath;
            _initialSourcePath = initialSourcePath;
            _openStudio = openStudio;

            _sfxService = new SfxBuilderService();
            _archiveExplorer = new ArchiveExplorerService();

            InitializeComponent();
        }

        protected override async Task RunAfterShown()
        {
            if (!string.IsNullOrEmpty(_initialSourcePath))
            {
                txtSource.Text = _initialSourcePath;
                AutoSuggestDest(_initialSourcePath);
                AutoDetectAndTune(_initialSourcePath);
                if (_openStudio)
                {
                    tabControl.SelectedIndex = 1;
                }
            }

            if (!string.IsNullOrEmpty(_initialArchivePath) && File.Exists(_initialArchivePath))
            {
                await LoadArchiveAsync(_initialArchivePath);
                tabControl.SelectedIndex = 0;
            }
        }

        private void InitializeComponent()
        {
            this.Text = "ZeroZip — Sovereign Ultra-Compression Studio & Archive Explorer";
            TrySetWindowIcon();
            this.Size = new Size(960, 750);
            this.MinimumSize = new Size(880, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AllowDrop = true;

            // Drag & Drop
            this.DragEnter += Form1_DragEnter;
            this.DragDrop += Form1_DragDrop;

            // 1. Top Header Bar
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(18, 6, 18, 6),
                BackColor = ZeroTheme.Colors.Surface
            };

            lblAppTitle = new Label
            {
                Text = "⚡ ZEROZIP SOVEREIGN ARCHIVER",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(16, 6),
                AutoSize = true,
                ForeColor = ZeroTheme.Colors.Primary
            };
            pnlHeader.Controls.Add(lblAppTitle);

            lblAppSub = new Label
            {
                Text = "Ultra-Compression (Zstd/LZMA/Gorilla) & High-Performance Archive Suite — ZeroUniverse",
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                Location = new Point(17, 28),
                AutoSize = true,
                ForeColor = ZeroTheme.Colors.TextSecondary
            };
            pnlHeader.Controls.Add(lblAppSub);

            btnToggleTheme = new SimpleButton
            {
                Text = ZeroTheme.IsDark ? "☀️ Giao diện Sáng" : "🌙 Giao diện Tối",
                ButtonStyle = ZeroButtonStyle.Ghost,
                Size = new Size(140, 30),
                Location = new Point(780, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnToggleTheme.Click += BtnToggleTheme_Click;
            pnlHeader.Controls.Add(btnToggleTheme);

            // 2. WinRAR Action Toolbar
            BuildWinRarToolbar();

            // 3. TabControl Navigation
            tabControl = new TabControlEx
            {
                Dock = DockStyle.Fill,
                TabStyle = TabStyle.Pill,
                TabHeight = 36,
                TabWidth = 260
            };

            var tabExplorer = tabControl.AddTab("📁 Trình Duyệt Gói Nén (Explorer)", "");
            var tabStudio = tabControl.AddTab("⚡ Siêu Nén & Đóng Gói (Studio)", "");

            BuildArchiveExplorerTab(tabExplorer);
            BuildStudioTab(tabStudio);

            // Add controls in reverse dock order
            this.Controls.Add(tabControl);
            this.Controls.Add(pnlWinRarToolbar);
            this.Controls.Add(pnlHeader);
        }

        private void BuildWinRarToolbar()
        {
            pnlWinRarToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(12, 6, 12, 6),
                BackColor = ZeroTheme.Colors.Background
            };

            int x = 12;
            int btnWidth = 110;
            int gap = 8;

            tbBtnAdd = new SimpleButton
            {
                Text = "➕ Thêm...",
                ButtonStyle = ZeroButtonStyle.Primary,
                Location = new Point(x, 8),
                Size = new Size(btnWidth, 34)
            };
            tbBtnAdd.Click += (s, e) => tabControl.SelectedIndex = 1;
            pnlWinRarToolbar.Controls.Add(tbBtnAdd);
            x += btnWidth + gap;

            tbBtnExtract = new SimpleButton
            {
                Text = "📂 Giải nén...",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(x, 8),
                Size = new Size(btnWidth, 34)
            };
            tbBtnExtract.Click += TbBtnExtract_Click;
            pnlWinRarToolbar.Controls.Add(tbBtnExtract);
            x += btnWidth + gap;

            tbBtnTest = new SimpleButton
            {
                Text = "🧪 Kiểm tra",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(x, 8),
                Size = new Size(btnWidth, 34)
            };
            tbBtnTest.Click += TbBtnTest_Click;
            pnlWinRarToolbar.Controls.Add(tbBtnTest);
            x += btnWidth + gap;

            tbBtnView = new SimpleButton
            {
                Text = "👁️ Xem tệp",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(x, 8),
                Size = new Size(btnWidth, 34)
            };
            tbBtnView.Click += TbBtnView_Click;
            pnlWinRarToolbar.Controls.Add(tbBtnView);
            x += btnWidth + gap;

            tbBtnInfo = new SimpleButton
            {
                Text = "ℹ️ Thông tin",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(x, 8),
                Size = new Size(btnWidth, 34)
            };
            tbBtnInfo.Click += TbBtnInfo_Click;
            pnlWinRarToolbar.Controls.Add(tbBtnInfo);
            x += btnWidth + gap;

            tbBtnContextMenu = new SimpleButton
            {
                Text = ShellContextMenuService.IsRegistered() ? "✓ Đã bật Menu Explorer" : "⚙️ Bật Menu Explorer",
                ButtonStyle = ZeroButtonStyle.Ghost,
                Location = new Point(x, 8),
                Size = new Size(180, 34)
            };
            tbBtnContextMenu.Click += TbBtnContextMenu_Click;
            pnlWinRarToolbar.Controls.Add(tbBtnContextMenu);
        }

        #region Tab 1: WinRAR Archive Explorer

        private void BuildArchiveExplorerTab(TabPageEx page)
        {
            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            page.Controls.Add(pnlMain);

            // Path & Breadcrumb Bar
            var pnlPath = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(0, 2, 0, 6)
            };

            btnUpLevel = new SimpleButton
            {
                Text = "⬆️ Lên",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(0, 2),
                Size = new Size(68, 30)
            };
            btnUpLevel.Click += (s, e) =>
            {
                if (_archiveExplorer.NavigateUp())
                {
                    RefreshFileListView();
                }
            };
            pnlPath.Controls.Add(btnUpLevel);

            txtCurrentPath = new ButtonEdit
            {
                Location = new Point(74, 2),
                Size = new Size(640, 30),
                ReadOnly = true,
                Text = "\\"
            };
            pnlPath.Controls.Add(txtCurrentPath);

            btnOpenArchive = new SimpleButton
            {
                Text = "📂 Mở gói...",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(720, 2),
                Size = new Size(100, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnOpenArchive.Click += (s, e) => BrowseAndOpenArchive();
            pnlPath.Controls.Add(btnOpenArchive);

            pnlMain.Controls.Add(pnlPath);

            // Status Bar
            statusStripArchive = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextSecondary
            };

            lblStatusItems = new ToolStripStatusLabel("Chưa mở gói nén nào.") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            lblStatusSelection = new ToolStripStatusLabel("") { AutoSize = true };
            lblStatusFormat = new ToolStripStatusLabel("") { AutoSize = true };

            statusStripArchive.Items.AddRange(new ToolStripItem[] { lblStatusItems, lblStatusSelection, lblStatusFormat });
            pnlMain.Controls.Add(statusStripArchive);

            // File ListView (WinRAR View)
            imgListFiles = CreateImageList();

            lvArchiveFiles = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = true,
                SmallImageList = imgListFiles,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Font = new Font("Segoe UI", 9.25f, FontStyle.Regular)
            };

            lvArchiveFiles.Columns.Add("Tên", 320);
            lvArchiveFiles.Columns.Add("Dung lượng gốc", 120, HorizontalAlignment.Right);
            lvArchiveFiles.Columns.Add("Kích thước nén / Tỷ lệ", 130, HorizontalAlignment.Right);
            lvArchiveFiles.Columns.Add("Ngày sửa đổi", 150);
            lvArchiveFiles.Columns.Add("Loại", 100);

            lvArchiveFiles.DoubleClick += LvArchiveFiles_DoubleClick;
            lvArchiveFiles.SelectedIndexChanged += LvArchiveFiles_SelectedIndexChanged;

            // In-app Context Menu
            BuildContextMenu();
            lvArchiveFiles.ContextMenuStrip = contextMenuFiles;

            pnlMain.Controls.Add(lvArchiveFiles);
        }

        private ImageList CreateImageList()
        {
            var list = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };

            // Folder Icon
            Bitmap folderBmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(folderBmp))
            {
                g.Clear(Color.Transparent);
                using var brush = new SolidBrush(Color.FromArgb(245, 185, 66));
                g.FillRectangle(brush, 1, 3, 14, 11);
                using var topBrush = new SolidBrush(Color.FromArgb(225, 165, 46));
                g.FillRectangle(topBrush, 1, 1, 6, 3);
            }
            list.Images.Add("folder", folderBmp);

            // File Icon
            Bitmap fileBmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(fileBmp))
            {
                g.Clear(Color.Transparent);
                using var brush = new SolidBrush(Color.FromArgb(90, 150, 240));
                g.FillRectangle(brush, 2, 1, 12, 14);
                using var cornerBrush = new SolidBrush(Color.FromArgb(230, 240, 255));
                g.FillPolygon(cornerBrush, new Point[] { new Point(10, 1), new Point(14, 5), new Point(10, 5) });
            }
            list.Images.Add("file", fileBmp);

            return list;
        }

        private void BuildContextMenu()
        {
            contextMenuFiles = new ContextMenuStrip
            {
                RenderMode = ToolStripRenderMode.System,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary
            };

            var itemOpen = new ToolStripMenuItem("👁️ Mở / Xem tệp (Open/View)", null, (s, e) => OpenSelectedFile());
            var itemExtract = new ToolStripMenuItem("📂 Giải nén tệp đã chọn... (Extract Selected)", null, (s, e) => ExtractSelectedFiles());
            var itemCopy = new ToolStripMenuItem("📋 Sao chép đường dẫn (Copy Path)", null, (s, e) => CopySelectedPath());
            var itemInfo = new ToolStripMenuItem("ℹ️ Thuộc tính tệp (Properties)", null, (s, e) => ShowSelectedFileInfo());

            contextMenuFiles.Items.AddRange(new ToolStripItem[] { itemOpen, itemExtract, new ToolStripSeparator(), itemCopy, itemInfo });
        }

        private async void BrowseAndOpenArchive()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Gói nén ZeroZip (*.ztar;*.exe)|*.ztar;*.exe|Tất cả tệp (*.*)|*.*",
                Title = "Chọn tệp nén ZeroZip để mở"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                await LoadArchiveAsync(ofd.FileName);
            }
        }

        public async Task LoadArchiveAsync(string archivePath)
        {
            var overlay = ShowLoading("Đang nạp gói nén", $"Đang đọc cấu trúc tệp: {Path.GetFileName(archivePath)}...");
            try
            {
                string? password = null;
                var res = await _archiveExplorer.LoadArchiveAsync(archivePath, password);

                if (!res.Success && res.IsEncrypted)
                {
                    HideLoading();
                    password = PromptPassword();
                    if (string.IsNullOrEmpty(password)) return;
                    overlay = ShowLoading("Đang giải mã", "Đang xác thực mật khẩu AES-256-GCM...");
                    res = await _archiveExplorer.LoadArchiveAsync(archivePath, password);
                }

                if (!res.Success)
                {
                    ShowToast(res.ErrorMessage ?? "Lỗi không xác định khi mở gói nén.", "Lỗi nạp tệp", ToastType.Error);
                    return;
                }

                RefreshFileListView();

                var f = res.Footer!;
                string enc = f.IsEncrypted ? "🔒 AES-256-GCM" : "🔓 Public";
                lblStatusFormat.Text = $"ZTAR v{f.Version} • {f.Method} • {enc}";
                ShowToast($"Đã nạp thành công {res.TotalEntries} mục ({FormatSize(res.TotalOriginalSize)})", "Duyệt Gói Nén", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast("Lỗi mở gói nén: " + ex.Message, "Lỗi", ToastType.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private void RefreshFileListView()
        {
            lvArchiveFiles.BeginUpdate();
            lvArchiveFiles.Items.Clear();

            string currentVirtual = _archiveExplorer.CurrentVirtualPath;
            txtCurrentPath.Text = string.IsNullOrEmpty(currentVirtual) ? "\\" : "\\" + currentVirtual.Replace('/', '\\');

            var items = _archiveExplorer.GetCurrentDirectoryItems();

            foreach (var item in items)
            {
                var lvi = new ListViewItem(item.Name, item.IsDirectory ? "folder" : "file")
                {
                    Tag = item
                };

                if (item.IsDirectory)
                {
                    lvi.SubItems.Add("");
                    lvi.SubItems.Add("<Thư mục>");
                    lvi.SubItems.Add(item.ModificationTime == default ? "" : item.ModificationTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                    lvi.SubItems.Add("Thư mục");
                }
                else
                {
                    lvi.SubItems.Add(FormatSize(item.Size));
                    lvi.SubItems.Add("Zstandard");
                    lvi.SubItems.Add(item.ModificationTime == default ? "" : item.ModificationTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                    string ext = Path.GetExtension(item.Name).TrimStart('.').ToUpperInvariant();
                    lvi.SubItems.Add(string.IsNullOrEmpty(ext) ? "Tệp tin" : $"{ext} File");
                }

                lvArchiveFiles.Items.Add(lvi);
            }

            lvArchiveFiles.EndUpdate();

            int dirCount = items.Count(i => i.IsDirectory);
            int fileCount = items.Count(i => !i.IsDirectory);
            long totalSize = items.Where(i => !i.IsDirectory).Sum(i => i.Size);
            lblStatusItems.Text = $"{fileCount} tệp, {dirCount} thư mục | Thư mục hiện tại: {FormatSize(totalSize)}";
            lblStatusSelection.Text = "0 mục được chọn";
        }

        private void LvArchiveFiles_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (lvArchiveFiles.SelectedItems.Count == 0)
            {
                lblStatusSelection.Text = "0 mục được chọn";
                return;
            }

            long selSize = 0;
            int count = lvArchiveFiles.SelectedItems.Count;
            foreach (ListViewItem item in lvArchiveFiles.SelectedItems)
            {
                if (item.Tag is VirtualItem v && !v.IsDirectory)
                    selSize += v.Size;
            }

            lblStatusSelection.Text = $"Đã chọn: {count} mục ({FormatSize(selSize)})";
        }

        private async void LvArchiveFiles_DoubleClick(object? sender, EventArgs e)
        {
            if (lvArchiveFiles.SelectedItems.Count == 0) return;
            var selected = lvArchiveFiles.SelectedItems[0].Tag as VirtualItem;
            if (selected == null) return;

            if (selected.IsDirectory)
            {
                _archiveExplorer.NavigateTo(selected.Name);
                RefreshFileListView();
            }
            else
            {
                await OpenFileAsync(selected.RelativePath);
            }
        }

        private async Task OpenFileAsync(string relativePath)
        {
            var overlay = ShowLoading("Đang trích xuất", $"Đang chuẩn bị xem tệp: {Path.GetFileName(relativePath)}...");
            try
            {
                string tempFile = await _archiveExplorer.ExtractSingleToTempAsync(relativePath);
                var psi = new ProcessStartInfo(tempFile) { UseShellExecute = true };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ShowToast("Không thể mở tệp: " + ex.Message, "Lỗi xem tệp", ToastType.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private void OpenSelectedFile()
        {
            if (lvArchiveFiles.SelectedItems.Count == 0) return;
            var sel = lvArchiveFiles.SelectedItems[0].Tag as VirtualItem;
            if (sel != null && !sel.IsDirectory)
            {
                _ = OpenFileAsync(sel.RelativePath);
            }
        }

        private async void ExtractSelectedFiles()
        {
            if (lvArchiveFiles.SelectedItems.Count == 0)
            {
                TbBtnExtract_Click(this, EventArgs.Empty);
                return;
            }

            using var fbd = new FolderBrowserDialog { Description = "Chọn thư mục giải nén các tệp đã chọn" };
            if (fbd.ShowDialog() != DialogResult.OK) return;

            string dest = fbd.SelectedPath;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ListViewItem item in lvArchiveFiles.SelectedItems)
            {
                if (item.Tag is VirtualItem v)
                {
                    set.Add(v.RelativePath);
                }
            }

            var dialog = new OperationProgressDialog(OperationType.Extract, _archiveExplorer.CurrentArchivePath!, dest);
            dialog.ShowDialog(this);
            if (dialog.IsCompleted)
            {
                ShowToast($"Đã giải nén {set.Count} mục vào: {dest}", "Giải nén thành công", ToastType.Success);
            }
        }

        private void CopySelectedPath()
        {
            if (lvArchiveFiles.SelectedItems.Count == 0) return;
            var paths = lvArchiveFiles.SelectedItems.Cast<ListViewItem>()
                .Select(i => (i.Tag as VirtualItem)?.RelativePath ?? i.Text);
            Clipboard.SetText(string.Join(Environment.NewLine, paths));
            ShowToast("Đã sao chép đường dẫn vào Clipboard.", "Sao chép", ToastType.Info);
        }

        private void ShowSelectedFileInfo()
        {
            if (lvArchiveFiles.SelectedItems.Count == 0) return;
            var sel = lvArchiveFiles.SelectedItems[0].Tag as VirtualItem;
            if (sel == null) return;

            string info = $"Tên: {sel.Name}\nĐường dẫn nội bộ: {sel.RelativePath}\nLoại: {(sel.IsDirectory ? "Thư mục" : "Tệp tin")}\nDung lượng: {FormatSize(sel.Size)}\nNgày sửa đổi: {sel.ModificationTime.LocalDateTime}";
            ModalDialog.Info(this, "Thuộc Tính Tệp", info);
        }

        #endregion

        #region Toolbar Action Handlers

        private void TbBtnExtract_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_archiveExplorer.CurrentArchivePath))
            {
                ShowToast("Chưa có gói nén nào được mở. Vui lòng chọn gói nén trước.", "Thông báo", ToastType.Warning);
                BrowseAndOpenArchive();
                return;
            }

            using var fbd = new FolderBrowserDialog { Description = "Chọn thư mục giải nén toàn bộ kho lưu trữ" };
            if (fbd.ShowDialog() != DialogResult.OK) return;

            var dialog = new OperationProgressDialog(OperationType.Extract, _archiveExplorer.CurrentArchivePath, fbd.SelectedPath);
            dialog.ShowDialog(this);
            if (dialog.IsCompleted)
            {
                ShowToast($"Giải nén hoàn tất vào: {fbd.SelectedPath}", "Thành công", ToastType.Success);
            }
        }

        private void TbBtnTest_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_archiveExplorer.CurrentArchivePath))
            {
                ShowToast("Vui lòng mở một gói nén để kiểm tra.", "Thông báo", ToastType.Warning);
                BrowseAndOpenArchive();
                return;
            }

            var dialog = new OperationProgressDialog(OperationType.Test, _archiveExplorer.CurrentArchivePath);
            dialog.ShowDialog(this);
            if (dialog.IsCompleted)
            {
                ModalDialog.Info(this, "Kiểm Tra Hoàn Tất", "✔ Gói nén hoàn toàn nguyên vẹn, mã kiểm tra CRC32 hợp lệ, không có lỗi payload.");
            }
        }

        private void TbBtnView_Click(object? sender, EventArgs e)
        {
            OpenSelectedFile();
        }

        private void TbBtnInfo_Click(object? sender, EventArgs e)
        {
            if (_archiveExplorer.CurrentFooter == null)
            {
                ShowToast("Vui lòng mở một gói nén để xem thông tin.", "Thông báo", ToastType.Warning);
                return;
            }

            var f = _archiveExplorer.CurrentFooter;
            double ratio = f.OriginalSize > 0 ? (double)f.PayloadSize / f.OriginalSize : 0;
            string msg = $"Tệp lưu trữ: {Path.GetFileName(_archiveExplorer.CurrentArchivePath)}\n"
                       + $"Định dạng: ZTAR Container v{f.Version} ({(f.IsAppended ? "SFX Executable" : "Tệp độc lập")})\n"
                       + $"Thuật toán nén: {f.Method}\n"
                       + $"Tổng dung lượng gốc: {FormatSize(f.OriginalSize)}\n"
                       + $"Dung lượng nén: {FormatSize(f.PayloadSize)} (Tỷ lệ: {ratio:P1})\n"
                       + $"Mã kiểm tra toàn vẹn CRC32: 0x{f.Crc32:X8}\n"
                       + $"Bảo mật: {(f.IsEncrypted ? "Mã hóa AEAD AES-256-GCM" : "Không mã hóa")}\n"
                       + $"Cắt khối (Multi-part): {(f.IsMultiPart ? $"{f.PartCount} volumes" : "Không")}\n"
                       + $"Precompressed (Repack): {(f.IsPrecompressed ? "Có" : "Không")}";

            ModalDialog.Info(this, "Thông Tin Kho Lưu Trữ (Archive Info)", msg);
        }

        private void TbBtnContextMenu_Click(object? sender, EventArgs e)
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
                    tbBtnContextMenu.Text = "⚙️ Bật Menu Explorer";
                    ShowToast("Đã gỡ bỏ ZeroZip khỏi menu chuột phải Windows Explorer.", "Menu ngữ cảnh", ToastType.Info);
                }
            }
            else
            {
                if (ShellContextMenuService.Register())
                {
                    tbBtnContextMenu.Text = "✓ Đã bật Menu Explorer";
                    ShowToast("Đã đăng ký thành công ZeroZip vào menu chuột phải Windows Explorer!", "Menu ngữ cảnh", ToastType.Success);
                }
                else
                {
                    ShowToast("Không thể ghi cấu hình Registry người dùng.", "Lỗi Menu", ToastType.Error);
                }
            }
        }

        private void BtnToggleTheme_Click(object? sender, EventArgs e)
        {
            ZeroTheme.CurrentMode = ZeroTheme.IsDark ? ZeroThemeMode.Light : ZeroThemeMode.Dark;
            btnToggleTheme.Text = ZeroTheme.IsDark ? "☀️ Giao diện Sáng" : "🌙 Giao diện Tối";
            lblAppTitle.ForeColor = ZeroTheme.Colors.Primary;
            lblAppSub.ForeColor = ZeroTheme.Colors.TextSecondary;
            lvArchiveFiles.BackColor = ZeroTheme.Colors.Surface;
            lvArchiveFiles.ForeColor = ZeroTheme.Colors.TextPrimary;
            statusStripArchive.BackColor = ZeroTheme.Colors.Surface;
            statusStripArchive.ForeColor = ZeroTheme.Colors.TextSecondary;
            pnlWinRarToolbar.BackColor = ZeroTheme.Colors.Background;
            ShowToast(ZeroTheme.IsDark ? "Đã chuyển sang giao diện Tối (Obsidian Dark)" : "Đã chuyển sang giao diện Sáng (Clean Light)", "Chủ đề", ToastType.Info);
        }

        #endregion

        #region Tab 2: Studio SFX & Packer

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
            const int cardWidth = 880;

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
                Size = new Size(580, 32),
                PlaceholderText = "Chọn hoặc kéo thả thư mục / tệp cần nén..."
            };
            txtSource.ButtonClick += (s, e) => BrowseSourceFolder();
            txtSource.TextChanged += (s, e) => AutoDetectAndTune(txtSource.Text);
            cardSource.ContentPanel.Controls.Add(txtSource);

            btnBrowseSourceFolder = new SimpleButton
            {
                Text = "📁 Thư mục",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(604, 26),
                Size = new Size(115, 32)
            };
            btnBrowseSourceFolder.Click += (s, e) => BrowseSourceFolder();
            cardSource.ContentPanel.Controls.Add(btnBrowseSourceFolder);

            btnBrowseSourceFile = new SimpleButton
            {
                Text = "📄 Tệp tin",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(727, 26),
                Size = new Size(115, 32)
            };
            btnBrowseSourceFile.Click += (s, e) => BrowseSourceFile();
            cardSource.ContentPanel.Controls.Add(btnBrowseSourceFile);

            var lblDst = new Label { Text = "Lưu thành (.exe SFX / .ztar):", Location = new Point(14, 66), AutoSize = true };
            cardSource.ContentPanel.Controls.Add(lblDst);

            txtDest = new ButtonEdit
            {
                Location = new Point(14, 86),
                Size = new Size(705, 32),
                PlaceholderText = "Đường dẫn tệp đầu ra (.ztar hoặc .exe SFX)..."
            };
            txtDest.ButtonClick += (s, e) => BrowseDest();
            cardSource.ContentPanel.Controls.Add(txtDest);

            btnBrowseDest = new SimpleButton
            {
                Text = "💾 Lưu...",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(727, 86),
                Size = new Size(115, 32)
            };
            btnBrowseDest.Click += (s, e) => BrowseDest();
            cardSource.ContentPanel.Controls.Add(btnBrowseDest);

            pnlContainer.Controls.Add(cardSource);
            currentY += 203;

            // --- Card 2: Thuật toán & Động cơ nén ZeroUniverse ---
            var cardEngine = new Card
            {
                Title = "2. Thuật Toán & Động Cơ Nén (ZeroUniverse)",
                Subtitle = "Tự động nhận diện cấu trúc tệp (DataClassifier) & chọn thuật toán tối ưu",
                StepNumber = 2,
                Size = new Size(cardWidth, 175),
                Location = new Point(10, currentY),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblMethod = new Label { Text = "Thuật toán nén:", Location = new Point(14, 6), AutoSize = true };
            cardEngine.ContentPanel.Controls.Add(lblMethod);

            cmbMethod = new ComboBoxEdit
            {
                Location = new Point(14, 26),
                Size = new Size(270, 32)
            };
            cmbMethod.Items.Add("⭐ Tự động nhận diện (Adaptive Auto-Detect)");
            cmbMethod.Items.Add("Zstandard (Nhanh, đa luồng, giải nén GB/s)");
            cmbMethod.Items.Add("LZMA (Nén sâu nhất, chuẩn đóng gói)");
            cmbMethod.Items.Add("Brotli (Nén văn bản / Web / JSON)");
            cmbMethod.Items.Add("Không nén (Store - Đóng gói thô)");
            cmbMethod.Items.Add("ZeroTelemetry (Gorilla XOR / Delta DoD)");
            cmbMethod.SelectedIndex = 0;
            cardEngine.ContentPanel.Controls.Add(cmbMethod);

            var lblProfile = new Label { Text = "Mức nén (Profile):", Location = new Point(300, 6), AutoSize = true };
            cardEngine.ContentPanel.Controls.Add(lblProfile);

            cmbProfile = new ComboBoxEdit
            {
                Location = new Point(300, 26),
                Size = new Size(180, 32)
            };
            cmbProfile.Items.Add("Nhanh (Fast)");
            cmbProfile.Items.Add("Cân bằng (Normal)");
            cmbProfile.Items.Add("Siêu nén (Ultra)");
            cmbProfile.SelectedIndex = 2;
            cardEngine.ContentPanel.Controls.Add(cmbProfile);

            var lblSplit = new Label { Text = "Cắt Volume (Split):", Location = new Point(500, 6), AutoSize = true };
            cardEngine.ContentPanel.Controls.Add(lblSplit);

            cmbSplitMode = new ComboBoxEdit
            {
                Location = new Point(500, 26),
                Size = new Size(342, 32)
            };
            cmbSplitMode.Items.Add("Nhúng trực tiếp (1 tệp)");
            cmbSplitMode.Items.Add("Cắt mảnh 2GB (FAT32/Flash an toàn)");
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
                Text = "Khử trùng lặp khoảng cách xa (LDM 2GB window)",
                Location = new Point(530, 66),
                Size = new Size(310, 24)
            };
            cardEngine.ContentPanel.Controls.Add(chkLong);

            lblAutoDetectBadge = new Label
            {
                Text = "Chưa nạp dữ liệu phân tích.",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Location = new Point(14, 98),
                AutoSize = true
            };
            cardEngine.ContentPanel.Controls.Add(lblAutoDetectBadge);

            pnlContainer.Controls.Add(cardEngine);
            currentY += 183;

            // --- Card 3: Bảo mật & Mã hóa ---
            var cardSecurity = new Card
            {
                Title = "3. Bảo Mật & Mã Hóa Dữ Liệu",
                Subtitle = "Mã hóa có xác thực cấp quân sự AES-256-GCM AEAD (Chống giả mạo)",
                StepNumber = 3,
                Size = new Size(cardWidth, 125),
                Location = new Point(10, currentY),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblPass = new Label { Text = "Mật khẩu bảo vệ (Để trống nếu không mã hóa):", Location = new Point(14, 6), AutoSize = true };
            cardSecurity.ContentPanel.Controls.Add(lblPass);

            txtPassword = new TextEdit
            {
                Location = new Point(14, 26),
                Size = new Size(500, 32),
                PlaceholderText = "Nhập mật khẩu an toàn...",
                UseSystemPasswordChar = true,
                ShowPasswordEyeButton = true
            };
            cardSecurity.ContentPanel.Controls.Add(txtPassword);

            var lblCryptoNotice = new Label
            {
                Text = "🔒 Sử dụng AES-256-GCM AEAD + Key Derivation Argon2/PBKDF2.",
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Location = new Point(14, 62),
                AutoSize = true
            };
            cardSecurity.ContentPanel.Controls.Add(lblCryptoNotice);

            pnlContainer.Controls.Add(cardSecurity);
            currentY += 133;

            // --- Card 4: Thực thi & Tiến trình ---
            var cardExec = new Card
            {
                Title = "4. Thực Thi & Giám Sát Tiến Trình",
                Subtitle = "Khởi chạy thuật toán siêu nén hoặc kiểm tra trước tỉ lệ tiết kiệm",
                StepNumber = 4,
                Size = new Size(cardWidth, 175),
                Location = new Point(10, currentY),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            progressBar = new ProgressBarControl
            {
                Location = new Point(14, 12),
                Size = new Size(cardWidth - 45, 18),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cardExec.ContentPanel.Controls.Add(progressBar);

            lblStatus = new Label
            {
                Text = "Sẵn sàng thực hiện.",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Location = new Point(14, 36),
                Size = new Size(cardWidth - 45, 20),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cardExec.ContentPanel.Controls.Add(lblStatus);

            btnCompress = new SimpleButton
            {
                Text = "⚡ Bắt Đầu Nén",
                ButtonStyle = ZeroButtonStyle.Primary,
                Location = new Point(14, 62),
                Size = new Size(180, 40)
            };
            btnCompress.Click += BtnCompress_Click;
            cardExec.ContentPanel.Controls.Add(btnCompress);

            btnEstimate = new SimpleButton
            {
                Text = "📊 Ước Tính Tỉ Lệ",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(204, 62),
                Size = new Size(160, 40)
            };
            btnEstimate.Click += BtnEstimate_Click;
            cardExec.ContentPanel.Controls.Add(btnEstimate);

            btnCancel = new SimpleButton
            {
                Text = "Dừng Lại",
                ButtonStyle = ZeroButtonStyle.Danger,
                Location = new Point(374, 62),
                Size = new Size(100, 40),
                Enabled = false
            };
            btnCancel.Click += (s, e) =>
            {
                _cts?.Cancel();
                btnCancel.Enabled = false;
                lblStatus.Text = "Đang hủy tiến trình...";
            };
            cardExec.ContentPanel.Controls.Add(btnCancel);

            pnlContainer.Controls.Add(cardExec);
            currentY += 185;

            pnlContainer.AutoScrollMinSize = new Size(0, currentY + 15);
        }

        #endregion

        #region Drag & Drop Support

        private void Form1_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private async void Form1_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true) return;
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            string path = files[0];
            string ext = Path.GetExtension(path).ToLowerInvariant();

            // If dropped an archive or SFX, open in Archive Explorer
            if (ext == ".exe" || ext == ".ztar")
            {
                tabControl.SelectedIndex = 0;
                await LoadArchiveAsync(path);
                return;
            }

            // Otherwise, set as Studio source
            txtSource.Text = path;
            AutoSuggestDest(path);
            AutoDetectAndTune(path);
            tabControl.SelectedIndex = 1;
            ShowToast($"Đã chọn nguồn nén: {Path.GetFileName(path)}", "Kéo thả thành công", ToastType.Info);
        }

        #endregion

        #region Studio Logic & Actions

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

        private void AutoDetectAndTune(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !(Directory.Exists(path) || File.Exists(path))) return;

            Task.Run(() =>
            {
                try
                {
                    var cls = DataClassifier.ClassifyPath(path);
                    if (this.IsDisposed) return;
                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            lblAutoDetectBadge.Text = $"💡 Nhận diện: [{cls.DetectedType}] -> Khuyến nghị: {cls.RecommendedMethod} | {cls.Reason}";
                        }));
                    }
                    else
                    {
                        lblAutoDetectBadge.Text = $"💡 Nhận diện: [{cls.DetectedType}] -> Khuyến nghị: {cls.RecommendedMethod} | {cls.Reason}";
                    }
                }
                catch { }
            });
        }

        private void AutoSuggestDest(string sourcePath)
        {
            if (string.IsNullOrEmpty(txtDest.Text))
            {
                string? dir = Path.GetDirectoryName(sourcePath);
                string name = Path.GetFileName(sourcePath);
                if (string.IsNullOrEmpty(name)) name = "Archive";
                txtDest.Text = Path.Combine(dir ?? "", name + ".ztar");
            }
        }

        private void BrowseDest()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "ZTar Container (*.ztar)|*.ztar|Executable SFX (*.exe)|*.exe",
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
                options.Workers = Environment.ProcessorCount; // ZeroUniverse parallel compute
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
                ShowToast(ex.Message, "Lỗi nén dữ liệu", ToastType.Error);
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
