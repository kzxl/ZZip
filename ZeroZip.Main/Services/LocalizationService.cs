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
        private const string SettingsKey = @"Software\ZeroUniverse\ZeroZip";

        static LocalizationService()
        {
            LocalizationManager.Instance.LanguageChanged += _ => LanguageChanged?.Invoke();

            string saved = LoadSavedLanguage();
            if (!string.IsNullOrEmpty(saved))
            {
                LocalizationManager.Instance.SetLanguage(saved);
            }
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
            SetLanguage(code);
        }

        public static void SetLanguage(string languageCode)
        {
            if (LocalizationManager.Instance.SetLanguage(languageCode))
            {
                SaveLanguage(languageCode);
            }
        }

        private static string LoadSavedLanguage()
        {
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SettingsKey);
                return k?.GetValue("Language") as string ?? "vi";
            }
            catch
            {
                return "vi";
            }
        }

        private static void SaveLanguage(string code)
        {
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(SettingsKey);
                k?.SetValue("Language", code);
            }
            catch { }
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
