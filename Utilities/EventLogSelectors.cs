using System.Diagnostics.Eventing.Reader;

namespace AWLM.Utilities
{
    /// <summary>
    /// Pre-built <see cref="EventLogPropertySelector"/> instances and index constants
    /// for AppLocker file and APPX events. Using selectors avoids serialising each
    /// record to XML and then parsing it back.
    /// </summary>
    public static class AppLockerSelectors
    {
        // ── Shared field indices (positions 0–6 in both selectors) ──────────────

        public const int TimeCreated = 0;
        public const int EventId = 1;
        public const int Computer = 2;
        public const int UserID = 3;   // System/Security/@UserID (fallback SID)
        public const int PolicyName = 4;
        public const int RuleName = 5;
        public const int TargetUser = 6;   // EventData TargetUser SID (preferred)

        // ── File-event-specific indices (positions 7–8) ─────────────────────────

        public const int FullFilePath = 7;
        public const int FileFqbn = 8;

        // ── APPX-event-specific indices (positions 7–8) ─────────────────────────

        public const int Package = 7;
        public const int AppxFqbn = 8;

        // ── Selectors ───────────────────────────────────────────────────────────

        // The Windows Event Log XPath evaluator matches bare element names without namespace
        // qualification, so "Event/UserData/RuleAndFileData/PolicyName" works even though
        // RuleAndFileData declares a different namespace.
        //
        // IMPORTANT: Security/@UserID is returned as a SecurityIdentifier object (not string).
        // All SID-valued fields (UserID, TargetUser) must be extracted via SidResolver.ExtractSid.

        public static readonly EventLogPropertySelector FileSelector =
            new EventLogPropertySelector(new[]
            {
                "Event/System/TimeCreated/@SystemTime",          // 0
                "Event/System/EventID",                          // 1
                "Event/System/Computer",                         // 2
                "Event/System/Security/@UserID",                 // 3  → SecurityIdentifier object
                "Event/UserData/RuleAndFileData/PolicyName",     // 4
                "Event/UserData/RuleAndFileData/RuleName",       // 5
                "Event/UserData/RuleAndFileData/TargetUser",     // 6  → SecurityIdentifier or string
                "Event/UserData/RuleAndFileData/FullFilePath",   // 7
                "Event/UserData/RuleAndFileData/Fqbn",           // 8
            });

        public static readonly EventLogPropertySelector AppxSelector =
            new EventLogPropertySelector(new[]
            {
                "Event/System/TimeCreated/@SystemTime",          // 0
                "Event/System/EventID",                          // 1
                "Event/System/Computer",                         // 2
                "Event/System/Security/@UserID",                 // 3  → SecurityIdentifier object
                "Event/UserData/RuleAndFileData/PolicyName",     // 4
                "Event/UserData/RuleAndFileData/RuleName",       // 5
                "Event/UserData/RuleAndFileData/TargetUser",     // 6  → SecurityIdentifier or string
                "Event/UserData/RuleAndFileData/Package",        // 7
                "Event/UserData/RuleAndFileData/Fqbn",           // 8
            });
    }

    public struct ParseProgress
    {
        public string Channel { get; set; }
        public int EventsRead { get; set; }
    }
}
