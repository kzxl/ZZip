using System.Collections.Generic;

namespace ZeroZip.Localization.Languages;

/// <summary>
/// Built-in Japanese Language Package (日本語).
/// Demonstrates multi-language modular expansion with automatic fallback for missing keys.
/// </summary>
public sealed class JapaneseLanguagePackage : BaseLanguagePackage
{
    public static readonly LanguageInfo PackageInfo = new()
    {
        Code = "ja",
        DisplayName = "🇯🇵 日本語",
        NativeName = "日本語",
        CultureName = "ja-JP"
    };

    public JapaneseLanguagePackage() : base(PackageInfo, CreateDictionary())
    {
    }

    private static Dictionary<string, string> CreateDictionary() => new()
    {
        // Window & Brand
        ["App_Title"] = "ZeroZip — 超高速圧縮スタジオ ＆ アーカイブエクスプローラー",
        ["Brand_Title"] = "⚡ ZEROZIP ARCHIVER",
        ["Brand_Subtitle"] = "超高速圧縮ツール (Zstd/LZMA/Brotli) — ZeroUniverse",

        // Menu Bar - File
        ["Menu_File"] = "ファイル",
        ["Menu_OpenArchive"] = "アーカイブを開く... (Ctrl+O)",
        ["Menu_NewArchive"] = "新しいアーカイブを作成 (Studio)... (Ctrl+N)",
        ["Menu_CloseArchive"] = "アーカイブを閉じる",
        ["Menu_Exit"] = "終了 (Alt+F4)",

        // Menu Bar - Commands
        ["Menu_Commands"] = "コマンド",
        ["Menu_AddFiles"] = "ファイルをアーカイブに追加...",
        ["Menu_ExtractTo"] = "指定フォルダに展開...",
        ["Menu_TestArchive"] = "アーカイブのテスト",
        ["Menu_ViewFile"] = "ファイルを表示 (Enter / F3)",
        ["Menu_SelectAll"] = "すべて選択 (Ctrl+A)",

        // Menu Bar - Tools
        ["Menu_Tools"] = "ツール",
        ["Menu_Estimate"] = "圧縮率の試算...",
        ["Menu_ShellIntegrate"] = "Windows Explorer コンテキストメニューの統合",

        // Menu Bar - Options
        ["Menu_Options"] = "オプション",
        ["Menu_ToggleTheme"] = "ライト/ダークテーマの切り替え (Ctrl+T)",

        // Menu Bar - Language
        ["Menu_Language"] = "言語 (Language)",

        // Menu Bar - Help
        ["Menu_Help"] = "ヘルプ",
        ["Menu_About"] = "ZeroZip について",

        // Action Toolbar
        ["Btn_Add"] = "➕ 追加...",
        ["Btn_Open"] = "📂 開く...",
        ["Btn_Extract"] = "📥 展開...",
        ["Btn_Test"] = "🧪 テスト",
        ["Btn_View"] = "👁️ 表示",
        ["Btn_Info"] = "ℹ️ 情報",
        ["Btn_Theme_Light"] = "☀️ ライト",
        ["Btn_Theme_Dark"] = "🌙 ダーク",

        // Tabs
        ["Tab_Explorer"] = "📁 アーカイブエクスプローラー",
        ["Tab_Studio"] = "⚡ 圧縮スタジオ",

        // Path & Navigation Bar
        ["Path_Up"] = "⬆️ 上へ",
        ["Path_Refresh"] = "🔄 更新",
        ["Path_OpenArchive"] = "📂 開く...",

        // Grid Columns
        ["Col_Name"] = "名前",
        ["Col_OriginalSize"] = "元のサイズ",
        ["Col_PackedSize"] = "圧縮後サイズ",
        ["Col_Ratio"] = "圧縮率",
        ["Col_Method"] = "方式",
        ["Col_Modified"] = "更新日時",
        ["Col_CRC"] = "CRC32",
        ["Col_Type"] = "種類",
        ["Col_Attributes"] = "属性",

        // Grid Cell & Type Texts
        ["Type_Folder"] = "ファイル フォルダー",
        ["Type_File"] = "ファイル",
        ["Folder_Tag"] = "<フォルダー>",

        // Status Bar
        ["Status_NoArchive"] = "アーカイブが開かれていません。",
        ["Status_ItemsSummary"] = "{0} ファイル, {1} フォルダー | 現在の場所: {2}",
        ["Status_ZeroSelected"] = "0 個の項目を選択",
        ["Status_SelectedItems"] = "{0} 個の項目を選択 ({1})",

        // Dialogs & Messages
        ["Dialog_AddArchive_Title"] = "アーカイブへの追加 (ZeroZip)",
        ["Studio_LblSource"] = "圧縮元のファイルまたはフォルダー:",
        ["Studio_LblDestSave"] = "出力先アーカイブ (.zz, .zip, .exe):",
        ["Studio_Card1_Title"] = "1. ソースデータと出力先",
        ["Studio_Card1_Sub"] = "フォルダーまたはファイルをここにドラッグ＆ドロップしてください",
        ["Studio_Card2_Title"] = "2. 圧縮アルゴリズムとエンジン (ZeroUniverse)",
        ["Studio_Card2_Sub"] = "データ分類 (DataClassifier) による最適な圧縮方式の自動選択",
        ["Studio_Card3_Title"] = "3. セキュリティと暗号化",
        ["Studio_Card3_Sub"] = "軍用グレード認証付き暗号化 AES-256-GCM AEAD",
        ["Studio_Card4_Title"] = "4. 実行と進捗状況",
        ["Studio_Card4_Sub"] = "超高速圧縮を開始または圧縮率を試算します",
        ["Studio_BtnEstimate"] = "📊 圧縮率を試算",
        ["Studio_BtnCompress"] = "⚡ 圧縮を開始"
    };
}
