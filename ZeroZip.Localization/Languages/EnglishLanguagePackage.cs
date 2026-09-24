using System.Collections.Generic;

namespace ZeroZip.Localization.Languages;

/// <summary>
/// Built-in English Language Package (English).
/// </summary>
public sealed class EnglishLanguagePackage : BaseLanguagePackage
{
    public static readonly LanguageInfo PackageInfo = new()
    {
        Code = "en",
        DisplayName = "🇺🇸 English",
        NativeName = "English",
        CultureName = "en-US"
    };

    public EnglishLanguagePackage() : base(PackageInfo, CreateDictionary())
    {
    }

    private static Dictionary<string, string> CreateDictionary() => new()
    {
        // Window & Brand
        ["App_Title"] = "ZeroZip — Sovereign Ultra-Compression Studio & Archive Explorer",
        ["Brand_Title"] = "⚡ ZEROZIP ARCHIVER",
        ["Brand_Subtitle"] = "High-Performance Sovereign Archiver (Zstd/LZMA/Brotli) — ZeroUniverse",

        // Menu Bar - File
        ["Menu_File"] = "File",
        ["Menu_OpenArchive"] = "Open Archive... (Ctrl+O)",
        ["Menu_NewArchive"] = "New Archive (Studio)... (Ctrl+N)",
        ["Menu_CloseArchive"] = "Close Archive",
        ["Menu_Exit"] = "Exit (Alt+F4)",

        // Menu Bar - Commands
        ["Menu_Commands"] = "Commands",
        ["Menu_AddFiles"] = "Add Files to Archive...",
        ["Menu_ExtractTo"] = "Extract to Specified Folder...",
        ["Menu_TestArchive"] = "Test Archive Integrity",
        ["Menu_ViewFile"] = "View File (Enter / F3)",
        ["Menu_SelectAll"] = "Select All (Ctrl+A)",

        // Menu Bar - Tools
        ["Menu_Tools"] = "Tools",
        ["Menu_Estimate"] = "Estimate Compression Ratio...",
        ["Menu_ShellIntegrate"] = "Windows Explorer Shell Integration",

        // Menu Bar - Options
        ["Menu_Options"] = "Options",
        ["Menu_ToggleTheme"] = "Toggle Light / Dark Theme (Ctrl+T)",

        // Menu Bar - Language
        ["Menu_Language"] = "Language",
        ["Menu_Lang_Vi"] = "🇻🇳 Vietnamese (Tiếng Việt)",
        ["Menu_Lang_En"] = "🇺🇸 English",

        // Menu Bar - Help
        ["Menu_Help"] = "Help",
        ["Menu_About"] = "About ZeroZip",

        // Action Toolbar
        ["Btn_Add"] = "➕ Add...",
        ["Btn_Open"] = "📂 Open...",
        ["Btn_Extract"] = "📥 Extract...",
        ["Btn_Test"] = "🧪 Test",
        ["Btn_View"] = "👁️ View",
        ["Btn_Info"] = "ℹ️ Info",
        ["Btn_Shell_On"] = "✓ Shell Menu",
        ["Btn_Shell_Off"] = "⚙️ Enable Shell",
        ["Btn_Lang_Toggle"] = "🌐 VI",
        ["Btn_Theme_Light"] = "☀️ Light",
        ["Btn_Theme_Dark"] = "🌙 Dark",

        // Tabs
        ["Tab_Explorer"] = "📁 Archive Explorer",
        ["Tab_Studio"] = "⚡ Compression Studio",

        // Path & Navigation Bar
        ["Path_Up"] = "⬆️ Up",
        ["Path_Refresh"] = "🔄 Refresh",
        ["Path_OpenArchive"] = "📂 Open...",

        // Grid Columns
        ["Col_Name"] = "Name",
        ["Col_OriginalSize"] = "Original Size",
        ["Col_PackedSize"] = "Packed Size",
        ["Col_Ratio"] = "Ratio",
        ["Col_Method"] = "Method",
        ["Col_Modified"] = "Date Modified",
        ["Col_CRC"] = "CRC32",
        ["Col_Type"] = "Type",
        ["Col_Attributes"] = "Attributes",

        // Grid Cell & Type Texts
        ["Type_Folder"] = "File folder",
        ["Type_File"] = "File",
        ["Folder_Tag"] = "<Folder>",

        // Status Bar
        ["Status_NoArchive"] = "No archive opened.",
        ["Status_ItemsSummary"] = "{0} files, {1} folders | Current folder: {2}",
        ["Status_ZeroSelected"] = "0 items selected",
        ["Status_SelectedItems"] = "{0} items selected ({1})",

        // Studio Tab & Dialogs
        ["Studio_SourceTitle"] = "1. Source Data to Compress",
        ["Studio_SourceDesc"] = "Select a file or an entire directory for ultra-compression:",
        ["Studio_BtnBrowseFolder"] = "📁 Folder...",
        ["Studio_BtnBrowseFile"] = "📄 File...",
        ["Studio_ConfigTitle"] = "2. Compression Configuration",
        ["Studio_LblProfile"] = "Compression Profile:",
        ["Studio_LblMethod"] = "Main Algorithm:",
        ["Studio_LblLevel"] = "Compression Level:",
        ["Studio_ChkSfx"] = "Create Self-Extracting Archive (.exe SFX)",
        ["Studio_ChkPrecomp"] = "Enable Pre-compression (Precomp)",
        ["Studio_OutputTitle"] = "3. Output & Packaging Options",
        ["Studio_LblDest"] = "Destination Archive Path (.zz / .exe / .zip):",
        ["Studio_BtnBrowseDest"] = "Save as...",
        ["Studio_LblPassword"] = "Password (AES-256-GCM, optional):",
        ["Studio_LblSplit"] = "Split Volumes:",
        ["Studio_BtnStart"] = "🚀 Start Ultra-Compression",
        ["Studio_BtnCancel"] = "Cancel",

        // Dialogs & Messages
        ["Dialog_AddArchive_Title"] = "Add to Archive (ZeroZip)",
        ["Msg_SelectSourceFirst"] = "Please select a valid source file or folder before proceeding.",
        ["Msg_ConfirmUnregisterShell"] = "ZeroZip is currently integrated into Windows Explorer context menu. Do you want to remove it?",
        ["Msg_ShellRemoved"] = "Successfully removed ZeroZip from Windows Explorer context menu.",
        ["Msg_ShellAdded"] = "Successfully registered ZeroZip into Windows Explorer context menu!",
        ["Msg_LowDiskSpaceTitle"] = "Disk Space Warning",
        ["Msg_LowDiskSpaceWarning"] = "The destination drive does not have enough free space to extract the archive!\n\n• Required uncompressed size: {0}\n• Available free space ({1}): {2}\n• Deficit: {3}\n\nDo you want to proceed anyway?",
        ["Msg_SelectExtractDest"] = "Select destination folder to extract all files:",
        ["Msg_ArchiveInfo"] = "ZeroZip Archive Information",
        ["Msg_AboutTitle"] = "About ZeroZip Sovereign Archiver",
        ["Msg_AboutText"] = "ZeroZip Sovereign Archiver v1.0.0\nPowered by ZeroUniverse Sovereign Codecs.\nUltra-performance archiving with Zstandard, Brotli, LZMA & Gorilla.\nCopyright © 2026 ZeroUniverse.",

        // Context Menu Keys
        ["Ctx_CopyPath"] = "📋 Copy Path",
        ["Ctx_Properties"] = "ℹ️ Properties",
        ["Ctx_OpenFolder"] = "📁 Open Folder",

        // Add Archive Dialog Cards
        ["Studio_LblSource"] = "Source file or folder:",
        ["Studio_LblDestSave"] = "Destination archive (.zz, .zip, .exe):",
        ["Studio_Card1_Title"] = "1. Source Data and Destination",
        ["Studio_Card1_Sub"] = "Drag and drop folders/files here, or click to browse",
        ["Studio_Card2_Title"] = "2. Compression Algorithm and Engine (ZeroUniverse)",
        ["Studio_Card2_Sub"] = "Automatic pattern classification (DataClassifier) and optimal engine",
        ["Studio_Card3_Title"] = "3. Data Security and Encryption",
        ["Studio_Card3_Sub"] = "Military-grade authenticated encryption AES-256-GCM AEAD",
        ["Studio_Card4_Title"] = "4. Execution and Progress",
        ["Studio_Card4_Sub"] = "Launch ultra-compression or estimate storage savings",
        ["Studio_BtnEstimate"] = "📊 Estimate Ratio",
        ["Studio_BtnCompress"] = "⚡ Start Compression",

        // Shell Context Menu
        ["Shell_OpenArchive"] = "Open archive",
        ["Shell_ExtractFiles"] = "Extract files...",
        ["Shell_ExtractHere"] = "Extract Here",
        ["Shell_ExtractToFolder"] = "Extract to folder...",
        ["Shell_TestArchive"] = "Test archive",
        ["Shell_AddToArchive"] = "Add to archive...",
        ["Shell_CompressZz"] = "Fast compress to .zz",
        ["Shell_CompressZip"] = "Fast compress to .zip",
        ["Shell_CompressSplit2G"] = "Compress & split 2GB (.001, .002)...",
        ["Shell_CrcSha"] = "Checksum & compression ratio (CRC SHA)...",
        ["Shell_CompressDirZz"] = "Fast compress folder to .zz",
        ["Shell_CompressDirZip"] = "Fast compress folder to .zip",
        ["Shell_CompressDirSplit2G"] = "Compress folder & split 2GB...",
        ["Shell_CompressCurrentFolderZz"] = "Compress current folder to .zz...",
        ["Shell_CompressCurrentFolderZip"] = "Compress current folder to .zip...",
        ["Shell_OpenStudioCurrent"] = "Open ZeroZip Studio here...",
        ["Shell_ZeroZipArchiveDesc"] = "ZeroZip Sovereign Archive (.zz)"
    };
}
