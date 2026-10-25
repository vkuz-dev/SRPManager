using System;

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
            foreach (RegistryKey hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using (var key = hive.OpenSubKey(KeyPath))
                    {
                        object val = key?.GetValue(valueName);
                        if (val != null)
                            return Convert.ToInt32(val) != 0;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsRegistry] {hive.Name}\\{valueName}: {ex.Message}");
                }
            }

            return null;
        }
    }
}
