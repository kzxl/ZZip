using System;
using System.Collections.Generic;
using ZeroZip.Localization;

namespace ZeroZip.Main.Services
{
    public enum AppLanguage
    {
        Vietnamese,
        English
    }

    /// <summary>
    /// UI Facade wrapping the independent ZeroZip.Localization module.
    /// Preserves complete backward compatibility while exposing multi-language extensibility.
    /// </summary>
    public static class LocalizationService
    {
        static LocalizationService()
        {
            LocalizationManager.Instance.LanguageChanged += _ => LanguageChanged?.Invoke();
        }

        public static event Action? LanguageChanged;

        public static string CurrentLanguageCode => LocalizationManager.Instance.CurrentLanguage.Code;

        public static AppLanguage CurrentLanguage =>
            string.Equals(CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.English
                : AppLanguage.Vietnamese;

        public static void SetLanguage(AppLanguage language)
        {
            string code = language == AppLanguage.English ? "en" : "vi";
            LocalizationManager.Instance.SetLanguage(code);
        }

        public static void SetLanguage(string languageCode)
        {
            LocalizationManager.Instance.SetLanguage(languageCode);
        }

        public static void ToggleLanguage()
        {
            SetLanguage(CurrentLanguage == AppLanguage.Vietnamese ? AppLanguage.English : AppLanguage.Vietnamese);
        }

        public static string Get(string key, params object[] args)
        {
            return LocalizationManager.Instance.GetString(key, args);
        }

        public static IReadOnlyList<LanguageInfo> GetAvailableLanguages()
        {
            return LocalizationManager.Instance.GetAvailableLanguages();
        }
    }
}
