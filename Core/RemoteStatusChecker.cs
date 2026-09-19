using System;
using System.Threading.Tasks;

using AWLM.Utilities;

namespace AWLM.Core
{
    /// <summary>
    /// Point-in-time AppLocker status for a remote host, mirroring what
    /// StatusChecker + ResolvePolicyState provide for localhost.
    /// </summary>
    public sealed class RemoteStatusSnapshot
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public bool AppIdSvcRunning { get; set; }
        public bool DllFilteringActive { get; set; }
        public PolicyState State { get; set; } = PolicyState.Unknown;
    }

    public static class RemoteStatusChecker
    {
        public static async Task<RemoteStatusSnapshot> GetSnapshotAsync(string machineName)
        {
            CommandResult result;
            try
            {
                result = await RemoteManagement.GetStatusSnapshotRemoteAsync(machineName);
            }
            catch (Exception ex)
            {
                return new RemoteStatusSnapshot { Success = false, Error = ex.Message };
            }

            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return new RemoteStatusSnapshot
                {
                    Success = false,
                    Error = !string.IsNullOrWhiteSpace(result.ErrorMessage) ? result.ErrorMessage : result.StandardError
                };
            }

            string output = result.StandardOutput.TrimEnd('\r', '\n');
            int markerIndex = output.IndexOf(RemoteManagement.SnapshotDelimiter, StringComparison.Ordinal);
            if (markerIndex < 0)
                return new RemoteStatusSnapshot { Success = false, Error = "Unexpected response from remote host." };

            string header = output.Substring(0, markerIndex);
            string xml = output.Substring(markerIndex + RemoteManagement.SnapshotDelimiter.Length).TrimStart('\r', '\n');

            string[] lines = header.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string svc = lines.Length > 0 ? lines[0].Trim() : "";
            string dll = lines.Length > 1 ? lines[1].Trim() : "";

            bool appIdSvcRunning = string.Equals(svc, "Running", StringComparison.OrdinalIgnoreCase);

            return new RemoteStatusSnapshot
            {
                Success = true,
                AppIdSvcRunning = appIdSvcRunning,
                DllFilteringActive = dll == "1" || dll == "2",
                // Mirrors ResolvePolicyState.DetectCurrentPolicy(): AppIDSvc down means Off
                // regardless of what the effective policy XML says.
                State = appIdSvcRunning ? ResolvePolicyState.CheckXml(xml) : PolicyState.Off
            };
        }
    }
}
