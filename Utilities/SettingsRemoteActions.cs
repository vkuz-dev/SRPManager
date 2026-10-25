using Microsoft.Win32;

namespace AWLM.Utilities
{
    public static class SettingsRemoteManagement
    {
        private const string ValueName = "EnableRemoteManagement";

        public static bool IsRemoteManagementEnabled() =>
            SettingsRegistry.ReadFlag(ValueName) ?? false;

        public static void SaveRemoteManagementEnabled(bool enable)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsRegistry.KeyPath))
                key?.SetValue(ValueName, enable ? 1 : 0, RegistryValueKind.DWord);
        }
    }
}
