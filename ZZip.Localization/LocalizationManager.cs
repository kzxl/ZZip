using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZZip.Localization.Languages;

namespace ZZip.Localization;

/// <summary>
/// Sovereign Localization Manager.
/// Implements auto-discovery, package registry, intelligent fallback, and dynamic multi-language switching.
/// </summary>
public sealed class LocalizationManager : ILocalizationManager
{
    private static readonly Lazy<LocalizationManager> _lazyInstance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => _lazyInstance.Value;

    private readonly ConcurrentDictionary<string, ILanguagePackage> _packages = new(StringComparer.OrdinalIgnoreCase);
    private ILanguagePackage _currentPackage;
    private readonly ILanguagePackage _fallbackEnPackage;
    private readonly ILanguagePackage _fallbackViPackage;

    public event Action<LanguageInfo>? LanguageChanged;

    public LanguageInfo CurrentLanguage => _currentPackage.Info;

    private LocalizationManager()
    {
        // 1. Register Built-in Language Packages
        _fallbackViPackage = new VietnameseLanguagePackage();
        _fallbackEnPackage = new EnglishLanguagePackage();

        RegisterPackage(_fallbackViPackage);
        RegisterPackage(_fallbackEnPackage);
        RegisterPackage(new JapaneseLanguagePackage());
        RegisterPackage(new ChineseLanguagePackage());

        // Default to Vietnamese as requested by user context
        _currentPackage = _fallbackViPackage;

        // 2. Discover External Community JSON Language Packs
        TryDiscoverExternalPackages();
    }

    /// <summary>
    /// Register or replace a language package in the registry.
    /// </summary>
    public void RegisterPackage(ILanguagePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        _packages[package.Info.Code] = package;
    }

    /// <summary>
    /// Gets all available registered language descriptors.
    /// </summary>
    public IReadOnlyList<LanguageInfo> GetAvailableLanguages()
    {
        return _packages.Values
            .Select(p => p.Info)
            .OrderBy(i => i.Code == "vi" ? 0 : i.Code == "en" ? 1 : 2)
            .ThenBy(i => i.DisplayName)
            .ToList();
    }

    /// <summary>
    /// Switches the active language by its ISO/short code (e.g. "vi", "en", "ja", "zh", "fr").
    /// </summary>
    public bool SetLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return false;

        if (_packages.TryGetValue(languageCode.Trim(), out var pkg))
        {
            if (_currentPackage.Info.Code.Equals(pkg.Info.Code, StringComparison.OrdinalIgnoreCase))
                return true;

            _currentPackage = pkg;
            LanguageChanged?.Invoke(_currentPackage.Info);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Retrieves a localized string by key with multi-tier fallback (Current -> EN -> VI -> Key).
    /// </summary>
    public string GetString(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        // 1. Try current language
        string? val = _currentPackage.GetTranslation(key);

        // 2. Fallback to English
        if (string.IsNullOrEmpty(val) && _currentPackage != _fallbackEnPackage)
        {
            val = _fallbackEnPackage.GetTranslation(key);
        }

        // 3. Fallback to Vietnamese
        if (string.IsNullOrEmpty(val) && _currentPackage != _fallbackViPackage)
        {
            val = _fallbackViPackage.GetTranslation(key);
        }

        // 4. Default to raw key if all translations missing
        string format = val ?? key;

        return args.Length > 0 ? string.Format(format, args) : format;
    }

    /// <summary>
    /// Alias for GetString for concise syntax.
    /// </summary>
    public string Get(string key, params object[] args) => GetString(key, args);

    /// <summary>
    /// Loads all JSON language packages found in the specified directory.
    /// </summary>
    public int LoadPackagesFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;

        int count = 0;
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var pkg = JsonLanguagePackage.FromFile(file);
                RegisterPackage(pkg);
                count++;
            }
            catch
            {
                // Silently skip malformed community packages
            }
        }
        return count;
    }

    private void TryDiscoverExternalPackages()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string langDir = Path.Combine(baseDir, "languages");
            if (Directory.Exists(langDir))
            {
                LoadPackagesFromDirectory(langDir);
            }
        }
        catch { }
    }
}
