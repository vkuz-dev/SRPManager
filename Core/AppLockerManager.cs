using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

using AWLM.Utilities;
namespace AWLM.Core
{
    public static class AppLockerManager
    {
        public static readonly Dictionary<string, string> Policies = new Dictionary<string, string>
        {
            { "Enable AppLocker",  "AppLocker-Enable.xml"  },
            { "Disable AppLocker", "AppLocker-Disable.xml" },
        };

        public sealed class ApplyPolicyResult
        {
            public bool Success { get; }
            public string Error { get; }

            /// <summary>True when the freeze lock blocked the apply, which gets its own dialog.</summary>
            public bool Frozen { get; }

            public ApplyPolicyResult(bool success, string error, bool frozen = false)
            {
                Success = success;
                Error = error;
                Frozen = frozen;
            }
        }

        /// <summary>
        /// True when the policy turns AppLocker on. The freeze lock only guards enabling —
        /// disabling stays available so an operator is never locked out of turning it off.
        /// </summary>
        private static bool IsEnablePolicy(string resourceFileName) =>
            resourceFileName != null &&
            resourceFileName.StartsWith("AppLocker-Enable", StringComparison.OrdinalIgnoreCase);

        /// <param name="resourceFileName">Policy file name, resolved from disk or embedded resources.</param>
        /// <param name="force">Bypasses the freeze lock. Set by the CLI's -force switch.</param>
        public static async Task<ApplyPolicyResult> ApplyPolicyAsync(string resourceFileName, bool force = false)
        {
            if (!StatusChecker.IsProcessElevated())
            {
                return new ApplyPolicyResult(false,
                    "Administrator privileges are required to apply AppLocker policies.");
            }

            if (!force && IsEnablePolicy(resourceFileName) && MutexManager.IsMutexActive())
            {
                return new ApplyPolicyResult(false,
                    "AppLocker is frozen. Unfreeze it first, or use -force from the command line.",
                    frozen: true);
            }

            PolicyState stateBeforeApply = await ResolvePolicyState.DetectCurrentPolicyAsync();

            string xml = PolicyStore.ResolvePolicy(resourceFileName, out string resolveError);
            if (xml == null)
                return new ApplyPolicyResult(false, resolveError);

            string tempFile = null;

            try
            {
                tempFile = PolicyStore.AdminTempPath($"applocker_policy_{Guid.NewGuid():N}.xml");
                File.WriteAllText(tempFile, xml, Encoding.UTF8);

                CommandResult cmdResult = await CommandExecution.ExecuteCommandAsync(
                    "powershell.exe",
                    $"-NoProfile -Command \"Set-AppLockerPolicy -XmlPolicy '{tempFile}'\""
                );

                if (!cmdResult.Success)
                {
                    string error = !string.IsNullOrWhiteSpace(cmdResult.StandardError)
                        ? cmdResult.StandardError.Trim()
                        : cmdResult.StandardOutput;
                    Debug.WriteLine("[AppLockerPolicy] ApplyPolicyAsync error: " + error);
                    return new ApplyPolicyResult(false, error);
                }

                // Verify that something has changed before writing to the log
                PolicyState stateAfterApply = stateBeforeApply;
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(1000);

                    var current = await ResolvePolicyState.DetectCurrentPolicyAsync();
                    if (current != PolicyState.Unknown)
                        stateAfterApply = current;

                    if (stateAfterApply != stateBeforeApply)
                        break;
                }

                if (stateAfterApply != PolicyState.Unknown && stateAfterApply != stateBeforeApply)
                    WriteAuditLog(stateBeforeApply, stateAfterApply);

                return new ApplyPolicyResult(true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppLockerPolicy] ApplyPolicyAsync exception: " + ex.Message);
                return new ApplyPolicyResult(false, ex.Message);
            }
            finally
            {
                // Don't leave a copy of the policy behind per apply.
                try { if (tempFile != null) File.Delete(tempFile); }
                catch (Exception ex)
                {
                    Debug.WriteLine("[AppLockerPolicy] temp policy cleanup failed: " + ex.Message);
                }
            }
        }

