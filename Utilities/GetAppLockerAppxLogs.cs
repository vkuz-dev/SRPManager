using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace AWLM.Utilities
{
    /// <summary>
    /// Reads AppLocker Packaged App (APPX) events from the Windows Event Log.
    /// Handles both the Deployment channel (install/update/removal, IDs 8020–8025)
    /// and the Execution channel (launch attempts, IDs 8021–8022), which carry a
    /// Package name + FQBN instead of a file path.
    ///
    /// Local reads use the live Event Log reader APIs. Remote reads go through WinRM
    /// (PowerShell Remoting via <see cref="RemoteManagement"/>), same as <see cref="GetAppLockerFileLogs"/>.
    /// </summary>
    public static class GetAppLockerAppxLogs
    {
        private static readonly string[] LogChannels =
        {
            "Microsoft-Windows-AppLocker/Packaged app-Deployment",
            "Microsoft-Windows-AppLocker/Packaged app-Execution"
        };

        private static readonly HashSet<int> WatchedEventIds =
            new HashSet<int> { 8020, 8021, 8022, 8023, 8024, 8025, 8027 };

        // ── Public API ──────────────────────────────────────────────────────────

        public static List<AppLockerAppxEvent> GetEvents(
            int timeframeDays,
            string machineName = null,
            CancellationToken ct = default,
            IProgress<ParseProgress> progress = null)
        {
            bool isRemote = !string.IsNullOrWhiteSpace(machineName);
            return isRemote
                ? GetEventsRemote(timeframeDays, machineName, ct, progress)
                : GetEventsLocal(timeframeDays, ct, progress);
        }

        // ── Local query ─────────────────────────────────────────────────────────

        private static List<AppLockerAppxEvent> GetEventsLocal(
            int timeframeDays,
            CancellationToken ct,
            IProgress<ParseProgress> progress)
        {
            var results = new List<AppLockerAppxEvent>();
            DateTime since = DateTime.UtcNow.AddDays(-timeframeDays);
            int eventsRead = 0;

            foreach (string channel in LogChannels)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new ParseProgress { Channel = channel, EventsRead = eventsRead });

                try
                {
                    var query = new EventLogQuery(channel, PathType.LogName, BuildXPath(since));
                    using (var reader = new EventLogReader(query))
                    {
                        EventRecord record;
                        while ((record = reader.ReadEvent()) != null)
                        {
                            ct.ThrowIfCancellationRequested();
                            using (record)
                            {
                                try
                                {
                                    if (WatchedEventIds.Contains(record.Id))
                                    {
                                        results.Add(ParseEvent(record));
                                        if (++eventsRead % 100 == 0)
                                            progress?.Report(new ParseProgress { Channel = channel, EventsRead = eventsRead });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine("[AppxEventLog] Skipping event: " + ex.Message);
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (EventLogNotFoundException)
                {
                    Debug.WriteLine("[AppxEventLog] Channel not found (local): " + channel);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[AppxEventLog] Error reading (local) " + channel + ": " + ex.Message);
                }
            }

            results.Sort((a, b) => b.TimeCreated.CompareTo(a.TimeCreated));
            return results;
        }

        // ── Remote query ────────────────────────────────────────────────────────

        private static List<AppLockerAppxEvent> GetEventsRemote(
            int timeframeDays,
            string machineName,
            CancellationToken ct,
            IProgress<ParseProgress> progress)
        {
            var results = new List<AppLockerAppxEvent>();
            DateTime since = DateTime.UtcNow.AddDays(-timeframeDays);
            string xpath = BuildXPath(since);

            ct.ThrowIfCancellationRequested();
            progress?.Report(new ParseProgress { Channel = "AppLocker Packaged App logs", EventsRead = 0 });

            // Both channels are queried in a single Invoke-Command round trip rather than
            // one call per channel — fewer WinRM round trips, and one slow channel can no
            // longer starve the timeout budget of the other.
            CommandResult result = RemoteManagement
                .GetEventChannelsXmlRemoteAsync(machineName, LogChannels, xpath)
                .GetAwaiter().GetResult();

            if (!result.Success)
                throw new InvalidOperationException(
                    $"Failed to query '{machineName}':\n{result.ErrorMessage}" +
                    (string.IsNullOrWhiteSpace(result.StandardError) ? "" : $"\n{result.StandardError}"));

            ct.ThrowIfCancellationRequested();

            int eventsRead = 0;
            foreach (var eventEl in ParseEventBatch(result.StandardOutput))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    int id = GetEventId(eventEl);
                    if (WatchedEventIds.Contains(id))
                    {
                        results.Add(ParseEventXml(eventEl, id, machineName));
                        if (++eventsRead % 100 == 0)
                            progress?.Report(new ParseProgress { Channel = "AppLocker Packaged App logs", EventsRead = eventsRead });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[AppxEventLog] Skipping remote event: " + ex.Message);
                }
            }

            results.Sort((a, b) => b.TimeCreated.CompareTo(a.TimeCreated));
            return results;
        }

        // ── Remote XML parsing ───────────────────────────────────────────────────

        /// <summary>Splits the synthetic &lt;R&gt; batch returned by RemoteManagement into individual &lt;Event&gt; elements.</summary>
        private static List<XElement> ParseEventBatch(string batchXml)
        {
            if (string.IsNullOrWhiteSpace(batchXml)) return new List<XElement>();

            try
            {
                return XElement.Parse(batchXml)
                    .Elements()
                    .Where(e => e.Name.LocalName == "Event")
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppxEventLog] Failed to parse remote event batch: " + ex.Message);
                return new List<XElement>();
            }
        }

        private static int GetEventId(XElement eventEl) =>
            int.TryParse(GetChildText(eventEl, "EventID"), out int id) ? id : -1;

        private static AppLockerAppxEvent ParseEventXml(XElement eventEl, int eventId, string fallbackComputer)
        {
            DateTime.TryParse(
                GetAttr(eventEl, "TimeCreated", "SystemTime"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime timeCreated);

            string computer = GetChildText(eventEl, "Computer") ?? fallbackComputer;
            string policyName = GetChildText(eventEl, "PolicyName");
            string ruleName = GetChildText(eventEl, "RuleName");
            string packageName = GetChildText(eventEl, "Package");
            string fqbn = GetChildText(eventEl, "Fqbn");
            string logName = GetChildText(eventEl, "Channel");

            string user = SidResolver.Resolve(GetChildText(eventEl, "TargetUser"))
                ?? SidResolver.Resolve(GetAttr(eventEl, "Security", "UserID"));

            string publisher = null;
            string packageVersion = null;
            if (!string.IsNullOrEmpty(fqbn))
                ParseFqbn(fqbn, out publisher, out packageVersion);

            return new AppLockerAppxEvent
            {
                TimeCreated = timeCreated,
                EventId = eventId,
                Action = ClassifyEventId(eventId),
                Channel = ClassifyChannel(logName),
                User = user ?? "N/A",
                PackageName = packageName ?? "N/A",
                Publisher = publisher ?? "N/A",
                PackageVersion = packageVersion ?? "N/A",
                Fqbn = fqbn ?? "N/A",
                PolicyName = policyName ?? "N/A",
                RuleName = ruleName ?? "N/A",
                TargetComputer = computer
            };
        }

        /// <summary>Finds a descendant element by local name (ignoring namespace) and returns its text.</summary>
        private static string GetChildText(XElement parent, string localName) =>
            parent.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

        /// <summary>Finds a descendant element by local name and returns one of its attributes by local name.</summary>
        private static string GetAttr(XElement parent, string elementLocalName, string attrLocalName) =>
            parent.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == elementLocalName)
                ?.Attributes().FirstOrDefault(a => a.Name.LocalName == attrLocalName)?.Value;

        // ── Local parsing ─────────────────────────────────────────────────────────

        private static AppLockerAppxEvent ParseEvent(EventRecord record)
        {
            string packageName = null;
            string fqbn = null;
            string publisher = null;
            string packageVersion = null;
            string policyName = null;
            string ruleName = null;
            string user = null;
            string computer = null;

            try
            {
                var props = ((EventLogRecord)record).GetPropertyValues(AppLockerSelectors.AppxSelector);

                // Computer comes from the event itself — reliable even for remote reads.
                computer = props[AppLockerSelectors.Computer] as string;
                policyName = props[AppLockerSelectors.PolicyName] as string;
                ruleName = props[AppLockerSelectors.RuleName] as string;
                packageName = props[AppLockerSelectors.Package] as string;
                fqbn = props[AppLockerSelectors.AppxFqbn] as string;

                // TargetUser is element text → string; Security/@UserID → SecurityIdentifier object.
                string targetSid = SidResolver.ExtractSid(props[AppLockerSelectors.TargetUser]);
                if (!string.IsNullOrEmpty(targetSid))
                    user = SidResolver.Resolve(targetSid);

                if (user == null)
                {
                    string systemSid = SidResolver.ExtractSid(props[AppLockerSelectors.UserID]);
                    user = SidResolver.Resolve(systemSid);
                }

                if (!string.IsNullOrEmpty(fqbn))
                    ParseFqbn(fqbn, out publisher, out packageVersion);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppxEventLog] Property read error: {ex.Message}");
            }

            computer ??= record.MachineName ?? Environment.MachineName;

            return new AppLockerAppxEvent
            {
                TimeCreated = record.TimeCreated ?? DateTime.MinValue,
                EventId = record.Id,
                Action = ClassifyEventId(record.Id),
                Channel = ClassifyChannel(record.LogName),
                User = user ?? "N/A",
                PackageName = packageName ?? "N/A",
                Publisher = publisher ?? "N/A",
                PackageVersion = packageVersion ?? "N/A",
                Fqbn = fqbn ?? "N/A",
                PolicyName = policyName ?? "N/A",
                RuleName = ruleName ?? "N/A",
                TargetComputer = computer
            };
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static string BuildXPath(DateTime since) =>
            string.Format(
                "*[System[TimeCreated[@SystemTime>='{0}']]]",
                since.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"));

        /// <summary>
        /// Extracts publisher (CN= subject) and version from an APPX FQBN string.
        /// FQBN format: "CN=PUBLISHER, O=...\PACKAGE_NAME\APPX\1.2.3.4"
        /// </summary>
        private static void ParseFqbn(string fqbn, out string publisher, out string version)
        {
            publisher = null;
            version = null;

            var parts = fqbn.Split('\\');

            if (parts.Length >= 1)
            {
                string dn = parts[0];
                int cnStart = dn.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
                if (cnStart >= 0)
                {
                    int cnEnd = dn.IndexOf(',', cnStart);
                    publisher = cnEnd > cnStart
                        ? dn.Substring(cnStart + 3, cnEnd - cnStart - 3).Trim()
                        : dn.Substring(cnStart + 3).Trim();
                }
            }

            if (parts.Length >= 4)
                version = parts[parts.Length - 1].Trim();
        }

        private static string ClassifyEventId(int id)
        {
            if (id == 8020 || id == 8023) return "Allowed";
            if (id == 8022 || id == 8025) return "Blocked (Enforced)";
            if (id == 8021 || id == 8024) return "Blocked (Audit mode)";
            if (id == 8027) return "No Rule Match";
            return "Unknown";
        }

        private static string ClassifyChannel(string logName)
        {
            if (string.IsNullOrEmpty(logName)) return "";
            if (logName.IndexOf("Execution", StringComparison.OrdinalIgnoreCase) >= 0) return "Execution";
            if (logName.IndexOf("Deployment", StringComparison.OrdinalIgnoreCase) >= 0) return "Deployment";
            return logName.Split('/').Last();
        }

        // ── Summary ─────────────────────────────────────────────────────────────

        public static List<AppLockerAppxEventSummary> GetSummary(List<AppLockerAppxEvent> events)
        {
            return events
                .GroupBy(e => new { e.PackageName, e.Action })
                .Select(g => new AppLockerAppxEventSummary
                {
                    PackageName = g.Key.PackageName,
                    Action = g.Key.Action,
                    Count = g.Count()
                })
                .OrderByDescending(s => s.Count)
                .ToList();
        }
    }

    // ── Data classes ─────────────────────────────────────────────────────────────

    public class AppLockerAppxEvent
    {
        public DateTime TimeCreated { get; set; }
        public int EventId { get; set; }
        public string Action { get; set; }
        /// <summary>"Deployment" or "Execution"</summary>
        public string Channel { get; set; }
        public string User { get; set; }
        public string PackageName { get; set; }
        public string Publisher { get; set; }
        public string PackageVersion { get; set; }
        public string Fqbn { get; set; }
        public string PolicyName { get; set; }
        public string RuleName { get; set; }
        public string TargetComputer { get; set; }
    }

    public class AppLockerAppxEventSummary
    {
        public string PackageName { get; set; }
        public string Action { get; set; }
        public int Count { get; set; }
    }
}
