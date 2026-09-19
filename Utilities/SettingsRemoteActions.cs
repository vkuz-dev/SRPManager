using System;

using Microsoft.Win32;

namespace AWLM.Utilities
{
    public static class SettingsRemoteManagement
    {
        private const string RegKeyPath = @"SOFTWARE\SRPManager";
        private const string ValueName = "EnableRemoteManagement";

        public static bool IsRemoteManagementEnabled()
        {
            // HKCU
            using (var key = Registry.CurrentUser.OpenSubKey(RegKeyPath))
            {
                object val = key?.GetValue(ValueName);
                if (val != null)
                    return Convert.ToInt32(val) != 0;
            }

            // HKLM
            using (var key = Registry.LocalMachine.OpenSubKey(RegKeyPath))
            {
                object val = key?.GetValue(ValueName);
                if (val != null)
                    return Convert.ToInt32(val) != 0;
            }

            return false;
        }

        public static void SaveRemoteManagementEnabled(bool enable)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RegKeyPath))
                key?.SetValue(ValueName, enable ? 1 : 0, RegistryValueKind.DWord);
        }
    }
}
