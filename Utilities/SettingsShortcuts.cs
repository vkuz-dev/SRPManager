using System;
using System.Diagnostics;

namespace AWLM.Utilities
{
    /// <summary>
    /// Desktop and Start menu shortcuts for SRPManager.
    ///
    /// Each location is checked in both the per-user and the all-users folder, but only ever
    /// written per-user: the all-users folders need administrator rights, and a shortcut an
    /// administrator deployed there is not ours to delete. When only an all-users shortcut
    /// exists the toggle reports "managed" so the UI can show it as present but read-only.
    /// </summary>
    public static class SettingsShortcuts
    {
        private const string ShortcutDescription = "SRPManager - AppLocker Management Utility";

        private static string UserDesktop =>
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        private static string CommonDesktop =>
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

        private static string UserStartMenu =>
            Environment.GetFolderPath(Environment.SpecialFolder.Programs);

        private static string CommonStartMenu =>
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);

        // ── Desktop ───────────────────────────────────────────────────────────

        public static bool IsDesktopShortcutPresent() =>
            ShellShortcut.ExistsIn(UserDesktop) || ShellShortcut.ExistsIn(CommonDesktop);

        public static bool IsDesktopShortcutManaged() =>
            !ShellShortcut.ExistsIn(UserDesktop) && ShellShortcut.ExistsIn(CommonDesktop);

        public static bool SetDesktopShortcut(bool enable) =>
            Apply(UserDesktop, CommonDesktop, enable, "desktop");

        // ── Start menu ────────────────────────────────────────────────────────

        public static bool IsStartMenuShortcutPresent() =>
            ShellShortcut.ExistsIn(UserStartMenu) || ShellShortcut.ExistsIn(CommonStartMenu);

        public static bool IsStartMenuShortcutManaged() =>
            !ShellShortcut.ExistsIn(UserStartMenu) && ShellShortcut.ExistsIn(CommonStartMenu);

        public static bool SetStartMenuShortcut(bool enable) =>
            Apply(UserStartMenu, CommonStartMenu, enable, "start menu");

        // ── Shared ────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds or removes the per-user shortcut. Returns false if the request could not be
        /// honoured — either an all-users shortcut owns the slot, or the write failed.
        /// </summary>
        private static bool Apply(string userFolder, string commonFolder, bool enable, string label)
        {
            // An all-users shortcut is administrator-deployed; leave it alone either way.
            if (!ShellShortcut.ExistsIn(userFolder) && ShellShortcut.ExistsIn(commonFolder))
                return false;

            try
            {
                if (enable)
                    ShellShortcut.CreateIn(userFolder, ShortcutDescription);
                else
                    ShellShortcut.RemoveFrom(userFolder);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsShortcuts] {label} shortcut ({(enable ? "create" : "remove")}): {ex.Message}");
                return false;
            }
        }
    }
}
