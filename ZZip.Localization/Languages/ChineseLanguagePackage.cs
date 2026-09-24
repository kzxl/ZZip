using System.Collections.Generic;

namespace ZZip.Localization.Languages;

/// <summary>
/// Built-in Chinese Language Package (简体中文).
/// Demonstrates multi-language modular expansion with automatic fallback for missing keys.
/// </summary>
public sealed class ChineseLanguagePackage : BaseLanguagePackage
{
    public static readonly LanguageInfo PackageInfo = new()
    {
        Code = "zh",
        DisplayName = "🇨🇳 简体中文",
        NativeName = "简体中文",
        CultureName = "zh-CN"
    };

    public ChineseLanguagePackage() : base(PackageInfo, CreateDictionary())
    {
    }

    private static Dictionary<string, string> CreateDictionary() => new()
    {
        // Window & Brand
        ["App_Title"] = "ZZip — 主权级超高压缩工坊与归档管理器",
        ["Brand_Title"] = "⚡ ZZIP ARCHIVER",
        ["Brand_Subtitle"] = "高性能极速压缩工具 (Zstd/LZMA/Brotli) — ZeroUniverse",

        // Menu Bar - File
        ["Menu_File"] = "文件",
        ["Menu_OpenArchive"] = "打开压缩文件... (Ctrl+O)",
        ["Menu_NewArchive"] = "新建压缩包 (Studio)... (Ctrl+N)",
        ["Menu_CloseArchive"] = "关闭当前压缩包",
        ["Menu_Exit"] = "退出 (Alt+F4)",

        // Menu Bar - Commands
        ["Menu_Commands"] = "命令",
        ["Menu_AddFiles"] = "添加文件到压缩包...",
        ["Menu_ExtractTo"] = "解压到指定文件夹...",
        ["Menu_TestArchive"] = "测试压缩文件完整性",
        ["Menu_ViewFile"] = "查看文件 (Enter / F3)",
        ["Menu_SelectAll"] = "全选 (Ctrl+A)",

        // Menu Bar - Tools
        ["Menu_Tools"] = "工具",
        ["Menu_Estimate"] = "评估压缩比...",
        ["Menu_ShellIntegrate"] = "Windows 资源管理器右键菜单集成",

        // Menu Bar - Options
        ["Menu_Options"] = "选项",
        ["Menu_ToggleTheme"] = "切换明暗主题 (Ctrl+T)",

        // Menu Bar - Language
        ["Menu_Language"] = "语言 (Language)",

        // Menu Bar - Help
        ["Menu_Help"] = "帮助",
        ["Menu_About"] = "关于 ZZip",

        // Action Toolbar
        ["Btn_Add"] = "➕ 添加...",
        ["Btn_Open"] = "📂 打开...",
        ["Btn_Extract"] = "📥 解压...",
        ["Btn_Test"] = "🧪 测试",
        ["Btn_View"] = "👁️ 查看",
        ["Btn_Info"] = "ℹ️ 信息",
        ["Btn_Theme_Light"] = "☀️ 浅色",
        ["Btn_Theme_Dark"] = "🌙 深色",
        ["Btn_Cancel"] = "取消",
        ["Btn_Close"] = "关闭",

        // Tabs
        ["Tab_Explorer"] = "📁 归档管理器",
        ["Tab_Studio"] = "⚡ 压缩工坊",
        ["Tab_General"] = "常规 (General)",
        ["Tab_Advanced"] = "高级 (Advanced)",
        ["Tab_Security"] = "安全 (Security)",

        // Path & Navigation Bar
        ["Path_Up"] = "⬆️ 向上",
        ["Path_Refresh"] = "🔄 刷新",
        ["Path_OpenArchive"] = "📂 打开...",

        // Grid Columns
        ["Col_Name"] = "名称",
        ["Col_OriginalSize"] = "原始大小",
        ["Col_PackedSize"] = "压缩大小",
        ["Col_Ratio"] = "压缩率",
        ["Col_Method"] = "算法",
        ["Col_Modified"] = "修改日期",
        ["Col_CRC"] = "CRC32",
        ["Col_Type"] = "类型",
        ["Col_Attributes"] = "属性",

        // Grid Cell & Type Texts
        ["Type_Folder"] = "文件夹",
        ["Type_File"] = "文件",
        ["Folder_Tag"] = "<文件夹>",

        // Status Bar
        ["Status_NoArchive"] = "未打开任何压缩文件。",
        ["Status_ItemsSummary"] = "{0} 个文件，{1} 个文件夹 | 当前位置: {2}",
        ["Status_ZeroSelected"] = "已选 0 项",
        ["Status_SelectedItems"] = "已选 {0} 项 ({1})",
        ["Empty_Title"] = "尚未打开任何压缩包",
        ["Empty_Desc"] = "将压缩包（.zz, .zip, .rar, .7z）或文件夹拖放至此进行浏览或压缩。\n或者选择下方的快捷操作：",

        // Dialogs & Messages
        ["Dialog_AddArchive_Title"] = "添加到压缩文件 (ZZip)",
        ["Msg_LowDiskSpaceTitle"] = "磁盘空间不足警告",
        ["Msg_LowDiskSpaceWarning"] = "目标驱动器没有足够的剩余空间来解压文件！\n\n• 解压所需大小: {0}\n• 剩余可用空间 ({1}): {2}\n• 空间不足: {3}\n\n您确定要继续解压吗？",
        ["Studio_LblSource"] = "源文件或文件夹:",
        ["Studio_LblDestSave"] = "目标压缩包 (.zz, .zip, .exe):",
        ["Studio_Card1_Title"] = "1. 源数据与目标文件",
        ["Studio_Card1_Sub"] = "拖放文件夹或文件至此，或点击按钮浏览",
        ["Studio_Card2_Title"] = "2. 压缩算法与引擎 (ZeroUniverse)",
        ["Studio_Card2_Sub"] = "自动模式识别 (DataClassifier) 与最优算法匹配",
        ["Studio_Card3_Title"] = "3. 安全与数据加密",
        ["Studio_Card3_Sub"] = "军工级认证加密 AES-256-GCM AEAD (防篡改)",
        ["Studio_Card4_Title"] = "4. 执行与进度监控",
        ["Studio_Card4_Sub"] = "启动极限压缩或预先估算压缩收益",
        ["Studio_BtnEstimate"] = "📊 评估压缩比",
        ["Studio_BtnCompress"] = "⚡ 开始压缩",

        // Shell Context Menu
        ["Shell_OpenArchive"] = "打开压缩文件",
        ["Shell_ExtractFiles"] = "解压文件...",
        ["Shell_ExtractHere"] = "解压到当前文件夹",
        ["Shell_ExtractToFolder"] = "解压到单独文件夹...",
        ["Shell_TestArchive"] = "测试压缩文件",
        ["Shell_AddToArchive"] = "添加到压缩文件...",
        ["Shell_CompressZz"] = "快速压缩为 .zz",
        ["Shell_CompressZip"] = "快速压缩为 .zip",
        ["Shell_CompressSplit2G"] = "压缩并分卷为 2GB (.001, .002)...",
        ["Shell_CrcSha"] = "校验和与压缩率检查 (CRC SHA)...",
        ["Shell_CompressDirZz"] = "将文件夹快速压缩为 .zz",
        ["Shell_CompressDirZip"] = "将文件夹快速压缩为 .zip",
        ["Shell_CompressDirSplit2G"] = "将文件夹压缩并分卷 2GB...",
        ["Shell_CompressCurrentFolderZz"] = "将当前文件夹压缩为 .zz...",
        ["Shell_CompressCurrentFolderZip"] = "将当前文件夹压缩为 .zip...",
        ["Shell_OpenStudioCurrent"] = "在此处打开 ZZip Studio...",
        ["Shell_ZZipArchiveDesc"] = "ZZip 归档文件 (.zz)"
    };
}
