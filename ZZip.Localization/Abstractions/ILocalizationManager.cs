using System;
using System.Collections.Generic;

namespace ZZip.Localization;

/// <summary>
/// Sovereign Localization Manager Contract.
/// Orchestrates language discovery, switching, translation lookup, and external provider integration.
/// </summary>
public interface ILocalizationManager
{
    LanguageInfo CurrentLanguage { get; }

    IReadOnlyList<LanguageInfo> GetAvailableLanguages();

    bool SetLanguage(string languageCode);

    string GetString(string key, params object[] args);

    string Get(string key, params object[] args);

    void RegisterPackage(ILanguagePackage package);

    int LoadPackagesFromDirectory(string directoryPath);

    event Action<LanguageInfo>? LanguageChanged;
}
