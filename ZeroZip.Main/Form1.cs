using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroCompression.Core;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Data;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Theme;
using ZeroZip.Core;
using ZeroZip.Main.Dialogs;
using ZeroZip.Main.Services;
using ModalDialog = ZeroUI.WinForms.Overlays.ModalDialog;
using ToastType = ZeroUI.WinForms.Overlays.ToastType;

namespace ZeroZip.Main
{
    public class Form1 : BaseForm
    {
        // Initial launch parameters
        private readonly string? _initialArchivePath;
        private readonly string? _initialSourcePath;

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
        private ToolStripMenuItem menuHelp = null!;
        private ToolStripMenuItem itemAbout = null!;

        // Action Toolbar (Streamlined 4 Quick Actions)
        private Panel pnlWinRarToolbar = null!;
        private SimpleButton tbBtnAdd = null!;
        private SimpleButton tbBtnOpen = null!;
        private SimpleButton tbBtnExtract = null!;
        private SimpleButton tbBtnTest = null!;

        // --- WinRAR Archive Explorer Main View ---
        private Panel pnlArchiveMain = null!;
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

        public Form1(string? initialArchivePath = null, string? initialSourcePath = null, bool openStudio = false)
        {
            _initialArchivePath = initialArchivePath;
            _initialSourcePath = initialSourcePath;

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
                OpenAddArchiveDialog(_initialSourcePath);
            }
            else if (!string.IsNullOrEmpty(_initialArchivePath) && File.Exists(_initialArchivePath))
            {
                await LoadArchiveAsync(_initialArchivePath);
            }
        }

        private async void OpenAddArchiveDialog(string? initialSource = null)
        {
            using var dlg = new AddArchiveDialog(initialSource);
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.ResultArchivePath))
            {
                if (File.Exists(dlg.ResultArchivePath))
                {
                    await LoadArchiveAsync(dlg.ResultArchivePath);
                }
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
            itemNewArchive = new ToolStripMenuItem(LocalizationService.Get("Menu_NewArchive"), null, (s, e) => OpenAddArchiveDialog(), Keys.Control | Keys.N);
            itemCloseArchive = new ToolStripMenuItem(LocalizationService.Get("Menu_CloseArchive"), null, (s, e) => CloseArchive());
            itemExit = new ToolStripMenuItem(LocalizationService.Get("Menu_Exit"), null, (s, e) => this.Close(), Keys.Alt | Keys.F4);
            menuFile.DropDownItems.AddRange(new ToolStripItem[] { itemOpenArchive, itemNewArchive, itemCloseArchive, new ToolStripSeparator(), itemExit });

            // Menu: Commands (Lệnh)
            menuCommands = new ToolStripMenuItem(LocalizationService.Get("Menu_Commands"));
            itemAddFiles = new ToolStripMenuItem(LocalizationService.Get("Menu_AddFiles"), null, (s, e) => OpenAddArchiveDialog());
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
            itemEstimate = new ToolStripMenuItem(LocalizationService.Get("Menu_Estimate"), null, (s, e) => OpenAddArchiveDialog());
            itemShellIntegrate = new ToolStripMenuItem(LocalizationService.Get("Menu_ShellIntegrate"), null, (s, e) => TbBtnContextMenu_Click(s, e));
            menuTools.DropDownItems.AddRange(new ToolStripItem[] { itemEstimate, itemShellIntegrate });

            // Menu: Options (Tùy chọn)
            menuOptions = new ToolStripMenuItem(LocalizationService.Get("Menu_Options"));
            itemToggleTheme = new ToolStripMenuItem(LocalizationService.Get("Menu_ToggleTheme"), null, (s, e) => BtnToggleTheme_Click(s, e), Keys.Control | Keys.T);
            menuOptions.DropDownItems.AddRange(new ToolStripItem[] { itemToggleTheme });

            // Menu: Language (Ngôn ngữ)
            menuLanguage = new ToolStripMenuItem(LocalizationService.Get("Menu_Language"));
            PopulateLanguageMenu();

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

            // 3. Main Archive Explorer View (Dock.Fill)
            BuildArchiveExplorerView();

            // Add controls in reverse dock order
            this.Controls.Add(pnlArchiveMain);
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
            tbBtnAdd.Click += (s, e) => OpenAddArchiveDialog();
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

        private void PopulateLanguageMenu()
        {
            menuLanguage.DropDownItems.Clear();
            string curCode = LocalizationService.CurrentLanguageCode;
            foreach (var lang in LocalizationService.GetAvailableLanguages())
            {
                var item = new ToolStripMenuItem(lang.DisplayName, null, (s, e) =>
                {
                    LocalizationService.SetLanguage(lang.Code);
                })
                {
                    Checked = string.Equals(lang.Code, curCode, StringComparison.OrdinalIgnoreCase),
                    Tag = lang.Code
                };
                menuLanguage.DropDownItems.Add(item);
            }
        }

        #region WinRAR Archive Explorer Main View

        private void BuildArchiveExplorerView()
        {
            pnlArchiveMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 6)
            };

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

            pnlArchiveMain.Controls.Add(pnlPath);

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
            pnlArchiveMain.Controls.Add(statusStripArchive);

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

            pnlArchiveMain.Controls.Add(lvArchiveFiles);
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

            if (menuLanguage != null)
            {
                menuLanguage.Text = LocalizationService.Get("Menu_Language");
                string curCode = LocalizationService.CurrentLanguageCode;
                foreach (ToolStripItem it in menuLanguage.DropDownItems)
                {
                    if (it is ToolStripMenuItem mnu && mnu.Tag is string code)
                    {
                        mnu.Checked = string.Equals(code, curCode, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            if (menuHelp != null) menuHelp.Text = LocalizationService.Get("Menu_Help");
            if (itemAbout != null) itemAbout.Text = LocalizationService.Get("Menu_About");

            // Action Toolbar (Quick Actions)
            if (tbBtnAdd != null) tbBtnAdd.Text = LocalizationService.Get("Btn_Add");
            if (tbBtnOpen != null) tbBtnOpen.Text = LocalizationService.Get("Btn_Open");
            if (tbBtnExtract != null) tbBtnExtract.Text = LocalizationService.Get("Btn_Extract");
            if (tbBtnTest != null) tbBtnTest.Text = LocalizationService.Get("Btn_Test");

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

            // If dropped an archive or SFX, open directly in Archive Explorer
            if (ext == ".exe" || ext == ".zz" || ext == ".ztar" || ext == ".zip")
            {
                await LoadArchiveAsync(path);
                return;
            }

            // Otherwise, prompt user with Add to Archive dialog (like WinRAR)
            OpenAddArchiveDialog(path);
        }

        #endregion

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
