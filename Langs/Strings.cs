using System;
using System.Collections.Generic;

namespace AWLM.Core
{
    public static partial class Strings
    {
        private static string _lang = "en";

        public static string Lang
        {
            get => _lang;
            set
            {
                if (string.Equals(_lang, value, StringComparison.OrdinalIgnoreCase))
                    return;

                _lang = value?.ToLowerInvariant() ?? "en";
                LanguageChanged?.Invoke(null, EventArgs.Empty);
            }
        }
        public static event EventHandler LanguageChanged;

        public static string Get(string key)
        {
            if (_table.TryGetValue(_lang, out var table) && table.TryGetValue(key, out string val))
                return val;

            // Fall back to English
            if (_table.TryGetValue("en", out table) && table.TryGetValue(key, out val))
                return val;

            // Last resort: make missing keys visible during development only
#if DEBUG
            return "[" + key + "]";
#else
            return key;
#endif
        }

        // String tables
        private static readonly Dictionary<string, Dictionary<string, string>> _table =
            new Dictionary<string, Dictionary<string, string>>
            {
                { "en", En() },
                { "lv", Lv() },
                { "ru", Ru() }
            };
    }
}
