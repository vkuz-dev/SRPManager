using System;

namespace AWLM.Utilities
{
    public static class SettingsStartup
    {
        // ── Folder helpers ────────────────────────────────────────────────────

        private static string UserStartupFolder =>
            Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        private static string GlobalStartupFolder =>
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

        // ── Public API ────────────────────────────────────────────────────────

        public static bool IsStartupEnabled()
        {
            return ShellShortcut.ExistsIn(UserStartupFolder)
                || ShellShortcut.ExistsIn(GlobalStartupFolder);
        }

        public static bool IsGlobalStartupEnabled()
        {
            return ShellShortcut.ExistsIn(GlobalStartupFolder);
        }

        public static void SetStartup(bool enable)
        {
            // Always clear the per-user entry first, so a re-enable rewrites it cleanly
            // rather than leaving a stale shortcut pointing at an old install path.
            ShellShortcut.RemoveFrom(UserStartupFolder);

            // A machine-wide startup entry is administrator-deployed; adding a per-user copy
            // on top would only launch a second instance, so leave the global one to do its job.
            if (IsGlobalStartupEnabled())
                return;

            if (enable)
                ShellShortcut.CreateIn(UserStartupFolder);
        }
    }
}
