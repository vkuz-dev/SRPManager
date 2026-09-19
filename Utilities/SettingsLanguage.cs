using System;

using Microsoft.Win32;

namespace AWLM.Utilities
{
    public static class SettingsLanguage
    {
        private const string RegPath = @"SOFTWARE\SRPManager";
        private const string RegKey = "Language";

        /// <summary>
        /// Loads the saved language, defaulting to "en" if nothing is stored.
        /// </summary>
        public static string Load()
        {
            try
            {
                // 1. User preference (HKCU wins)
                using (var key = Registry.CurrentUser.OpenSubKey(RegPath))
                {
                    string val = key?.GetValue(RegKey) as string;
                    if (IsValidLang(val))
                        return val;
                }

                // 2. System default (HKLM fallback)
                using (var key = Registry.LocalMachine.OpenSubKey(RegPath))
                {
                    string val = key?.GetValue(RegKey) as string;
                    if (IsValidLang(val))
                        return val;
                }

                // 3. Hard default
                return "en";
            }
            catch
            {
                return "en";
            }
        }

        public static void Save(string lang)
        {
            if (!IsValidLang(lang))
                lang = "en";

            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegPath))
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
