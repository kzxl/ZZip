using System;
using System.Collections.Generic;

namespace ZeroZip.Localization;

/// <summary>
/// Abstract base class for strongly-typed language packages.
/// </summary>
public abstract class BaseLanguagePackage : ILanguagePackage
{
    private readonly Dictionary<string, string> _translations;

    public LanguageInfo Info { get; }

    public IReadOnlyDictionary<string, string> Translations => _translations;

    protected BaseLanguagePackage(LanguageInfo info, IDictionary<string, string> dictionary)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        _translations = new Dictionary<string, string>(dictionary, StringComparer.OrdinalIgnoreCase);
    }

    public string? GetTranslation(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return _translations.TryGetValue(key, out var val) ? val : null;
    }
}
