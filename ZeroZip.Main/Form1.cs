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

        // UI Menu Bar
        private MenuStrip menuBar = null!;
        private ToolStripMenuItem menuFile = null!;
        private ToolStripMenuItem itemOpenArchive = null!;
        private ToolStripMenuItem itemNewArchive = null!;
        private ToolStripMenuItem itemCloseArchive = null!;
        private ToolStripMenuItem itemExit = null!;
        private ToolStripMenuItem menuCommands = null!;
        private ToolStripMenuItem itemAddFiles = null!;
        private ToolStripMenuItem itemExtractTo = null!;
        private ToolStripMenuItem itemTestArchive = null!;
        private ToolStripMenuItem itemViewFile = null!;
        private ToolStripMenuItem itemSelectAll = null!;
        private ToolStripMenuItem menuTools = null!;
        private ToolStripMenuItem itemEstimate = null!;
        private ToolStripMenuItem itemShellIntegrate = null!;
        private ToolStripMenuItem menuOptions = null!;
        private ToolStripMenuItem itemToggleTheme = null!;
        private ToolStripMenuItem menuLanguage = null!;
        private ToolStripMenuItem itemLangVi = null!;
        private ToolStripMenuItem itemLangEn = null!;
        private ToolStripMenuItem menuHelp = null!;
        private ToolStripMenuItem itemAbout = null!;

        // Action Toolbar (Streamlined 4 Quick Actions)
        private Panel pnlWinRarToolbar = null!;
        private SimpleButton tbBtnAdd = null!;
        private SimpleButton tbBtnOpen = null!;
        private SimpleButton tbBtnExtract = null!;
        private SimpleButton tbBtnTest = null!;

        // Navigation Tabs
        private TabControlEx tabControl = null!;
        private TabPageEx tabExplorer = null!;
        private TabPageEx tabStudio = null!;

        // --- Tab 1: WinRAR Archive Explorer ---
        private readonly ArchiveExplorerService _archiveExplorer;
        private ButtonEdit txtCurrentPath = null!;
        private SimpleButton btnUpLevel = null!;
        private SimpleButton btnRefresh = null!;
        private ListView lvArchiveFiles = null!;
        private ListViewColumnSorter _columnSorter = null!;
        private ColumnHeader colName = null!;
        private ColumnHeader colOrigSize = null!;
        private ColumnHeader colPackedSize = null!;
        private ColumnHeader colRatio = null!;
        private ColumnHeader colMethod = null!;
        private ColumnHeader colModified = null!;
        private ColumnHeader colCrc = null!;
        private ColumnHeader colType = null!;
        private ColumnHeader colAttr = null!;
        private ImageList imgListFiles = null!;
        private ContextMenuStrip contextMenuFiles = null!;
        private ToolStripMenuItem itemCtxOpen = null!;
        private ToolStripMenuItem itemCtxExtract = null!;
        private ToolStripMenuItem itemCtxCopy = null!;
        private ToolStripMenuItem itemCtxInfo = null!;
        private ToolStripMenuItem itemCtxOpenAnother = null!;
        private ToolStripMenuItem itemCtxTest = null!;
        private ToolStripMenuItem itemCtxRefresh = null!;
        private ToolStripMenuItem itemCtxSelectAll = null!;
        private StatusStrip statusStripArchive = null!;
        private ToolStripStatusLabel lblStatusItems = null!;
        private ToolStripStatusLabel lblStatusSelection = null!;
        private ToolStripStatusLabel lblStatusFormat = null!;

        // --- Tab 2: Studio SFX & Packer ---
        private Card cardSource = null!;
        private Card cardEngine = null!;
        private Card cardSecurity = null!;
        private Card cardExec = null!;
        private Label lblSrc = null!;
        private ButtonEdit txtSource = null!;
        private SimpleButton btnBrowseSourceFolder = null!;
        private SimpleButton btnBrowseSourceFile = null!;
        private Label lblDst = null!;
        private ButtonEdit txtDest = null!;
        private SimpleButton btnBrowseDest = null!;
        private Label lblMethod = null!;
        private ComboBoxEdit cmbMethod = null!;
        private Label lblProfile = null!;
        private ComboBoxEdit cmbProfile = null!;
        private Label lblSplit = null!;
        private ComboBoxEdit cmbSplitMode = null!;
        private CheckEdit chkWrapZip = null!;
        private CheckEdit chkPrecomp = null!;
        private CheckEdit chkLong = null!;
        private Label lblAutoDetectBadge = null!;
        private Label lblPass = null!;
        private TextEdit txtPassword = null!;
        private Label lblCryptoNotice = null!;
        private SimpleButton btnEstimate = null!;
        private SimpleButton btnCompress = null!;
        private SimpleButton btnCancel = null!;
        private ProgressBarControl progressBar = null!;
        private Label lblStatus = null!;

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

            LocalizationService.LanguageChanged += OnLanguageChanged;
            this.FormClosed += (s, e) => LocalizationService.LanguageChanged -= OnLanguageChanged;
            ApplyLocalization();
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
            this.Text = LocalizationService.Get("App_Title");
            TrySetWindowIcon();
            this.Size = new Size(1000, 750);
            this.MinimumSize = new Size(880, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AllowDrop = true;

            // Drag & Drop
            this.DragEnter += Form1_DragEnter;
            this.DragDrop += Form1_DragDrop;

            // 1. Menu Bar (WinRAR & 7-Zip Style)
            menuBar = new MenuStrip
            {
                Dock = DockStyle.Top,
                RenderMode = ToolStripRenderMode.System,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            // Menu: File (Tập tin)
            menuFile = new ToolStripMenuItem(LocalizationService.Get("Menu_File"));
            itemOpenArchive = new ToolStripMenuItem(LocalizationService.Get("Menu_OpenArchive"), null, (s, e) => BrowseAndOpenArchive(), Keys.Control | Keys.O);
            itemNewArchive = new ToolStripMenuItem(LocalizationService.Get("Menu_NewArchive"), null, (s, e) => tabControl.SelectedIndex = 1, Keys.Control | Keys.N);
            itemCloseArchive = new ToolStripMenuItem(LocalizationService.Get("Menu_CloseArchive"), null, (s, e) => CloseArchive());
            itemExit = new ToolStripMenuItem(LocalizationService.Get("Menu_Exit"), null, (s, e) => this.Close(), Keys.Alt | Keys.F4);
            menuFile.DropDownItems.AddRange(new ToolStripItem[] { itemOpenArchive, itemNewArchive, itemCloseArchive, new ToolStripSeparator(), itemExit });

            // Menu: Commands (Lệnh)
            menuCommands = new ToolStripMenuItem(LocalizationService.Get("Menu_Commands"));
            itemAddFiles = new ToolStripMenuItem(LocalizationService.Get("Menu_AddFiles"), null, (s, e) => tabControl.SelectedIndex = 1);
            itemExtractTo = new ToolStripMenuItem(LocalizationService.Get("Menu_ExtractTo"), null, (s, e) => TbBtnExtract_Click(s, e));
            itemTestArchive = new ToolStripMenuItem(LocalizationService.Get("Menu_TestArchive"), null, (s, e) => TbBtnTest_Click(s, e));
            itemViewFile = new ToolStripMenuItem(LocalizationService.Get("Menu_ViewFile"), null, (s, e) => OpenSelectedFile());
            itemSelectAll = new ToolStripMenuItem(LocalizationService.Get("Menu_SelectAll"), null, (s, e) =>
            {
                foreach (ListViewItem it in lvArchiveFiles.Items) it.Selected = true;
            }, Keys.Control | Keys.A);
            menuCommands.DropDownItems.AddRange(new ToolStripItem[] { itemAddFiles, itemExtractTo, itemTestArchive, itemViewFile, new ToolStripSeparator(), itemSelectAll });

            // Menu: Tools (Công cụ)
            menuTools = new ToolStripMenuItem(LocalizationService.Get("Menu_Tools"));
            itemEstimate = new ToolStripMenuItem(LocalizationService.Get("Menu_Estimate"), null, (s, e) => { tabControl.SelectedIndex = 1; BtnEstimate_Click(s, e); });
            itemShellIntegrate = new ToolStripMenuItem(LocalizationService.Get("Menu_ShellIntegrate"), null, (s, e) => TbBtnContextMenu_Click(s, e));
            menuTools.DropDownItems.AddRange(new ToolStripItem[] { itemEstimate, itemShellIntegrate });

            // Menu: Options (Tùy chọn)
            menuOptions = new ToolStripMenuItem(LocalizationService.Get("Menu_Options"));
            itemToggleTheme = new ToolStripMenuItem(LocalizationService.Get("Menu_ToggleTheme"), null, (s, e) => BtnToggleTheme_Click(s, e), Keys.Control | Keys.T);
            menuOptions.DropDownItems.AddRange(new ToolStripItem[] { itemToggleTheme });

            // Menu: Language (Ngôn ngữ)
            menuLanguage = new ToolStripMenuItem(LocalizationService.Get("Menu_Language"));
            itemLangVi = new ToolStripMenuItem(LocalizationService.Get("Menu_Lang_Vi"), null, (s, e) => LocalizationService.SetLanguage(AppLanguage.Vietnamese));
            itemLangEn = new ToolStripMenuItem(LocalizationService.Get("Menu_Lang_En"), null, (s, e) => LocalizationService.SetLanguage(AppLanguage.English));
            itemLangVi.Checked = LocalizationService.CurrentLanguage == AppLanguage.Vietnamese;
            itemLangEn.Checked = LocalizationService.CurrentLanguage == AppLanguage.English;
            menuLanguage.DropDownItems.AddRange(new ToolStripItem[] { itemLangVi, itemLangEn });

            // Menu: Help (Trợ giúp)
            menuHelp = new ToolStripMenuItem(LocalizationService.Get("Menu_Help"));
            itemAbout = new ToolStripMenuItem(LocalizationService.Get("Menu_About"), null, (s, e) =>
            {
                ModalDialog.Info(this, LocalizationService.Get("Msg_AboutTitle"), LocalizationService.Get("Msg_AboutText"));
            });
            menuHelp.DropDownItems.AddRange(new ToolStripItem[] { itemAbout });

            menuBar.Items.AddRange(new ToolStripItem[] { menuFile, menuCommands, menuTools, menuOptions, menuLanguage, menuHelp });
            this.MainMenuStrip = menuBar;

            // 2. Action Toolbar (Compact 44px)
            BuildWinRarToolbar();

            // 3. TabControl Navigation
            tabControl = new TabControlEx
            {
                Dock = DockStyle.Fill,
                TabStyle = TabStyle.Pill,
                TabHeight = 36,
                TabWidth = 260
            };

            tabExplorer = tabControl.AddTab(LocalizationService.Get("Tab_Explorer"), "");
            tabStudio = tabControl.AddTab(LocalizationService.Get("Tab_Studio"), "");

            BuildArchiveExplorerTab(tabExplorer);
            BuildStudioTab(tabStudio);

            // Add controls in reverse dock order
            this.Controls.Add(tabControl);
            this.Controls.Add(pnlWinRarToolbar);
            this.Controls.Add(menuBar);
        }

        private void BuildWinRarToolbar()
        {
            pnlWinRarToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(8, 4, 8, 4),
                BackColor = ZeroTheme.Colors.Background
            };

            int x = 8;
            int btnHeight = 32;
            int gap = 8;

            tbBtnAdd = new SimpleButton
            {
                Text = LocalizationService.Get("Btn_Add"),
                ButtonStyle = ZeroButtonStyle.Primary,
                Location = new Point(x, 6),
                Size = new Size(110, btnHeight)
            };
            tbBtnAdd.Click += (s, e) => tabControl.SelectedIndex = 1;
            pnlWinRarToolbar.Controls.Add(tbBtnAdd);
            x += 110 + gap;

            tbBtnOpen = new SimpleButton
            {
                Text = LocalizationService.Get("Btn_Open"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(x, 6),
                Size = new Size(110, btnHeight)
            };
            tbBtnOpen.Click += (s, e) => BrowseAndOpenArchive();
            pnlWinRarToolbar.Controls.Add(tbBtnOpen);
            x += 110 + gap;

            tbBtnExtract = new SimpleButton
            {
                Text = LocalizationService.Get("Btn_Extract"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(x, 6),
                Size = new Size(115, btnHeight)
            };
            tbBtnExtract.Click += TbBtnExtract_Click;
            pnlWinRarToolbar.Controls.Add(tbBtnExtract);
            x += 115 + gap;

            tbBtnTest = new SimpleButton
            {
                Text = LocalizationService.Get("Btn_Test"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(x, 6),
                Size = new Size(105, btnHeight)
            };
            tbBtnTest.Click += TbBtnTest_Click;
            pnlWinRarToolbar.Controls.Add(tbBtnTest);
        }

        #region Tab 1: WinRAR Archive Explorer

        private void BuildArchiveExplorerTab(TabPageEx page)
        {
            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 6)
            };
            page.Controls.Add(pnlMain);

            // Path & Breadcrumb Bar
            var pnlPath = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(0, 2, 0, 4)
            };

            btnUpLevel = new SimpleButton
            {
                Text = LocalizationService.Get("Path_Up"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Dock = DockStyle.Left,
                Width = 72
            };
            btnUpLevel.Click += (s, e) =>
            {
                if (_archiveExplorer.NavigateUp())
                {
                    RefreshFileListView();
                }
            };

            btnRefresh = new SimpleButton
            {
                Text = LocalizationService.Get("Path_Refresh"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Dock = DockStyle.Right,
                Width = 85
            };
            btnRefresh.Click += (s, e) => RefreshFileListView();

            var pnlPathCenter = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(6, 0, 6, 0)
            };

            txtCurrentPath = new ButtonEdit
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Text = "\\"
            };
            pnlPathCenter.Controls.Add(txtCurrentPath);

            pnlPath.Controls.Add(pnlPathCenter);
            pnlPath.Controls.Add(btnRefresh);
            pnlPath.Controls.Add(btnUpLevel);
            pnlPathCenter.BringToFront();

            pnlMain.Controls.Add(pnlPath);

            // Status Bar
            statusStripArchive = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextSecondary
            };

            lblStatusItems = new ToolStripStatusLabel(LocalizationService.Get("Status_NoArchive")) { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            lblStatusSelection = new ToolStripStatusLabel(LocalizationService.Get("Status_ZeroSelected")) { AutoSize = true };
            lblStatusFormat = new ToolStripStatusLabel("") { AutoSize = true };

            statusStripArchive.Items.AddRange(new ToolStripItem[] { lblStatusItems, lblStatusSelection, lblStatusFormat });
            pnlMain.Controls.Add(statusStripArchive);

            // File ListView (WinRAR / 7-Zip Details View)
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

            colName = new ColumnHeader { Text = LocalizationService.Get("Col_Name"), Width = 280 };
            colOrigSize = new ColumnHeader { Text = LocalizationService.Get("Col_OriginalSize"), Width = 110, TextAlign = HorizontalAlignment.Right };
            colPackedSize = new ColumnHeader { Text = LocalizationService.Get("Col_PackedSize"), Width = 110, TextAlign = HorizontalAlignment.Right };
            colRatio = new ColumnHeader { Text = LocalizationService.Get("Col_Ratio"), Width = 75, TextAlign = HorizontalAlignment.Right };
            colMethod = new ColumnHeader { Text = LocalizationService.Get("Col_Method"), Width = 100, TextAlign = HorizontalAlignment.Center };
            colModified = new ColumnHeader { Text = LocalizationService.Get("Col_Modified"), Width = 135 };
            colCrc = new ColumnHeader { Text = LocalizationService.Get("Col_CRC"), Width = 85, TextAlign = HorizontalAlignment.Center };
            colType = new ColumnHeader { Text = LocalizationService.Get("Col_Type"), Width = 95 };
            colAttr = new ColumnHeader { Text = LocalizationService.Get("Col_Attributes"), Width = 75, TextAlign = HorizontalAlignment.Center };

            lvArchiveFiles.Columns.AddRange(new ColumnHeader[]
            {
                colName, colOrigSize, colPackedSize, colRatio, colMethod, colModified, colCrc, colType, colAttr
            });

            _columnSorter = new ListViewColumnSorter();
            lvArchiveFiles.ListViewItemSorter = _columnSorter;
            lvArchiveFiles.ColumnClick += LvArchiveFiles_ColumnClick;

            lvArchiveFiles.DoubleClick += LvArchiveFiles_DoubleClick;
            lvArchiveFiles.SelectedIndexChanged += LvArchiveFiles_SelectedIndexChanged;

            // Handle Right-Click to select item under mouse (Critical WinForms fix)
            lvArchiveFiles.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var hitItem = lvArchiveFiles.GetItemAt(e.X, e.Y);
                    if (hitItem != null)
                    {
                        if (!hitItem.Selected)
                        {
                            lvArchiveFiles.SelectedItems.Clear();
                            hitItem.Selected = true;
                            hitItem.Focused = true;
                        }
                    }
                    else
                    {
                        lvArchiveFiles.SelectedItems.Clear();
                    }
                }
            };

            // Keyboard navigation (WinRAR style)
            lvArchiveFiles.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    if (lvArchiveFiles.SelectedItems.Count > 0)
                    {
                        LvArchiveFiles_DoubleClick(lvArchiveFiles, EventArgs.Empty);
                    }
                }
                else if (e.KeyCode == Keys.Back)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    if (_archiveExplorer.NavigateUp())
                    {
                        RefreshFileListView();
                    }
                }
                else if (e.KeyCode == Keys.F5)
                {
                    e.Handled = true;
                    RefreshFileListView();
                }
                else if (e.Control && e.KeyCode == Keys.A)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    foreach (ListViewItem it in lvArchiveFiles.Items) it.Selected = true;
                }
            };

            // In-app Context Menu
            BuildContextMenu();
            lvArchiveFiles.ContextMenuStrip = contextMenuFiles;

            pnlMain.Controls.Add(lvArchiveFiles);
            lvArchiveFiles.BringToFront(); // Crucial: gives lvArchiveFiles proper client bounds under pnlPath!
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

            itemCtxOpen = new ToolStripMenuItem(LocalizationService.Get("Menu_ViewFile"), null, (s, e) => OpenSelectedFile());
            itemCtxExtract = new ToolStripMenuItem(LocalizationService.Get("Menu_ExtractTo"), null, (s, e) => ExtractSelectedFiles());
            itemCtxCopy = new ToolStripMenuItem(LocalizationService.Get("Ctx_CopyPath"), null, (s, e) => CopySelectedPath());
            itemCtxInfo = new ToolStripMenuItem(LocalizationService.Get("Ctx_Properties"), null, (s, e) => ShowSelectedFileInfo());

            var sep1 = new ToolStripSeparator();
            itemCtxOpenAnother = new ToolStripMenuItem(LocalizationService.Get("Menu_OpenArchive"), null, (s, e) => BrowseAndOpenArchive());
            itemCtxTest = new ToolStripMenuItem(LocalizationService.Get("Menu_TestArchive"), null, (s, e) => TbBtnTest_Click(s, e));
            itemCtxRefresh = new ToolStripMenuItem(LocalizationService.Get("Path_Refresh") + " (F5)", null, (s, e) => RefreshFileListView());
            itemCtxSelectAll = new ToolStripMenuItem(LocalizationService.Get("Menu_SelectAll"), null, (s, e) =>
            {
                foreach (ListViewItem it in lvArchiveFiles.Items) it.Selected = true;
            });

            contextMenuFiles.Items.AddRange(new ToolStripItem[]
            {
                itemCtxOpen,
                itemCtxExtract,
                new ToolStripSeparator(),
                itemCtxCopy,
                itemCtxInfo,
                sep1,
                itemCtxOpenAnother,
                itemCtxTest,
                itemCtxRefresh,
                itemCtxSelectAll
            });

            contextMenuFiles.Opening += (s, e) =>
            {
                bool hasSelection = lvArchiveFiles.SelectedItems.Count > 0;
                bool hasArchive = !string.IsNullOrEmpty(_archiveExplorer.CurrentArchivePath);

                var firstSel = hasSelection ? lvArchiveFiles.SelectedItems[0].Tag as VirtualItem : null;
                bool isDir = firstSel != null && firstSel.IsDirectory;

                itemCtxOpen.Visible = hasSelection;
                itemCtxOpen.Text = isDir ? LocalizationService.Get("Ctx_OpenFolder") : LocalizationService.Get("Menu_ViewFile");
                itemCtxExtract.Visible = hasSelection;
                itemCtxCopy.Visible = hasSelection;
                itemCtxInfo.Visible = hasSelection;
                sep1.Visible = hasSelection;

                itemCtxTest.Enabled = hasArchive;
                itemCtxRefresh.Enabled = hasArchive;
            };
        }

        private async void BrowseAndOpenArchive()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Gói nén ZeroZip (*.zz;*.ztar;*.exe)|*.zz;*.ztar;*.exe|Kho lưu trữ .zz (*.zz)|*.zz|Executable SFX (*.exe)|*.exe|Tệp nén ZIP (*.zip)|*.zip|Tất cả tệp (*.*)|*.*",
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

            long origTotal = _archiveExplorer.CurrentFooter?.OriginalSize ?? 0;
            long payloadTotal = _archiveExplorer.CurrentFooter?.PayloadSize ?? 0;
            string methodText = _archiveExplorer.CurrentFooter?.Method.ToString() ?? "Zstd";
            string crcText = _archiveExplorer.CurrentFooter != null ? $"{_archiveExplorer.CurrentFooter.Crc32:X8}" : "—";

            foreach (var item in items)
            {
                var lvi = new ListViewItem(item.Name, item.IsDirectory ? "folder" : "file")
                {
                    Tag = item
                };

                if (item.IsDirectory)
                {
                    lvi.SubItems.Add("");                                                                   // 1. Original Size
                    lvi.SubItems.Add("");                                                                   // 2. Packed Size
                    lvi.SubItems.Add("");                                                                   // 3. Ratio
                    lvi.SubItems.Add("");                                                                   // 4. Method
                    lvi.SubItems.Add(item.ModificationTime == default ? "" : item.ModificationTime.LocalDateTime.ToString("dd/MM/yyyy HH:mm")); // 5. Modified
                    lvi.SubItems.Add("");                                                                   // 6. CRC32
                    lvi.SubItems.Add(LocalizationService.Get("Type_Folder"));                               // 7. Type
                    lvi.SubItems.Add("D----");                                                              // 8. Attributes
                }
                else
                {
                    lvi.SubItems.Add(FormatSize(item.Size));                                                // 1. Original Size

                    if (origTotal > 0 && payloadTotal > 0)
                    {
                        long packedEst = (long)Math.Round((double)item.Size * payloadTotal / origTotal);
                        lvi.SubItems.Add(FormatSize(packedEst));                                            // 2. Packed Size
                        double ratio = (double)payloadTotal / origTotal;
                        lvi.SubItems.Add(ratio.ToString("P0"));                                             // 3. Ratio
                    }
                    else
                    {
                        lvi.SubItems.Add(FormatSize(item.Size));
                        lvi.SubItems.Add("—");
                    }

                    lvi.SubItems.Add(methodText);                                                           // 4. Method
                    lvi.SubItems.Add(item.ModificationTime == default ? "" : item.ModificationTime.LocalDateTime.ToString("dd/MM/yyyy HH:mm")); // 5. Modified
                    lvi.SubItems.Add(crcText);                                                              // 6. CRC32

                    string ext = Path.GetExtension(item.Name).TrimStart('.').ToUpperInvariant();
                    lvi.SubItems.Add(string.IsNullOrEmpty(ext) ? LocalizationService.Get("Type_File") : $"{ext} File"); // 7. Type
                    lvi.SubItems.Add("----A");                                                              // 8. Attributes
                }

                lvArchiveFiles.Items.Add(lvi);
            }

            lvArchiveFiles.EndUpdate();

            int dirCount = items.Count(i => i.IsDirectory);
            int fileCount = items.Count(i => !i.IsDirectory);
            long totalSize = items.Where(i => !i.IsDirectory).Sum(i => i.Size);
            lblStatusItems.Text = LocalizationService.Get("Status_ItemsSummary", fileCount, dirCount, FormatSize(totalSize));
            lblStatusSelection.Text = LocalizationService.Get("Status_ZeroSelected");
        }

        private void LvArchiveFiles_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (lvArchiveFiles.SelectedItems.Count == 0)
            {
                lblStatusSelection.Text = LocalizationService.Get("Status_ZeroSelected");
                return;
            }

            long selSize = 0;
            int count = lvArchiveFiles.SelectedItems.Count;
            foreach (ListViewItem item in lvArchiveFiles.SelectedItems)
            {
                if (item.Tag is VirtualItem v && !v.IsDirectory)
                    selSize += v.Size;
            }

            lblStatusSelection.Text = LocalizationService.Get("Status_SelectedItems", count, FormatSize(selSize));
        }

        private void CloseArchive()
        {
            _archiveExplorer.CloseArchive();
            lvArchiveFiles.Items.Clear();
            txtCurrentPath.Text = "\\";
            lblStatusItems.Text = LocalizationService.Get("Status_NoArchive");
            lblStatusSelection.Text = LocalizationService.Get("Status_ZeroSelected");
            lblStatusFormat.Text = "";
        }

        private void LvArchiveFiles_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (e.Column == _columnSorter.SortColumn)
            {
                _columnSorter.Order = _columnSorter.Order == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                _columnSorter.SortColumn = e.Column;
                _columnSorter.Order = SortOrder.Ascending;
            }
            lvArchiveFiles.Sort();
        }

        private void OnLanguageChanged()
        {
            if (this.IsDisposed) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(ApplyLocalization));
            }
            else
            {
                ApplyLocalization();
            }
        }

        private void ApplyLocalization()
        {
            this.Text = LocalizationService.Get("App_Title");

            // Menu Bar
            if (menuFile != null) menuFile.Text = LocalizationService.Get("Menu_File");
            if (itemOpenArchive != null) itemOpenArchive.Text = LocalizationService.Get("Menu_OpenArchive");
            if (itemNewArchive != null) itemNewArchive.Text = LocalizationService.Get("Menu_NewArchive");
            if (itemCloseArchive != null) itemCloseArchive.Text = LocalizationService.Get("Menu_CloseArchive");
            if (itemExit != null) itemExit.Text = LocalizationService.Get("Menu_Exit");

            if (menuCommands != null) menuCommands.Text = LocalizationService.Get("Menu_Commands");
            if (itemAddFiles != null) itemAddFiles.Text = LocalizationService.Get("Menu_AddFiles");
            if (itemExtractTo != null) itemExtractTo.Text = LocalizationService.Get("Menu_ExtractTo");
            if (itemTestArchive != null) itemTestArchive.Text = LocalizationService.Get("Menu_TestArchive");
            if (itemViewFile != null) itemViewFile.Text = LocalizationService.Get("Menu_ViewFile");
            if (itemSelectAll != null) itemSelectAll.Text = LocalizationService.Get("Menu_SelectAll");

            if (menuTools != null) menuTools.Text = LocalizationService.Get("Menu_Tools");
            if (itemEstimate != null) itemEstimate.Text = LocalizationService.Get("Menu_Estimate");
            if (itemShellIntegrate != null) itemShellIntegrate.Text = LocalizationService.Get("Menu_ShellIntegrate");

            if (menuOptions != null) menuOptions.Text = LocalizationService.Get("Menu_Options");
            if (itemToggleTheme != null) itemToggleTheme.Text = LocalizationService.Get("Menu_ToggleTheme");

            if (menuLanguage != null) menuLanguage.Text = LocalizationService.Get("Menu_Language");
            if (itemLangVi != null) itemLangVi.Checked = LocalizationService.CurrentLanguage == AppLanguage.Vietnamese;
            if (itemLangEn != null) itemLangEn.Checked = LocalizationService.CurrentLanguage == AppLanguage.English;

            if (menuHelp != null) menuHelp.Text = LocalizationService.Get("Menu_Help");
            if (itemAbout != null) itemAbout.Text = LocalizationService.Get("Menu_About");

            // Action Toolbar (Quick Actions)
            if (tbBtnAdd != null) tbBtnAdd.Text = LocalizationService.Get("Btn_Add");
            if (tbBtnOpen != null) tbBtnOpen.Text = LocalizationService.Get("Btn_Open");
            if (tbBtnExtract != null) tbBtnExtract.Text = LocalizationService.Get("Btn_Extract");
            if (tbBtnTest != null) tbBtnTest.Text = LocalizationService.Get("Btn_Test");

            // Tabs
            if (tabExplorer != null) tabExplorer.Title = LocalizationService.Get("Tab_Explorer");
            if (tabStudio != null) tabStudio.Title = LocalizationService.Get("Tab_Studio");
            if (tabControl != null) tabControl.Invalidate();

            // Path & Navigation Bar
            if (btnUpLevel != null) btnUpLevel.Text = LocalizationService.Get("Path_Up");
            if (btnRefresh != null) btnRefresh.Text = LocalizationService.Get("Path_Refresh");

            // Grid Columns
            if (colName != null) colName.Text = LocalizationService.Get("Col_Name");
            if (colOrigSize != null) colOrigSize.Text = LocalizationService.Get("Col_OriginalSize");
            if (colPackedSize != null) colPackedSize.Text = LocalizationService.Get("Col_PackedSize");
            if (colRatio != null) colRatio.Text = LocalizationService.Get("Col_Ratio");
            if (colMethod != null) colMethod.Text = LocalizationService.Get("Col_Method");
            if (colModified != null) colModified.Text = LocalizationService.Get("Col_Modified");
            if (colCrc != null) colCrc.Text = LocalizationService.Get("Col_CRC");
            if (colType != null) colType.Text = LocalizationService.Get("Col_Type");
            if (colAttr != null) colAttr.Text = LocalizationService.Get("Col_Attributes");

            // Context Menu Items
            if (itemCtxOpen != null) itemCtxOpen.Text = LocalizationService.Get("Menu_ViewFile");
            if (itemCtxExtract != null) itemCtxExtract.Text = LocalizationService.Get("Menu_ExtractTo");
            if (itemCtxCopy != null) itemCtxCopy.Text = LocalizationService.Get("Ctx_CopyPath");
            if (itemCtxInfo != null) itemCtxInfo.Text = LocalizationService.Get("Ctx_Properties");
            if (itemCtxOpenAnother != null) itemCtxOpenAnother.Text = LocalizationService.Get("Menu_OpenArchive");
            if (itemCtxTest != null) itemCtxTest.Text = LocalizationService.Get("Menu_TestArchive");
            if (itemCtxRefresh != null) itemCtxRefresh.Text = LocalizationService.Get("Path_Refresh") + " (F5)";
            if (itemCtxSelectAll != null) itemCtxSelectAll.Text = LocalizationService.Get("Menu_SelectAll");

            // Studio Cards
            if (cardSource != null)
            {
                cardSource.Title = LocalizationService.Get("Studio_Card1_Title");
                cardSource.Subtitle = LocalizationService.Get("Studio_Card1_Sub");
            }
            if (lblSrc != null) lblSrc.Text = LocalizationService.Get("Studio_LblSource");
            if (btnBrowseSourceFolder != null) btnBrowseSourceFolder.Text = LocalizationService.Get("Studio_BtnBrowseFolder");
            if (btnBrowseSourceFile != null) btnBrowseSourceFile.Text = LocalizationService.Get("Studio_BtnBrowseFile");
            if (lblDst != null) lblDst.Text = LocalizationService.Get("Studio_LblDestSave");
            if (btnBrowseDest != null) btnBrowseDest.Text = LocalizationService.Get("Studio_BtnBrowseDest");

            if (cardEngine != null)
            {
                cardEngine.Title = LocalizationService.Get("Studio_Card2_Title");
                cardEngine.Subtitle = LocalizationService.Get("Studio_Card2_Sub");
            }
            if (lblMethod != null) lblMethod.Text = LocalizationService.Get("Studio_LblMethod");
            if (lblProfile != null) lblProfile.Text = LocalizationService.Get("Studio_LblProfile");
            if (lblSplit != null) lblSplit.Text = LocalizationService.Get("Studio_LblSplit");

            if (cardSecurity != null)
            {
                cardSecurity.Title = LocalizationService.Get("Studio_Card3_Title");
                cardSecurity.Subtitle = LocalizationService.Get("Studio_Card3_Sub");
            }
            if (lblPass != null) lblPass.Text = LocalizationService.Get("Studio_LblPassword");

            if (cardExec != null)
            {
                cardExec.Title = LocalizationService.Get("Studio_Card4_Title");
                cardExec.Subtitle = LocalizationService.Get("Studio_Card4_Sub");
            }
            if (btnCompress != null) btnCompress.Text = LocalizationService.Get("Studio_BtnCompress");
            if (btnEstimate != null) btnEstimate.Text = LocalizationService.Get("Studio_BtnEstimate");
            if (btnCancel != null) btnCancel.Text = LocalizationService.Get("Studio_BtnCancel");

            // Refresh file list if loaded
            if (lvArchiveFiles != null && lvArchiveFiles.Items.Count > 0)
            {
                RefreshFileListView();
            }
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
                    ShowToast("Đã gỡ bỏ ZeroZip khỏi menu chuột phải Windows Explorer.", "Menu ngữ cảnh", ToastType.Info);
                }
            }
            else
            {
                if (ShellContextMenuService.Register())
                {
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
            if (menuBar != null)
            {
                menuBar.BackColor = ZeroTheme.Colors.Surface;
                menuBar.ForeColor = ZeroTheme.Colors.TextPrimary;
            }
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
            cardSource = new Card
            {
                Title = LocalizationService.Get("Studio_Card1_Title"),
                Subtitle = LocalizationService.Get("Studio_Card1_Sub"),
                StepNumber = 1,
                Size = new Size(cardWidth, 195),
                Location = new Point(10, currentY),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblSrc = new Label { Text = LocalizationService.Get("Studio_LblSource"), Location = new Point(14, 6), AutoSize = true };
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
                Text = LocalizationService.Get("Studio_BtnBrowseFolder"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(604, 26),
                Size = new Size(115, 32)
            };
            btnBrowseSourceFolder.Click += (s, e) => BrowseSourceFolder();
            cardSource.ContentPanel.Controls.Add(btnBrowseSourceFolder);

            btnBrowseSourceFile = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnBrowseFile"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(727, 26),
                Size = new Size(115, 32)
            };
            btnBrowseSourceFile.Click += (s, e) => BrowseSourceFile();
            cardSource.ContentPanel.Controls.Add(btnBrowseSourceFile);

            lblDst = new Label { Text = LocalizationService.Get("Studio_LblDestSave"), Location = new Point(14, 66), AutoSize = true };
            cardSource.ContentPanel.Controls.Add(lblDst);

            txtDest = new ButtonEdit
            {
                Location = new Point(14, 86),
                Size = new Size(705, 32),
                PlaceholderText = "Đường dẫn tệp đầu ra (.zz, .zip hoặc .exe SFX)..."
            };
            txtDest.ButtonClick += (s, e) => BrowseDest();
            cardSource.ContentPanel.Controls.Add(txtDest);

            btnBrowseDest = new SimpleButton
            {
                Text = LocalizationService.Get("Studio_BtnBrowseDest"),
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(727, 86),
                Size = new Size(115, 32)
            };
            btnBrowseDest.Click += (s, e) => BrowseDest();
            cardSource.ContentPanel.Controls.Add(btnBrowseDest);

            pnlContainer.Controls.Add(cardSource);
            currentY += 203;

            // --- Card 2: Thuật toán & Động cơ nén ZeroUniverse ---
            cardEngine = new Card
            {
                Title = LocalizationService.Get("Studio_Card2_Title"),
                Subtitle = LocalizationService.Get("Studio_Card2_Sub"),
                StepNumber = 2,
                Size = new Size(cardWidth, 175),
                Location = new Point(10, currentY),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblMethod = new Label { Text = LocalizationService.Get("Studio_LblMethod"), Location = new Point(14, 6), AutoSize = true };
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

            lblProfile = new Label { Text = LocalizationService.Get("Studio_LblProfile"), Location = new Point(300, 6), AutoSize = true };
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

            lblSplit = new Label { Text = LocalizationService.Get("Studio_LblSplit"), Location = new Point(500, 6), AutoSize = true };
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
            cardSecurity = new Card
            {
                Title = LocalizationService.Get("Studio_Card3_Title"),
                Subtitle = LocalizationService.Get("Studio_Card3_Sub"),
                StepNumber = 3,
                Size = new Size(cardWidth, 125),
                Location = new Point(10, currentY),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblPass = new Label { Text = LocalizationService.Get("Studio_LblPassword"), Location = new Point(14, 6), AutoSize = true };
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

            lblCryptoNotice = new Label
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
            cardExec = new Card
            {
                Title = LocalizationService.Get("Studio_Card4_Title"),
                Subtitle = LocalizationService.Get("Studio_Card4_Sub"),
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
            if (ext == ".exe" || ext == ".zz" || ext == ".ztar")
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
