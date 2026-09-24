using System;
using System.Text.Json;
using ZeroZip.Localization;
using ZeroZip.Localization.Languages;

namespace ZeroZip.Tests;

public class LocalizationTests
{
    [Fact]
    public void BuiltInPackages_ShouldBeRegisteredByDefault()
    {
        var manager = LocalizationManager.Instance;
        var available = manager.GetAvailableLanguages();

        Assert.Contains(available, l => l.Code == "vi");
        Assert.Contains(available, l => l.Code == "en");
        Assert.Contains(available, l => l.Code == "ja");
        Assert.Contains(available, l => l.Code == "zh");
    }

    [Fact]
    public void SetLanguage_ShouldUpdateCurrentAndNotify()
    {
        var manager = LocalizationManager.Instance;
        manager.SetLanguage("vi");

        LanguageInfo? changedTo = null;
        Action<LanguageInfo> handler = lang => changedTo = lang;

        manager.LanguageChanged += handler;
        try
        {
            manager.SetLanguage("en");
            Assert.Equal("en", manager.CurrentLanguage.Code);
            Assert.NotNull(changedTo);
            Assert.Equal("en", changedTo!.Code);

            Assert.Equal("Open Archive... (Ctrl+O)", manager.Get("Menu_OpenArchive"));

            manager.SetLanguage("vi");
            Assert.Equal("vi", manager.CurrentLanguage.Code);
            Assert.Equal("Mở gói nén... (Ctrl+O)", manager.Get("Menu_OpenArchive"));
        }
        finally
        {
            manager.LanguageChanged -= handler;
        }
    }

    [Fact]
    public void Translations_WithArguments_ShouldFormatCorrectly()
    {
        var manager = LocalizationManager.Instance;
        manager.SetLanguage("en");

        var textEn = manager.Get("Status_ItemsSummary", 5, 2, "root");
        Assert.Equal("5 files, 2 folders | Current folder: root", textEn);

        manager.SetLanguage("vi");
        var textVi = manager.Get("Status_ItemsSummary", 5, 2, "gốc");
        Assert.Equal("5 tệp, 2 thư mục | Thư mục hiện tại: gốc", textVi);
    }

    [Fact]
    public void IntelligentFallback_ShouldFallbackToEnOrViWhenKeyMissing()
    {
        var manager = LocalizationManager.Instance;

        // Japanese package has Menu_OpenArchive
        manager.SetLanguage("ja");
        var openText = manager.Get("Menu_OpenArchive");
        Assert.Equal("アーカイブを開く... (Ctrl+O)", openText);

        // A key that might not be in JA should fall back to EN or VI
        var msgInfo = manager.Get("Msg_ArchiveInfo");
        Assert.False(string.IsNullOrWhiteSpace(msgInfo));
        Assert.NotEqual("Msg_ArchiveInfo", msgInfo);
    }

    [Fact]
    public void MissingKeyEverywhere_ShouldReturnKeyName()
    {
        var manager = LocalizationManager.Instance;
        var missingKey = "NonExistent_Key_12345";
        var result = manager.Get(missingKey);
        Assert.Equal(missingKey, result);
    }

    [Fact]
    public void JsonLanguagePackage_ShouldParseAndProvideTranslations()
    {
        var json = """
        {
          "code": "fr",
          "displayName": "French",
          "nativeName": "Français",
          "cultureName": "fr-FR",
          "translations": {
            "Menu_File": "Fichier",
            "Menu_OpenArchive": "Ouvrir l'archive..."
          }
        }
        """;

        var package = JsonLanguagePackage.FromJson(json);
        Assert.Equal("fr", package.Info.Code);
        Assert.Equal("French", package.Info.DisplayName);
        Assert.Equal("Français", package.Info.NativeName);
        Assert.Equal("fr-FR", package.Info.CultureName);

        Assert.Equal("Fichier", package.GetTranslation("Menu_File"));
        Assert.Equal("Ouvrir l'archive...", package.GetTranslation("Menu_OpenArchive"));
        Assert.Null(package.GetTranslation("NonExistentKey"));

        // Test registering into manager
        var manager = LocalizationManager.Instance;
        manager.RegisterPackage(package);

        var available = manager.GetAvailableLanguages();
        Assert.Contains(available, l => l.Code == "fr");

        manager.SetLanguage("fr");
        Assert.Equal("Ouvrir l'archive...", manager.Get("Menu_OpenArchive"));

        // Fallback for missing key in French package (Msg_ArchiveInfo should fall back to English/Vietnamese)
        var msgInfo = manager.Get("Msg_ArchiveInfo");
        Assert.False(string.IsNullOrWhiteSpace(msgInfo));
        Assert.NotEqual("Msg_ArchiveInfo", msgInfo);
    }
}
