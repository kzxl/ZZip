using System.Collections.Generic;

namespace ZZip.Localization;

/// <summary>
/// Contract representing an independent language translation package.
/// Can be loaded from code (compiled), embedded resources, or external files (.json).
/// </summary>
public interface ILanguagePackage
{
    LanguageInfo Info { get; }

    IReadOnlyDictionary<string, string> Translations { get; }

    string? GetTranslation(string key);
}
