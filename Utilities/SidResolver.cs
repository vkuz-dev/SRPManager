using System;
using System.Collections.Concurrent;
using System.Security.Principal;

namespace AWLM.Utilities
{
    /// <summary>
    /// Thread-safe cache for SID → account-name translation.
    /// Repeated SIDs (SYSTEM, common domain users) are resolved only once.
    /// </summary>
    public static class SidResolver
    {
        private static readonly ConcurrentDictionary<string, string> _cache =
            new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Extracts a SID string from an event-log property selector result value.
        /// <c>Event/System/Security/@UserID</c> returns a <see cref="SecurityIdentifier"/> object,
        /// while UserData text fields return a plain string.  This method handles both.
        /// </summary>
        public static string ExtractSid(object propValue)
        {
            if (propValue is SecurityIdentifier si) return si.Value;
            return propValue as string;
        }

        /// <summary>
        /// Translates <paramref name="sid"/> to a display name.
        /// Returns the raw SID string if translation fails, or null if the input is empty.
        /// </summary>
        public static string Resolve(string sid)
        {
            if (string.IsNullOrWhiteSpace(sid)) return null;
            return _cache.GetOrAdd(sid, s =>
            {
                try
                {
                    return new SecurityIdentifier(s)
                        .Translate(typeof(NTAccount))
                        .ToString();
                }
                catch
                {
                    return s;
                }
            });
        }

        /// <summary>
        /// Translates a SID to a display name, passing through a value that is already a name.
        /// Falls back to the input if translation fails. Use this for values that may be either,
        /// such as a rule's UserOrGroupSid.
        /// </summary>
        public static string ResolveDisplayName(string sidOrName)
        {
            if (string.IsNullOrWhiteSpace(sidOrName)) return sidOrName;
            if (!sidOrName.StartsWith("S-1-", StringComparison.Ordinal)) return sidOrName;

            return Resolve(sidOrName) ?? sidOrName;
        }
    }
}
