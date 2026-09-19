using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.ServiceProcess;

using Microsoft.Win32;

namespace AWLM.Core
{
    public static class StatusChecker
    {
        /// <summary>
        /// True when the process actually holds administrative rights.
        /// </summary>
        /// <remarks>
        /// Deliberately not GetTokenInformation(TokenElevation): that reports whether the token
        /// is a *filtered* one, which is only meaningful while UAC is on. With UAC disabled
        /// (EnableLUA=0) Windows never splits tokens, so TokenElevation reports "elevated" for
        /// every process — including a standard user with no rights at all. Checking for the
        /// Administrators SID answers the question we actually care about, and is correct
        /// whether UAC is on or off: a split-token admin running unelevated does not carry
        /// that SID, while a genuinely elevated process does.
        /// </remarks>
        public static bool IsProcessElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(identity)
                        .IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemPolicyChecker] IsProcessElevated: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// The account the process is running as, as DOMAIN\User (or MACHINE\User when not
        /// domain-joined). Falls back to the environment variables if the token is unreadable.
        /// </summary>
        public static string GetCurrentUserName()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    if (!string.IsNullOrEmpty(identity.Name))
                        return identity.Name;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemPolicyChecker] GetCurrentUserName: {ex.Message}");
            }

            string domain = Environment.UserDomainName;
            string user = Environment.UserName;

            return string.IsNullOrEmpty(domain) ? user : domain + "\\" + user;
        }

        private const string SRPRegistryKey = @"SOFTWARE\SRPManager";
        private const string DllPolicyKey = @"SOFTWARE\Policies\Microsoft\Windows\SrpV2\Dll";
        private const string UACRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

        public static bool IsDomainMode()
        {
            try
            {
                return ReadIntFromLocalMachine(SRPRegistryKey, "DomainMode") == 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemPolicyChecker] IsDomainMode: {ex.Message}");
                return false;
            }
        }

        public static bool IsAppIDSvcRunning()
        {
            try
            {
                using (ServiceController sc = new ServiceController("AppIDSvc"))
                {
                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch (InvalidOperationException)
            {
                return false; // service not found or inaccessible
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemPolicyChecker] IsAppIDSvcRunning: {ex.Message}");
                return false;
            }
        }

        public static bool IsDllFilteringActive()
        {
            try
            {
                int mode = ReadIntFromLocalMachine(DllPolicyKey, "EnforcementMode");
                return mode == 1 || mode == 2;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemPolicyChecker] IsDllFilteringActive: {ex.Message}");
                return false;
            }
        }

        public static bool IsUACActive()
        {
            try
            {
                return ReadIntFromLocalMachine(UACRegistryKey, "EnableLUA") == 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemPolicyChecker] IsUACActive: {ex.Message}");
                return false;
            }
        }

        public static bool IsLocalFilesDeployed()
        {
            try
            {
                string folder = PolicyStore.PoliciesFolder;

                if (!Directory.Exists(folder))
                    return false;

                string disablePath = Path.Combine(folder, "AppLocker-Disable.xml");
                string enablePath = Path.Combine(folder, "AppLocker-Enable.xml");

                return File.Exists(disablePath) && File.Exists(enablePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemPolicyChecker] LocalFilesDeployed: {ex.Message}");
                return false;
            }
        }

        private static int ReadIntFromLocalMachine(string subKey, string valueName, int defaultValue = 0)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subKey))
            {
                if (key == null)
                    return defaultValue;

                object value = key.GetValue(valueName, defaultValue);

                if (value == null)
                    return defaultValue;

                try
                {
                    return Convert.ToInt32(value);
                }
                catch
                {
                    return defaultValue;
                }
            }
        }
    }
}
