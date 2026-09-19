using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AWLM.Utilities
{
    public static class RemoteManagement
    {
        private const int DefaultTimeoutMs = 30_000;
        private const int GPUpdateTimeoutMs = 90_000;
        private const int LogQueryTimeoutMs = 120_000;
        private const int StatusSnapshotTimeoutMs = 30_000;
        private const int PolicyApplyTimeoutMs = 60_000;
        private const int ReachabilityTimeoutMs = 8_000;

        internal const string SnapshotDelimiter = "@@AWLM_SNAPSHOT_XML@@";

        public static Task<CommandResult> GetEffectivePolicyXmlRemoteAsync(string machineName) =>
            InvokeScriptAsync(machineName, "Get-AppLockerPolicy -Effective -Xml", DefaultTimeoutMs);

        public static Task<CommandResult> RunGPUpdateRemoteAsync(string machineName) =>
            InvokeScriptAsync(
                machineName,
                "'n' | & gpupdate.exe /force 2>&1 | Out-String",
                GPUpdateTimeoutMs);

        public static Task<CommandResult> GetEventChannelsXmlRemoteAsync(
            string machineName, IEnumerable<string> channels, string xpath)
        {
            // Single-quote the XPath; double up any embedded single quotes
            string safeXpath = PsLiteral(xpath);

            string wevtutilCalls = string.Join(" + ", channels.Select(c =>
                $"(& wevtutil.exe qe '{PsLiteral(c)}' '/q:{safeXpath}' /f:xml)"));

            string body = $"'<R>' + (({wevtutilCalls}) -join '') + '</R>'";

            return InvokeScriptAsync(machineName, body, LogQueryTimeoutMs);
        }

        /// <summary>
        /// Escapes a value for embedding in a single-quoted PowerShell string literal.
        /// Every interpolation into a script body must go through this.
        /// </summary>
        private static string PsLiteral(string value) => value?.Replace("'", "''");

        public static async Task<bool> TestReachabilityAsync(string machineName)
        {
            string safeMachine = PsLiteral(machineName);
            string script =
                "$ProgressPreference='SilentlyContinue'; " +
                $"try {{ $null = Test-WSMan -ComputerName '{safeMachine}' -ErrorAction Stop; 'REACHABLE' }} catch {{ 'UNREACHABLE' }}";

            CommandResult result = await RunPowerShellAsync(script, ReachabilityTimeoutMs);

            return result.Success
                && result.StandardOutput != null
                && result.StandardOutput.Trim().Equals("REACHABLE", StringComparison.OrdinalIgnoreCase);
        }

        public static Task<CommandResult> GetStatusSnapshotRemoteAsync(string machineName)
        {
            string body =
                "$svcObj = Get-Service -Name 'AppIDSvc' -ErrorAction SilentlyContinue; " +
                // String interpolation instead of an explicit .ToString() call due to Constrained Language Mode
                "$svc = if ($svcObj) { \"$($svcObj.Status)\" } else { 'NotFound' }; " +
                "$dllObj = Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\SrpV2\\Dll' -Name EnforcementMode -ErrorAction SilentlyContinue; " +
                "$dll = if ($dllObj) { $dllObj.EnforcementMode } else { '' }; " +
                "$xml = Get-AppLockerPolicy -Effective -Xml; " +
                $"\"$svc`n$dll`n{SnapshotDelimiter}`n$xml\"";

            return InvokeScriptAsync(machineName, body, StatusSnapshotTimeoutMs);
        }

        public static Task<CommandResult> ApplyPolicyXmlRemoteAsync(string machineName, string policyXml)
        {
            string safeXml = PsLiteral(policyXml);
            string body =
                "$tmp = Join-Path $env:TEMP 'srpmanager_applocker_policy.xml'; " +
                $"Set-Content -Path $tmp -Value '{safeXml}' -Encoding UTF8; " +
                "try { Set-AppLockerPolicy -XmlPolicy $tmp } finally { Remove-Item $tmp -ErrorAction SilentlyContinue }";

            return InvokeScriptAsync(machineName, body, PolicyApplyTimeoutMs);
        }

        private static Task<CommandResult> InvokeScriptAsync(string machineName, string remoteScriptBody, int timeoutMs)
        {
            string wrapped =
                "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; " +
                $"Invoke-Command -ComputerName '{PsLiteral(machineName)}' -ScriptBlock {{ {remoteScriptBody} }}";

            return RunPowerShellAsync(wrapped, timeoutMs);
        }

        private static Task<CommandResult> RunPowerShellAsync(string script, int timeoutMs)
        {
            return CommandExecution.ExecuteCommandAsync(
                "powershell.exe",
                "-NoProfile -NonInteractive -Command " + CommandExecution.QuoteArgument(script),
                timeoutMs: timeoutMs);
        }
    }
}
