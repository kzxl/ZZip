namespace ZeroZip.Localization;

/// <summary>
/// Immutable metadata descriptor for a supported language.
/// </summary>
public record LanguageInfo
{
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public string? NativeName { get; init; }
    public string? CultureName { get; init; }

    public override string ToString() => DisplayName;
}
