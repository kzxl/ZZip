using System.Collections.Generic;

namespace ZeroZip.Localization.Languages;

/// <summary>
/// Built-in Vietnamese Language Package (Tiếng Việt).
/// </summary>
public sealed class VietnameseLanguagePackage : BaseLanguagePackage
{
    public static readonly LanguageInfo PackageInfo = new()
    {
        Code = "vi",
        DisplayName = "🇻🇳 Tiếng Việt",
        NativeName = "Tiếng Việt",
        CultureName = "vi-VN"
    };

    public VietnameseLanguagePackage() : base(PackageInfo, CreateDictionary())
    {
    }

    private static Dictionary<string, string> CreateDictionary() => new()
    {
        // Window & Brand
        ["App_Title"] = "ZeroZip — Sovereign Ultra-Compression Studio & Archive Explorer",
        ["Brand_Title"] = "⚡ ZEROZIP ARCHIVER",
        ["Brand_Subtitle"] = "Bộ Công Cụ Nén Siêu Tốc (Zstd/LZMA/Brotli) Chuẩn ZeroUniverse",

        // Menu Bar - File
        ["Menu_File"] = "Tập tin",
        ["Menu_OpenArchive"] = "Mở gói nén... (Ctrl+O)",
        ["Menu_NewArchive"] = "Tạo gói nén mới (Studio)... (Ctrl+N)",
        ["Menu_CloseArchive"] = "Đóng gói hiện tại",
        ["Menu_Exit"] = "Thoát (Alt+F4)",

        // Menu Bar - Commands
        ["Menu_Commands"] = "Lệnh",
        ["Menu_AddFiles"] = "Thêm tệp vào kho nén...",
        ["Menu_ExtractTo"] = "Giải nén vào thư mục...",
        ["Menu_TestArchive"] = "Kiểm tra tính toàn vẹn gói nén",
        ["Menu_ViewFile"] = "Xem tệp (Enter / F3)",
        ["Menu_SelectAll"] = "Chọn tất cả (Ctrl+A)",

        // Menu Bar - Tools
        ["Menu_Tools"] = "Công cụ",
        ["Menu_Estimate"] = "Đánh giá tỉ lệ nén nhanh...",
        ["Menu_ShellIntegrate"] = "Cài đặt Menu chuột phải Windows Explorer",

        // Menu Bar - Options
        ["Menu_Options"] = "Tùy chọn",
        ["Menu_ToggleTheme"] = "Chuyển giao diện Sáng / Tối (Ctrl+T)",

        // Menu Bar - Language
        ["Menu_Language"] = "Ngôn ngữ (Language)",
        ["Menu_Lang_Vi"] = "🇻🇳 Tiếng Việt",
        ["Menu_Lang_En"] = "🇺🇸 English",

        // Menu Bar - Help
        ["Menu_Help"] = "Trợ giúp",
        ["Menu_About"] = "Giới thiệu ZeroZip",

        // Action Toolbar
        ["Btn_Add"] = "➕ Thêm...",
        ["Btn_Open"] = "📂 Mở gói...",
        ["Btn_Extract"] = "📥 Giải nén...",
        ["Btn_Test"] = "🧪 Kiểm tra",
        ["Btn_View"] = "👁️ Xem tệp",
        ["Btn_Info"] = "ℹ️ Thông tin",
        ["Btn_Shell_On"] = "✓ Menu Explorer",
        ["Btn_Shell_Off"] = "⚙️ Bật Menu",
        ["Btn_Lang_Toggle"] = "🌐 EN",
        ["Btn_Theme_Light"] = "☀️ Sáng",
        ["Btn_Theme_Dark"] = "🌙 Tối",

        // Tabs
        ["Tab_Explorer"] = "📁 Trình Duyệt Gói Nén (Explorer)",
        ["Tab_Studio"] = "⚡ Siêu Nén & Đóng Gói (Studio)",

        // Path & Navigation Bar
        ["Path_Up"] = "⬆️ Lên",
        ["Path_Refresh"] = "🔄 Làm mới",
        ["Path_OpenArchive"] = "📂 Mở gói...",

        // Grid Columns
        ["Col_Name"] = "Tên",
        ["Col_OriginalSize"] = "Dung lượng gốc",
        ["Col_PackedSize"] = "Kích thước nén",
        ["Col_Ratio"] = "Tỷ lệ",
        ["Col_Method"] = "Thuật toán",
        ["Col_Modified"] = "Ngày sửa đổi",
        ["Col_CRC"] = "Mã CRC",
        ["Col_Type"] = "Loại tệp",
        ["Col_Attributes"] = "Thuộc tính",

        // Grid Cell & Type Texts
        ["Type_Folder"] = "Thư mục tệp",
        ["Type_File"] = "Tệp tin",
        ["Folder_Tag"] = "<Thư mục>",

        // Status Bar
        ["Status_NoArchive"] = "Chưa mở gói nén nào.",
        ["Status_ItemsSummary"] = "{0} tệp, {1} thư mục | Thư mục hiện tại: {2}",
        ["Status_ZeroSelected"] = "0 mục được chọn",
        ["Status_SelectedItems"] = "Đã chọn {0} mục ({1})",

        // Studio Tab & Dialogs
        ["Studio_SourceTitle"] = "1. Nguồn Dữ Liệu Cần Nén",
        ["Studio_SourceDesc"] = "Chọn một tệp hoặc toàn bộ thư mục để đóng gói siêu nén:",
        ["Studio_BtnBrowseFolder"] = "📁 Thư mục...",
        ["Studio_BtnBrowseFile"] = "📄 Tệp...",
        ["Studio_ConfigTitle"] = "2. Cấu Hình & Thuật Toán Nén",
        ["Studio_LblProfile"] = "Chế độ nén:",
        ["Studio_LblMethod"] = "Thuật toán chính:",
        ["Studio_LblLevel"] = "Mức độ nén (Level):",
        ["Studio_ChkSfx"] = "Tạo tệp tự giải nén (.exe SFX)",
        ["Studio_ChkPrecomp"] = "Bật tiền xử lý dữ liệu (Precomp)",
        ["Studio_OutputTitle"] = "3. Đầu Ra & Tùy Chọn Đóng Gói",
        ["Studio_LblDest"] = "Đường dẫn tệp đích (.zz / .exe / .zip):",
        ["Studio_BtnBrowseDest"] = "Lưu thành...",
        ["Studio_LblPassword"] = "Mật khẩu (AES-256-GCM, tùy chọn):",
        ["Studio_LblSplit"] = "Chia nhỏ khối lượng:",
        ["Studio_BtnStart"] = "🚀 Bắt Đầu Nén Siêu Tốc",
        ["Studio_BtnCancel"] = "Dừng",

        // Dialogs & Messages
        ["Dialog_AddArchive_Title"] = "Thêm vào tập tin nén (ZeroZip)",
        ["Msg_SelectSourceFirst"] = "Vui lòng chọn tệp hoặc thư mục nguồn hợp lệ trước khi bắt đầu.",
        ["Msg_ConfirmUnregisterShell"] = "ZeroZip hiện đã có trong menu chuột phải của Windows. Bạn có muốn gỡ bỏ tích hợp không?",
        ["Msg_ShellRemoved"] = "Đã gỡ bỏ ZeroZip khỏi menu chuột phải Windows Explorer.",
        ["Msg_ShellAdded"] = "Đã đăng ký thành công ZeroZip vào menu chuột phải Windows Explorer!",
        ["Msg_SelectExtractDest"] = "Chọn thư mục đích để giải nén toàn bộ:",
        ["Msg_ArchiveInfo"] = "Thông Tin Gói Nén ZeroZip",
        ["Msg_AboutTitle"] = "Về ZeroZip Sovereign Archiver",
        ["Msg_AboutText"] = "ZeroZip Sovereign Archiver v1.0.0\nKiến trúc ZeroUniverse Sovereign Codecs.\nTối ưu hiệu năng cao với Zstandard, Brotli, LZMA & Gorilla.\nBản quyền © 2026 ZeroUniverse.",

        // Context Menu Keys
        ["Ctx_CopyPath"] = "📋 Sao chép đường dẫn (Copy Path)",
        ["Ctx_Properties"] = "ℹ️ Thuộc tính tệp (Properties)",
        ["Ctx_OpenFolder"] = "📁 Mở thư mục này",

        // Add Archive Dialog Cards
        ["Studio_LblSource"] = "Tập tin hoặc thư mục nguồn:",
        ["Studio_LblDestSave"] = "Tập tin nén đích (.zz, .zip, .exe):",
        ["Studio_Card1_Title"] = "1. Nguồn Dữ Liệu và Tệp Đích",
        ["Studio_Card1_Sub"] = "Kéo thả thư mục hoặc tệp vào đây, hoặc nhấn nút chọn đường dẫn",
        ["Studio_Card2_Title"] = "2. Thuật Toán và Động Cơ Nén (ZeroUniverse)",
        ["Studio_Card2_Sub"] = "Tự động nhận diện cấu trúc tệp (DataClassifier) và chọn thuật toán tối ưu",
        ["Studio_Card3_Title"] = "3. Bảo Mật và Mã Hóa Dữ Liệu",
        ["Studio_Card3_Sub"] = "Mã hóa có xác thực cấp quân sự AES-256-GCM AEAD (Chống giả mạo)",
        ["Studio_Card4_Title"] = "4. Thực Thi và Giám Sát Tiến Trình",
        ["Studio_Card4_Sub"] = "Khởi chạy thuật toán siêu nén hoặc kiểm tra trước tỉ lệ tiết kiệm",
        ["Studio_BtnEstimate"] = "📊 Ước Tính Tỉ Lệ",
        ["Studio_BtnCompress"] = "⚡ Bắt Đầu Nén"
    };
}
