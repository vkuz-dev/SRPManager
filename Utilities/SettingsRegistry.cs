using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Win32;

namespace AWLM.Utilities
{
    /// <summary>
    /// Reads SRPManager settings: HKCU first, then HKLM (fleet defaults).
    /// Never throws — a malformed value deployed by GPO must not crash the tray at startup.
    /// </summary>
    public static class SettingsRegistry
    {
        public const string KeyPath = @"SOFTWARE\SRPManager";

        /// <summary>
        /// True/false from the first hive holding a parseable value, or null if neither does.
        /// </summary>
        public static bool? ReadFlag(string valueName)
        {
            foreach (object val in ReadAll(valueName))
            {
                try { return Convert.ToInt32(val) != 0; }
                catch (Exception) { /* malformed — fall through to the next hive */ }
            }

            return null;
        }

        /// <summary>The first string value accepted by <paramref name="isValid"/>, or null.</summary>
        public static string ReadString(string valueName, Func<string, bool> isValid) =>
            ReadAll(valueName).OfType<string>().FirstOrDefault(isValid);

        private static List<object> ReadAll(string valueName)
        {
            var values = new List<object>();
            foreach (RegistryKey hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using (var key = hive.OpenSubKey(KeyPath))
                    {
                        object val = key?.GetValue(valueName);
                        if (val != null)
                            values.Add(val);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsRegistry] {hive.Name}\\{valueName}: {ex.Message}");
                }
            }
            return values;
        }
    }
}
