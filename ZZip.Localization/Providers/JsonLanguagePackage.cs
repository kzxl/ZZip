using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZZip.Localization;

/// <summary>
/// Dynamic language package loaded from a JSON file or stream.
/// Enables users and translators to drop in new .json files into languages/ folder without recompiling.
/// </summary>
public class JsonLanguagePackage : BaseLanguagePackage
{
    private class JsonSchema
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("nativeName")]
        public string? NativeName { get; set; }

        [JsonPropertyName("cultureName")]
        public string? CultureName { get; set; }

        [JsonPropertyName("translations")]
        public Dictionary<string, string>? Translations { get; set; }
    }

    private JsonLanguagePackage(LanguageInfo info, IDictionary<string, string> dictionary)
        : base(info, dictionary)
    {
    }

    public static JsonLanguagePackage FromJson(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new ArgumentException("JSON content cannot be empty", nameof(jsonContent));

        var doc = JsonSerializer.Deserialize<JsonSchema>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to parse language package JSON.");

        if (string.IsNullOrWhiteSpace(doc.Code))
            throw new InvalidOperationException("Language package JSON must contain a non-empty 'code' field.");

        var info = new LanguageInfo
        {
            Code = doc.Code.Trim().ToLowerInvariant(),
            DisplayName = string.IsNullOrWhiteSpace(doc.DisplayName) ? doc.Code : doc.DisplayName,
            NativeName = doc.NativeName,
            CultureName = doc.CultureName
        };

        return new JsonLanguagePackage(info, doc.Translations ?? new Dictionary<string, string>());
    }

    public static JsonLanguagePackage FromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Language package file not found: {filePath}", filePath);

        string json = File.ReadAllText(filePath);
        return FromJson(json);
    }
}
