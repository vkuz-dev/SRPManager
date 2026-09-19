using System;
using System.IO;
using System.Reflection;

using Microsoft.Win32;

namespace AWLM.Utilities
{
    public static class SettingsTrayPin
    {
        private const string NotifyIconRegPath = @"Control Panel\NotifyIconSettings";
        private const string RegKeyPath = @"SOFTWARE\SRPManager";
        private const string PinValueName = "PinToTray";

        // ── Public API ────────────────────────────────────────────────────────
        public static bool IsPinEnabled()
        {
            // HKCU
            using (var key = Registry.CurrentUser.OpenSubKey(RegKeyPath))
            {
                object val = key?.GetValue(PinValueName);
                if (val != null)
                    return Convert.ToInt32(val) != 0;
            }

            // HKLM 
            using (var key = Registry.LocalMachine.OpenSubKey(RegKeyPath))
            {
                object val = key?.GetValue(PinValueName);
                if (val != null)
                    return Convert.ToInt32(val) != 0;
            }

            return false;
        }

        public static void SaveTrayPin(bool enable)
        {
            // Persist user preference
            using (var key = Registry.CurrentUser.CreateSubKey(RegKeyPath))
                key?.SetValue(PinValueName, enable ? 1 : 0, RegistryValueKind.DWord);

            // Apply to the live NotifyIconSettings entries
            ApplyToNotifyIconSettings(enable ? 1 : 0);
        }

        public static void ApplyTrayPin()
        {
            // Always apply the effective state: 1 if pinned, 0 if not
            ApplyToNotifyIconSettings(IsPinEnabled() ? 1 : 0);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static void ApplyToNotifyIconSettings(int promoted)
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            string exeFile = Path.GetFileName(exePath).ToLowerInvariant();

            try
            {
                using (var root = Registry.CurrentUser.OpenSubKey(
                    NotifyIconRegPath, writable: true))
                {
                    if (root == null) return;   // key won't exist until the app has been run at least once

                    foreach (string subName in root.GetSubKeyNames())
                    {
                        using (var sub = root.OpenSubKey(subName, writable: true))
                        {
                            if (sub == null) continue;

                            string regExePath = sub.GetValue("ExecutablePath") as string;
                            if (string.IsNullOrEmpty(regExePath)) continue;

                            if (string.Equals(
                                    Path.GetFileName(regExePath),
                                    exeFile,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                sub.SetValue("IsPromoted", promoted, RegistryValueKind.DWord);
                            }
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // NotifyIconSettings is always HKCU so this shouldn't happen,
                // but swallow it gracefully rather than crashing the app.
            }
        }
    }
}
