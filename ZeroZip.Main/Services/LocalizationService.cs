using System;
using System.Collections.Generic;

namespace ZeroZip.Main.Services
{
    public enum AppLanguage
    {
        Vietnamese,
        English
    }

    public static class LocalizationService
    {
        public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Vietnamese;

        public static event Action? LanguageChanged;

        public static void SetLanguage(AppLanguage language)
        {
            if (CurrentLanguage == language) return;
            CurrentLanguage = language;
            LanguageChanged?.Invoke();
        }

        public static void ToggleLanguage()
        {
            SetLanguage(CurrentLanguage == AppLanguage.Vietnamese ? AppLanguage.English : AppLanguage.Vietnamese);
        }

        public static string Get(string key, params object[] args)
        {
            if (Dictionary.TryGetValue(key, out var localizedMap))
            {
                if (localizedMap.TryGetValue(CurrentLanguage, out var text))
                {
                    return args.Length > 0 ? string.Format(text, args) : text;
                }
            }
            return key;
        }

        private static readonly Dictionary<string, Dictionary<AppLanguage, string>> Dictionary = new()
        {
            // Window & Brand
            ["App_Title"] = new()
            {
                [AppLanguage.Vietnamese] = "ZeroZip — Sovereign Ultra-Compression Studio & Archive Explorer",
                [AppLanguage.English] = "ZeroZip — Sovereign Ultra-Compression Studio & Archive Explorer"
            },
            ["Brand_Title"] = new()
            {
                [AppLanguage.Vietnamese] = "⚡ ZEROZIP ARCHIVER",
                [AppLanguage.English] = "⚡ ZEROZIP ARCHIVER"
            },
            ["Brand_Subtitle"] = new()
            {
                [AppLanguage.Vietnamese] = "Bộ Công Cụ Nén Siêu Tốc (Zstd/LZMA/Brotli) Chuẩn ZeroUniverse",
                [AppLanguage.English] = "High-Performance Sovereign Archiver (Zstd/LZMA/Brotli) — ZeroUniverse"
            },

            // Menu Bar - File
            ["Menu_File"] = new()
            {
                [AppLanguage.Vietnamese] = "Tập tin",
                [AppLanguage.English] = "File"
            },
            ["Menu_OpenArchive"] = new()
            {
                [AppLanguage.Vietnamese] = "Mở gói nén... (Ctrl+O)",
                [AppLanguage.English] = "Open Archive... (Ctrl+O)"
            },
            ["Menu_NewArchive"] = new()
            {
                [AppLanguage.Vietnamese] = "Tạo gói nén mới (Studio)... (Ctrl+N)",
                [AppLanguage.English] = "New Archive (Studio)... (Ctrl+N)"
            },
            ["Menu_CloseArchive"] = new()
            {
                [AppLanguage.Vietnamese] = "Đóng gói hiện tại",
                [AppLanguage.English] = "Close Archive"
            },
            ["Menu_Exit"] = new()
            {
                [AppLanguage.Vietnamese] = "Thoát (Alt+F4)",
                [AppLanguage.English] = "Exit (Alt+F4)"
            },

            // Menu Bar - Commands
            ["Menu_Commands"] = new()
            {
                [AppLanguage.Vietnamese] = "Lệnh",
                [AppLanguage.English] = "Commands"
            },
            ["Menu_AddFiles"] = new()
            {
                [AppLanguage.Vietnamese] = "Thêm tệp vào kho nén...",
                [AppLanguage.English] = "Add Files to Archive..."
            },
            ["Menu_ExtractTo"] = new()
            {
                [AppLanguage.Vietnamese] = "Giải nén vào thư mục...",
                [AppLanguage.English] = "Extract to Specified Folder..."
            },
            ["Menu_TestArchive"] = new()
            {
                [AppLanguage.Vietnamese] = "Kiểm tra tính toàn vẹn gói nén",
                [AppLanguage.English] = "Test Archive Integrity"
            },
            ["Menu_ViewFile"] = new()
            {
                [AppLanguage.Vietnamese] = "Xem tệp (Enter / F3)",
                [AppLanguage.English] = "View File (Enter / F3)"
            },
            ["Menu_SelectAll"] = new()
            {
                [AppLanguage.Vietnamese] = "Chọn tất cả (Ctrl+A)",
                [AppLanguage.English] = "Select All (Ctrl+A)"
            },

            // Menu Bar - Tools
            ["Menu_Tools"] = new()
            {
                [AppLanguage.Vietnamese] = "Công cụ",
                [AppLanguage.English] = "Tools"
            },
            ["Menu_Estimate"] = new()
            {
                [AppLanguage.Vietnamese] = "Đánh giá tỉ lệ nén nhanh...",
                [AppLanguage.English] = "Estimate Compression Ratio..."
            },
            ["Menu_ShellIntegrate"] = new()
            {
                [AppLanguage.Vietnamese] = "Cài đặt Menu chuột phải Windows Explorer",
                [AppLanguage.English] = "Windows Explorer Shell Integration"
            },

            // Menu Bar - Options
            ["Menu_Options"] = new()
            {
                [AppLanguage.Vietnamese] = "Tùy chọn",
                [AppLanguage.English] = "Options"
            },
            ["Menu_ToggleTheme"] = new()
            {
                [AppLanguage.Vietnamese] = "Chuyển giao diện Sáng / Tối (Ctrl+T)",
                [AppLanguage.English] = "Toggle Light / Dark Theme (Ctrl+T)"
            },

            // Menu Bar - Language
            ["Menu_Language"] = new()
            {
                [AppLanguage.Vietnamese] = "Ngôn ngữ (Language)",
                [AppLanguage.English] = "Language"
            },
            ["Menu_Lang_Vi"] = new()
            {
                [AppLanguage.Vietnamese] = "🇻🇳 Tiếng Việt",
                [AppLanguage.English] = "🇻🇳 Vietnamese (Tiếng Việt)"
            },
            ["Menu_Lang_En"] = new()
            {
                [AppLanguage.Vietnamese] = "🇺🇸 English",
                [AppLanguage.English] = "🇺🇸 English"
            },

            // Menu Bar - Help
            ["Menu_Help"] = new()
            {
                [AppLanguage.Vietnamese] = "Trợ giúp",
                [AppLanguage.English] = "Help"
            },
            ["Menu_About"] = new()
            {
                [AppLanguage.Vietnamese] = "Giới thiệu ZeroZip",
                [AppLanguage.English] = "About ZeroZip"
            },

            // Action Toolbar
            ["Btn_Add"] = new()
            {
                [AppLanguage.Vietnamese] = "➕ Thêm...",
                [AppLanguage.English] = "➕ Add..."
            },
            ["Btn_Open"] = new()
            {
                [AppLanguage.Vietnamese] = "📂 Mở gói...",
                [AppLanguage.English] = "📂 Open..."
            },
            ["Btn_Extract"] = new()
            {
                [AppLanguage.Vietnamese] = "📥 Giải nén...",
                [AppLanguage.English] = "📥 Extract..."
            },
            ["Btn_Test"] = new()
            {
                [AppLanguage.Vietnamese] = "🧪 Kiểm tra",
                [AppLanguage.English] = "🧪 Test"
            },
            ["Btn_View"] = new()
            {
                [AppLanguage.Vietnamese] = "👁️ Xem tệp",
                [AppLanguage.English] = "👁️ View"
            },
            ["Btn_Info"] = new()
            {
                [AppLanguage.Vietnamese] = "ℹ️ Thông tin",
                [AppLanguage.English] = "ℹ️ Info"
            },
            ["Btn_Shell_On"] = new()
            {
                [AppLanguage.Vietnamese] = "✓ Menu Explorer",
                [AppLanguage.English] = "✓ Shell Menu"
            },
            ["Btn_Shell_Off"] = new()
            {
                [AppLanguage.Vietnamese] = "⚙️ Bật Menu",
                [AppLanguage.English] = "⚙️ Enable Shell"
            },
            ["Btn_Lang_Toggle"] = new()
            {
                [AppLanguage.Vietnamese] = "🌐 EN",
                [AppLanguage.English] = "🌐 VI"
            },
            ["Btn_Theme_Light"] = new()
            {
                [AppLanguage.Vietnamese] = "☀️ Sáng",
                [AppLanguage.English] = "☀️ Light"
            },
            ["Btn_Theme_Dark"] = new()
            {
                [AppLanguage.Vietnamese] = "🌙 Tối",
                [AppLanguage.English] = "🌙 Dark"
            },

            // Tabs
            ["Tab_Explorer"] = new()
            {
                [AppLanguage.Vietnamese] = "📁 Trình Duyệt Gói Nén (Explorer)",
                [AppLanguage.English] = "📁 Archive Explorer"
            },
            ["Tab_Studio"] = new()
            {
                [AppLanguage.Vietnamese] = "⚡ Siêu Nén & Đóng Gói (Studio)",
                [AppLanguage.English] = "⚡ Compression Studio"
            },

            // Path & Navigation Bar
            ["Path_Up"] = new()
            {
                [AppLanguage.Vietnamese] = "⬆️ Lên",
                [AppLanguage.English] = "⬆️ Up"
            },
            ["Path_Refresh"] = new()
            {
                [AppLanguage.Vietnamese] = "🔄 Làm mới",
                [AppLanguage.English] = "🔄 Refresh"
            },
            ["Path_OpenArchive"] = new()
            {
                [AppLanguage.Vietnamese] = "📂 Mở gói...",
                [AppLanguage.English] = "📂 Open..."
            },

            // Grid Columns
            ["Col_Name"] = new()
            {
                [AppLanguage.Vietnamese] = "Tên",
                [AppLanguage.English] = "Name"
            },
            ["Col_OriginalSize"] = new()
            {
                [AppLanguage.Vietnamese] = "Dung lượng gốc",
                [AppLanguage.English] = "Original Size"
            },
            ["Col_PackedSize"] = new()
            {
                [AppLanguage.Vietnamese] = "Kích thước nén",
                [AppLanguage.English] = "Packed Size"
            },
            ["Col_Ratio"] = new()
            {
                [AppLanguage.Vietnamese] = "Tỷ lệ",
                [AppLanguage.English] = "Ratio"
            },
            ["Col_Method"] = new()
            {
                [AppLanguage.Vietnamese] = "Thuật toán",
                [AppLanguage.English] = "Method"
            },
            ["Col_Modified"] = new()
            {
                [AppLanguage.Vietnamese] = "Ngày sửa đổi",
                [AppLanguage.English] = "Date Modified"
            },
            ["Col_CRC"] = new()
            {
                [AppLanguage.Vietnamese] = "Mã CRC",
                [AppLanguage.English] = "CRC32"
            },
            ["Col_Type"] = new()
            {
                [AppLanguage.Vietnamese] = "Loại tệp",
                [AppLanguage.English] = "Type"
            },
            ["Col_Attributes"] = new()
            {
                [AppLanguage.Vietnamese] = "Thuộc tính",
                [AppLanguage.English] = "Attributes"
            },

            // Grid Cell & Type Texts
            ["Type_Folder"] = new()
            {
                [AppLanguage.Vietnamese] = "Thư mục tệp",
                [AppLanguage.English] = "File folder"
            },
            ["Type_File"] = new()
            {
                [AppLanguage.Vietnamese] = "Tệp tin",
                [AppLanguage.English] = "File"
            },
            ["Folder_Tag"] = new()
            {
                [AppLanguage.Vietnamese] = "<Thư mục>",
                [AppLanguage.English] = "<Folder>"
            },

            // Status Bar
            ["Status_NoArchive"] = new()
            {
                [AppLanguage.Vietnamese] = "Chưa mở gói nén nào.",
                [AppLanguage.English] = "No archive opened."
            },
            ["Status_ItemsSummary"] = new()
            {
                [AppLanguage.Vietnamese] = "{0} tệp, {1} thư mục | Thư mục hiện tại: {2}",
                [AppLanguage.English] = "{0} files, {1} folders | Current folder: {2}"
            },
            ["Status_ZeroSelected"] = new()
            {
                [AppLanguage.Vietnamese] = "0 mục được chọn",
                [AppLanguage.English] = "0 items selected"
            },
            ["Status_SelectedItems"] = new()
            {
                [AppLanguage.Vietnamese] = "Đã chọn {0} mục ({1})",
                [AppLanguage.English] = "{0} items selected ({1})"
            },

            // Studio Tab Texts
            ["Studio_SourceTitle"] = new()
            {
                [AppLanguage.Vietnamese] = "1. Nguồn Dữ Liệu Cần Nén",
                [AppLanguage.English] = "1. Source Data to Compress"
            },
            ["Studio_SourceDesc"] = new()
            {
                [AppLanguage.Vietnamese] = "Chọn một tệp hoặc toàn bộ thư mục để đóng gói siêu nén:",
                [AppLanguage.English] = "Select a file or an entire directory for ultra-compression:"
            },
            ["Studio_BtnBrowseFolder"] = new()
            {
                [AppLanguage.Vietnamese] = "📁 Thư mục...",
                [AppLanguage.English] = "📁 Folder..."
            },
            ["Studio_BtnBrowseFile"] = new()
            {
                [AppLanguage.Vietnamese] = "📄 Tệp...",
                [AppLanguage.English] = "📄 File..."
            },
            ["Studio_ConfigTitle"] = new()
            {
                [AppLanguage.Vietnamese] = "2. Cấu Hình & Thuật Toán Nén",
                [AppLanguage.English] = "2. Compression Configuration"
            },
            ["Studio_LblProfile"] = new()
            {
                [AppLanguage.Vietnamese] = "Chế độ nén:",
                [AppLanguage.English] = "Compression Profile:"
            },
            ["Studio_LblMethod"] = new()
            {
                [AppLanguage.Vietnamese] = "Thuật toán chính:",
                [AppLanguage.English] = "Main Algorithm:"
            },
            ["Studio_LblLevel"] = new()
            {
                [AppLanguage.Vietnamese] = "Mức độ nén (Level):",
                [AppLanguage.English] = "Compression Level:"
            },
            ["Studio_ChkSfx"] = new()
            {
                [AppLanguage.Vietnamese] = "Tạo tệp tự giải nén (.exe SFX)",
                [AppLanguage.English] = "Create Self-Extracting Archive (.exe SFX)"
            },
            ["Studio_ChkPrecomp"] = new()
            {
                [AppLanguage.Vietnamese] = "Bật tiền xử lý dữ liệu (Precomp)",
                [AppLanguage.English] = "Enable Pre-compression (Precomp)"
            },
            ["Studio_OutputTitle"] = new()
            {
                [AppLanguage.Vietnamese] = "3. Đầu Ra & Tùy Chọn Đóng Gói",
                [AppLanguage.English] = "3. Output & Packaging Options"
            },
            ["Studio_LblDest"] = new()
            {
                [AppLanguage.Vietnamese] = "Đường dẫn tệp đích (.zz / .exe / .zip):",
                [AppLanguage.English] = "Destination Archive Path (.zz / .exe / .zip):"
            },
            ["Studio_BtnBrowseDest"] = new()
            {
                [AppLanguage.Vietnamese] = "Lưu thành...",
                [AppLanguage.English] = "Save as..."
            },
            ["Studio_LblPassword"] = new()
            {
                [AppLanguage.Vietnamese] = "Mật khẩu (AES-256-GCM, tùy chọn):",
                [AppLanguage.English] = "Password (AES-256-GCM, optional):"
            },
            ["Studio_LblSplit"] = new()
            {
                [AppLanguage.Vietnamese] = "Chia nhỏ khối lượng:",
                [AppLanguage.English] = "Split Volumes:"
            },
            ["Studio_BtnStart"] = new()
            {
                [AppLanguage.Vietnamese] = "🚀 Bắt Đầu Nén Siêu Tốc",
                [AppLanguage.English] = "🚀 Start Ultra-Compression"
            },
            ["Studio_BtnCancel"] = new()
            {
                [AppLanguage.Vietnamese] = "Dừng",
                [AppLanguage.English] = "Cancel"
            },

            // Dialogs & Messages
            ["Dialog_AddArchive_Title"] = new()
            {
                [AppLanguage.Vietnamese] = "Thêm vào tập tin nén (ZeroZip)",
                [AppLanguage.English] = "Add to Archive (ZeroZip)"
            },
            ["Msg_SelectSourceFirst"] = new()
            {
                [AppLanguage.Vietnamese] = "Vui lòng chọn tệp hoặc thư mục nguồn hợp lệ trước khi bắt đầu.",
                [AppLanguage.English] = "Please select a valid source file or folder before proceeding."
            },
            ["Msg_ConfirmUnregisterShell"] = new()
            {
                [AppLanguage.Vietnamese] = "ZeroZip hiện đã có trong menu chuột phải của Windows. Bạn có muốn gỡ bỏ tích hợp không?",
                [AppLanguage.English] = "ZeroZip is currently integrated into Windows Explorer context menu. Do you want to remove it?"
            },
            ["Msg_ShellRemoved"] = new()
            {
                [AppLanguage.Vietnamese] = "Đã gỡ bỏ ZeroZip khỏi menu chuột phải Windows Explorer.",
                [AppLanguage.English] = "Successfully removed ZeroZip from Windows Explorer context menu."
            },
            ["Msg_ShellAdded"] = new()
            {
                [AppLanguage.Vietnamese] = "Đã đăng ký thành công ZeroZip vào menu chuột phải Windows Explorer!",
                [AppLanguage.English] = "Successfully registered ZeroZip into Windows Explorer context menu!"
            },
            ["Msg_SelectExtractDest"] = new()
            {
                [AppLanguage.Vietnamese] = "Chọn thư mục đích để giải nén toàn bộ:",
                [AppLanguage.English] = "Select destination folder to extract all files:"
            },
            ["Msg_ArchiveInfo"] = new()
            {
                [AppLanguage.Vietnamese] = "Thông Tin Gói Nén ZeroZip",
                [AppLanguage.English] = "ZeroZip Archive Information"
            },
            ["Msg_AboutTitle"] = new()
            {
                [AppLanguage.Vietnamese] = "Về ZeroZip Sovereign Archiver",
                [AppLanguage.English] = "About ZeroZip Sovereign Archiver"
            },
            ["Msg_AboutText"] = new()
            {
                [AppLanguage.Vietnamese] = "ZeroZip Sovereign Archiver v1.0.0\nKiến trúc ZeroUniverse Sovereign Codecs.\nTối ưu hiệu năng cao với Zstandard, Brotli, LZMA & Gorilla.\nBản quyền © 2026 ZeroUniverse.",
                [AppLanguage.English] = "ZeroZip Sovereign Archiver v1.0.0\nPowered by ZeroUniverse Sovereign Codecs.\nUltra-performance archiving with Zstandard, Brotli, LZMA & Gorilla.\nCopyright © 2026 ZeroUniverse."
            },

            // Context Menu Keys
            ["Ctx_CopyPath"] = new()
            {
                [AppLanguage.Vietnamese] = "📋 Sao chép đường dẫn (Copy Path)",
                [AppLanguage.English] = "📋 Copy Path"
            },
            ["Ctx_Properties"] = new()
            {
                [AppLanguage.Vietnamese] = "ℹ️ Thuộc tính tệp (Properties)",
                [AppLanguage.English] = "ℹ️ Properties"
            },
            ["Ctx_OpenFolder"] = new()
            {
                [AppLanguage.Vietnamese] = "📁 Mở thư mục này",
                [AppLanguage.English] = "📁 Open Folder"
            },
            ["Studio_LblSource"] = new()
            {
                [AppLanguage.Vietnamese] = "Tập tin hoặc thư mục nguồn:",
                [AppLanguage.English] = "Source file or folder:"
            },
            ["Studio_LblDestSave"] = new()
            {
                [AppLanguage.Vietnamese] = "Tập tin nén đích (.zz, .zip, .exe):",
                [AppLanguage.English] = "Destination archive (.zz, .zip, .exe):"
            },
            ["Studio_Card1_Title"] = new()
            {
                [AppLanguage.Vietnamese] = "1. Nguồn Dữ Liệu và Tệp Đích",
                [AppLanguage.English] = "1. Source Data and Destination"
            },
            ["Studio_Card1_Sub"] = new()
            {
                [AppLanguage.Vietnamese] = "Kéo thả thư mục hoặc tệp vào đây, hoặc nhấn nút chọn đường dẫn",
                [AppLanguage.English] = "Drag and drop folders/files here, or click to browse"
            },
            ["Studio_Card2_Title"] = new()
            {
                [AppLanguage.Vietnamese] = "2. Thuật Toán và Động Cơ Nén (ZeroUniverse)",
                [AppLanguage.English] = "2. Compression Algorithm and Engine (ZeroUniverse)"
            },
            ["Studio_Card2_Sub"] = new()
            {
                [AppLanguage.Vietnamese] = "Tự động nhận diện cấu trúc tệp (DataClassifier) và chọn thuật toán tối ưu",
                [AppLanguage.English] = "Automatic pattern classification (DataClassifier) and optimal engine"
            },
            ["Studio_Card3_Title"] = new()
            {
                [AppLanguage.Vietnamese] = "3. Bảo Mật và Mã Hóa Dữ Liệu",
                [AppLanguage.English] = "3. Data Security and Encryption"
            },
            ["Studio_Card3_Sub"] = new()
            {
                [AppLanguage.Vietnamese] = "Mã hóa có xác thực cấp quân sự AES-256-GCM AEAD (Chống giả mạo)",
                [AppLanguage.English] = "Military-grade authenticated encryption AES-256-GCM AEAD"
            },
            ["Studio_Card4_Title"] = new()
            {
                [AppLanguage.Vietnamese] = "4. Thực Thi và Giám Sát Tiến Trình",
                [AppLanguage.English] = "4. Execution and Progress"
            },
            ["Studio_Card4_Sub"] = new()
            {
                [AppLanguage.Vietnamese] = "Khởi chạy thuật toán siêu nén hoặc kiểm tra trước tỉ lệ tiết kiệm",
                [AppLanguage.English] = "Launch ultra-compression or estimate storage savings"
            },
            ["Studio_BtnEstimate"] = new()
            {
                [AppLanguage.Vietnamese] = "📊 Ước Tính Tỉ Lệ",
                [AppLanguage.English] = "📊 Estimate Ratio"
            },
            ["Studio_BtnCompress"] = new()
            {
                [AppLanguage.Vietnamese] = "⚡ Bắt Đầu Nén",
                [AppLanguage.English] = "⚡ Start Compression"
            }
        };
    }
}
