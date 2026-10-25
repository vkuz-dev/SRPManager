using System;

using Microsoft.Win32;

namespace AWLM.Utilities
{
    public static class SettingsLanguage
    {
        private const string RegKey = "Language";

        /// <summary>
        /// Loads the saved language, defaulting to "en" if nothing is stored.
        /// </summary>
        public static string Load() =>
            SettingsRegistry.ReadString(RegKey, IsValidLang) ?? "en";

        public static void Save(string lang)
        {
            if (!IsValidLang(lang))
                lang = "en";

            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsRegistry.KeyPath))
                {
                    key.SetValue(RegKey, lang, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[LangPreference] Save failed: " + ex.Message);
            }
        }

        private static bool IsValidLang(string lang)
        {
            return lang == "en" || lang == "lv" || lang == "ru";
        }
    }
}
