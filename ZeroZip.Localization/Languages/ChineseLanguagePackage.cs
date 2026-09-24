using System.Collections.Generic;

namespace ZeroZip.Localization.Languages;

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
        ["App_Title"] = "ZeroZip — 主权级超高压缩工坊与归档管理器",
        ["Brand_Title"] = "⚡ ZEROZIP ARCHIVER",
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
        ["Menu_About"] = "关于 ZeroZip",

        // Action Toolbar
        ["Btn_Add"] = "➕ 添加...",
        ["Btn_Open"] = "📂 打开...",
        ["Btn_Extract"] = "📥 解压...",
        ["Btn_Test"] = "🧪 测试",
        ["Btn_View"] = "👁️ 查看",
        ["Btn_Info"] = "ℹ️ 信息",
        ["Btn_Theme_Light"] = "☀️ 浅色",
        ["Btn_Theme_Dark"] = "🌙 深色",

        // Tabs
        ["Tab_Explorer"] = "📁 归档管理器",
        ["Tab_Studio"] = "⚡ 压缩工坊",

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

        // Dialogs & Messages
        ["Dialog_AddArchive_Title"] = "添加到压缩文件 (ZeroZip)",
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
        ["Studio_BtnCompress"] = "⚡ 开始压缩"
    };
}
