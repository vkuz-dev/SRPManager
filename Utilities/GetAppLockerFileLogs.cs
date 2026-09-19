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
    /// Reads AppLocker events directly from the Windows Event Log.
    /// Local reads use the live Event Log reader APIs. Remote reads go through WinRM
    /// (PowerShell Remoting via <see cref="RemoteManagement"/>), using the current user's
    /// credentials for pass-through auth.
    /// </summary>
    public static class GetAppLockerFileLogs
    {
        private static readonly string[] LogChannels =
        {
            "Microsoft-Windows-AppLocker/EXE and DLL",
            "Microsoft-Windows-AppLocker/MSI and Script"
        };

        private static readonly HashSet<int> WatchedEventIds =
            new HashSet<int> { 8002, 8003, 8004, 8005, 8006, 8007 };

        // ── Public API ──────────────────────────────────────────────────────────

        public static List<AppLockerFileEvent> GetEvents(
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

        private static List<AppLockerFileEvent> GetEventsLocal(
            int timeframeDays,
            CancellationToken ct,
            IProgress<ParseProgress> progress)
        {
            var results = new List<AppLockerFileEvent>();
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
                                    Debug.WriteLine("[AppLockerFileEventLog] Skipping event: " + ex.Message);
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (EventLogNotFoundException)
                {
                    Debug.WriteLine("[AppLockerFileEventLog] Channel not found (local): " + channel);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[AppLockerFileEventLog] Error reading (local) " + channel + ": " + ex.Message);
                }
            }

            results.Sort((a, b) => b.TimeCreated.CompareTo(a.TimeCreated));
            return results;
        }

        // ── Remote query ────────────────────────────────────────────────────────

        private static List<AppLockerFileEvent> GetEventsRemote(
            int timeframeDays,
            string machineName,
            CancellationToken ct,
            IProgress<ParseProgress> progress)
        {
            var results = new List<AppLockerFileEvent>();
            DateTime since = DateTime.UtcNow.AddDays(-timeframeDays);
            string xpath = BuildXPath(since);

            ct.ThrowIfCancellationRequested();
            progress?.Report(new ParseProgress { Channel = "AppLocker EXE/DLL/MSI/Script logs", EventsRead = 0 });

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
                            progress?.Report(new ParseProgress { Channel = "AppLocker EXE/DLL/MSI/Script logs", EventsRead = eventsRead });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[AppLockerFileEventLog] Skipping remote event: " + ex.Message);
                }
            }

            results.Sort((a, b) => b.TimeCreated.CompareTo(a.TimeCreated));
            return results;
        }

        // ── Remote XML parsing ───────────────────────────────────────────────────

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
                Debug.WriteLine("[AppLockerFileEventLog] Failed to parse remote event batch: " + ex.Message);
                return new List<XElement>();
            }
        }

        private static int GetEventId(XElement eventEl) =>
            int.TryParse(GetChildText(eventEl, "EventID"), out int id) ? id : -1;

        private static AppLockerFileEvent ParseEventXml(XElement eventEl, int eventId, string fallbackComputer)
        {
            DateTime.TryParse(
                GetAttr(eventEl, "TimeCreated", "SystemTime"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime timeCreated);

            string computer = GetChildText(eventEl, "Computer") ?? fallbackComputer;
            string policyName = GetChildText(eventEl, "PolicyName");
            string ruleName = GetChildText(eventEl, "RuleName");
            string filePath = GetChildText(eventEl, "FullFilePath");
            string fqbn = GetChildText(eventEl, "Fqbn");

            string user = SidResolver.Resolve(GetChildText(eventEl, "TargetUser"))
                ?? SidResolver.Resolve(GetAttr(eventEl, "Security", "UserID"));

            return new AppLockerFileEvent
            {
                TimeCreated = timeCreated,
                EventId = eventId,
                Action = ClassifyEventId(eventId),
                User = user ?? "N/A",
                FilePath = filePath ?? "N/A",
                PolicyName = policyName ?? "N/A",
                RuleName = ruleName ?? "N/A",
                TargetComputer = computer,
                Fqbn = fqbn ?? "N/A",
                LogName = ShortLogName(GetChildText(eventEl, "Channel"))
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

        private static AppLockerFileEvent ParseEvent(EventRecord record)
        {
            string filePath = null;
            string policyName = null;
            string ruleName = null;
            string user = null;
            string fqbn = null;
            string computer = null;

            try
            {
                var props = ((EventLogRecord)record).GetPropertyValues(AppLockerSelectors.FileSelector);

                // Computer comes from the event itself — reliable even for remote reads.
                computer = props[AppLockerSelectors.Computer] as string;
                policyName = props[AppLockerSelectors.PolicyName] as string;
                ruleName = props[AppLockerSelectors.RuleName] as string;
                filePath = props[AppLockerSelectors.FullFilePath] as string;
                fqbn = props[AppLockerSelectors.FileFqbn] as string;

                // TargetUser is element text → string; Security/@UserID → SecurityIdentifier object.
                string targetSid = SidResolver.ExtractSid(props[AppLockerSelectors.TargetUser]);
                if (!string.IsNullOrEmpty(targetSid))
                    user = SidResolver.Resolve(targetSid);

                if (user == null)
                {
                    string systemSid = SidResolver.ExtractSid(props[AppLockerSelectors.UserID]);
                    user = SidResolver.Resolve(systemSid);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLockerFileEventLog] Property read error: {ex.Message}");
            }

            computer ??= record.MachineName ?? Environment.MachineName;

            return new AppLockerFileEvent
            {
                TimeCreated = record.TimeCreated ?? DateTime.MinValue,
                EventId = record.Id,
                Action = ClassifyEventId(record.Id),
                User = user ?? "N/A",
                FilePath = filePath ?? "N/A",
                PolicyName = policyName ?? "N/A",
                RuleName = ruleName ?? "N/A",
                TargetComputer = computer,
                Fqbn = fqbn ?? "N/A",
                LogName = ShortLogName(record.LogName)
            };
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static string BuildXPath(DateTime since) =>
            string.Format(
                "*[System[TimeCreated[@SystemTime>='{0}']]]",
                since.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"));

        private static string ClassifyEventId(int id)
        {
            if (id == 8002 || id == 8005) return "Allowed";
            if (id == 8004 || id == 8007) return "Blocked (Enforced)";
            if (id == 8003 || id == 8006) return "Blocked (Audit mode)";
            return "Unknown";
        }

        private static string ShortLogName(string logName) =>
            string.IsNullOrEmpty(logName) ? "" : logName.Split('/').Last();

        // ── Summary ─────────────────────────────────────────────────────────────

        public static List<AppLockerFileEventSummary> GetSummary(List<AppLockerFileEvent> events)
        {
            return events
                .GroupBy(e => new { e.FilePath, e.Action })
                .Select(g => new AppLockerFileEventSummary
                {
                    FilePath = g.Key.FilePath,
                    Action = g.Key.Action,
                    Count = g.Count()
                })
                .OrderByDescending(s => s.Count)
                .ToList();
        }
    }

    // ── Data classes ─────────────────────────────────────────────────────────────

    public class AppLockerFileEvent
    {
        public DateTime TimeCreated { get; set; }
        public int EventId { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
        public string FilePath { get; set; }
        public string PolicyName { get; set; }
        public string RuleName { get; set; }
        public string Fqbn { get; set; }
        public string TargetComputer { get; set; }
        public string LogName { get; set; }
    }

    public class AppLockerFileEventSummary
    {
        public string FilePath { get; set; }
        public string Action { get; set; }
        public int Count { get; set; }
    }
}