        public static async Task<ApplyPolicyResult> EnableAppIDSvcAsync()
        {
            if (!StatusChecker.IsProcessElevated())
            {
                return new ApplyPolicyResult(false,
                    "Administrator privileges are required to enable the Application Identity service.");
            }

            try
            {
                //same as running `sc config appidsvc start= auto` manually
                CommandResult configResult = await CommandExecution.ExecuteCommandAsync(
                    "sc.exe", "config AppIDSvc start= auto");

                if (!configResult.Success)
                {
                    string error = !string.IsNullOrWhiteSpace(configResult.StandardError)
                        ? configResult.StandardError.Trim()
                        : configResult.StandardOutput;
                    Debug.WriteLine("[AppLockerPolicy] EnableAppIDSvcAsync config error: " + error);
                    return new ApplyPolicyResult(false, error);
                }

                CommandResult startResult = await CommandExecution.ExecuteCommandAsync(
                    "sc.exe", "start AppIDSvc", validExitCodes: new[] { 0, 1056 }); // 1056 = already running

                if (!startResult.Success)
                {
                    string error = !string.IsNullOrWhiteSpace(startResult.StandardError)
                        ? startResult.StandardError.Trim()
                        : startResult.StandardOutput;
                    Debug.WriteLine("[AppLockerPolicy] EnableAppIDSvcAsync start error: " + error);
                    return new ApplyPolicyResult(false, error);
                }

                return new ApplyPolicyResult(true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppLockerPolicy] EnableAppIDSvcAsync exception: " + ex.Message);
                return new ApplyPolicyResult(false, ex.Message);
            }
        }

        public static async Task<ApplyPolicyResult> ApplyPolicyRemoteAsync(string resourceFileName, string machineName)
        {
            PolicyState stateBeforeApply = (await RemoteStatusChecker.GetSnapshotAsync(machineName)).State;

            string xml = PolicyStore.ResolvePolicy(resourceFileName, out string resolveError);
            if (xml == null)
                return new ApplyPolicyResult(false, resolveError);

            try
            {
                CommandResult cmdResult = await RemoteManagement.ApplyPolicyXmlRemoteAsync(machineName, xml);

                if (!cmdResult.Success)
                {
                    string error = !string.IsNullOrWhiteSpace(cmdResult.StandardError)
                        ? cmdResult.StandardError.Trim()
                        : (!string.IsNullOrWhiteSpace(cmdResult.ErrorMessage) ? cmdResult.ErrorMessage : cmdResult.StandardOutput);
                    Debug.WriteLine("[AppLockerPolicy] ApplyPolicyRemoteAsync error: " + error);
                    return new ApplyPolicyResult(false, error);
                }

                // Verify that something has changed before writing to the log
                PolicyState stateAfterApply = stateBeforeApply;
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(1000);

                    RemoteStatusSnapshot snapshot = await RemoteStatusChecker.GetSnapshotAsync(machineName);
                    if (snapshot.Success && snapshot.State != PolicyState.Unknown)
                        stateAfterApply = snapshot.State;

                    if (stateAfterApply != stateBeforeApply)
                        break;
                }

                if (stateAfterApply != PolicyState.Unknown && stateAfterApply != stateBeforeApply)
                    WriteAuditLog(stateBeforeApply, stateAfterApply, machineName);

                // e.g. the NotConfigured policy on a target with no domain AppLocker GPO
                if (stateBeforeApply != PolicyState.Unknown && stateAfterApply == stateBeforeApply)
                    return new ApplyPolicyResult(false,
                        $"The policy was applied, but the state hasn't changed (still {stateAfterApply}). " +
                        "Is AppLocker healthy on the target?");

                return new ApplyPolicyResult(true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppLockerPolicy] ApplyPolicyRemoteAsync exception: " + ex.Message);
                return new ApplyPolicyResult(false, ex.Message);
            }
        }

        public static string ResolveEnablePolicyFileName(bool isRemoteTarget = false) =>
            (isRemoteTarget || StatusChecker.IsDomainMode())
                ? "AppLocker-EnableNotConfigured.xml"
                : "AppLocker-Enable.xml";


        // Audit logging
        private const string EventSource = "SRPManager";
        private const int EventIdEnabled = 1000;
        private const int EventIdDisabled = 1001;

        /// <summary>
        /// Logs any state change: 1000 when the new state is On, 1001 for anything weaker.
        /// </summary>
        private static void WriteAuditLog(PolicyState before, PolicyState after, string targetMachine = null)
        {
            try
            {
                // A freshly created source is usable right away - don't drop the first event.
                if (!EventLog.SourceExists(EventSource))
                    EventLog.CreateEventSource(EventSource, "Application");

                string user = Environment.UserName;
                string sid = WindowsIdentity.GetCurrent().User?.Value ?? "N/A";
                bool enabled = after == PolicyState.On;
                string action = enabled ? "Enabled" : "Disabled";
                int eventId = enabled ? EventIdEnabled : EventIdDisabled;

                string message =
                    $"AppLocker was {action} via SRPManager ({before} -> {after}).\r\n" +
                    $"User:    {user}\r\n" +
                    $"SID:     {sid}\r\n" +
                    $"Machine: {Environment.MachineName}\r\n" +
                    (targetMachine != null ? $"Target:  {targetMachine}\r\n" : "") +
                    $"Time:    {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                EventLog.WriteEntry(EventSource, message, EventLogEntryType.Information, eventId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppLockerPolicy] WriteAuditLog failed: " +
                                ex.GetType().Name + " — " + ex.Message);
            }
        }
    }
}
